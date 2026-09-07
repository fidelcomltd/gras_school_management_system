# TASK-0005a — School identity and the append-only config_version ledger

Owner: backend-dev
Depends on: TASK-0002 (privilege register), TASK-0019 (idempotency substrate)
Contract impact: additive — `GET /settings`, `PATCH /settings/identity`, `GET /config-versions`,
`GET /config-versions/{id}`. Approved 2026-09-06: `decisions/2026-Q3-contract-deltas.md`
("TASK-0005 — School settings delta").
Status: done

Split from TASK-0005 on 2026-09-06 (three-way, a → b → c). Siblings: TASK-0005b (uploads),
TASK-0005c (registration numbers). **Read TASK-0005 for the goal, the out-of-scope list and the
two "look like details and are not" notes — they bind all three parts.**

## Goal

An administrator saves the school's identity, and the save is recorded as an immutable new
version rather than an overwrite. This is the first product surface in the system and the
substrate every later settings card writes through: 0005b and 0005c add groups to the same
payload and the same ledger without redesigning either.

## Contract delta — approved, implement as written

| Method | Path | Privilege | Idempotency-Key | Success |
|---|---|---|---|---|
| GET | `/api/v1/settings` | `settings.view` | — | 200 `SettingsDto`, `identity` group only this card |
| PATCH | `/api/v1/settings/identity` | `settings.identity.update` | accepted | 200 `SettingsIdentityGroupDto` |
| GET | `/api/v1/config-versions` | `audit.view` | — | 200 cursor-paged summaries (§9.5, never offset) |
| GET | `/api/v1/config-versions/{id}` | `audit.view` | — | 200 detail incl. full `snapshot`; 404 `config_version.not_found` |

Errors: `422 request.validation_failed` (field-keyed) · `401` (three auth variants) ·
`403 authorization.forbidden` · `403 auth.password_change_required` ·
`409 settings.identity.stale_version` · `429`.

`GET /settings` returns each group's current integer `versionNumber`; `PATCH` echoes it back as
`expectedVersion`. Compare it inside the same transaction as the `config_version` insert and
reject with `409` **before** anything is written.

## Acceptance criteria

- [x] `school_profile` singleton per 6.2.3: `school_name`, `short_name`, `address`, `phone`,
      `email`, `motto` (nullable), `head_teacher_name`, plus `timezone` fixed at `Africa/Lagos`,
      stored and echoed but **rejected if a PATCH tries to change it**.
- [x] `abbreviation` **seeded as `GRAS`** by the migration (6.2.2), with its own version pointer
      column. It is not editable through `PATCH /settings/identity` — TASK-0005c owns its endpoint.
- [x] `config_version` is append-only: every save writes a new row holding the **whole serialised
      configuration** (not just the changed group), plus actor, timestamp, reason where the group
      requires one, and a globally monotonic version number. Nothing is ever updated or deleted.
      A test proves a second save leaves the first row byte-identical.
- [x] The snapshot is **copyable-out whole** (6.2.9's rationale, TASK-0005 note 1). Do not
      optimise it into a reference to live rows.
- [x] Optimistic concurrency: a stale `expectedVersion` is rejected `409`, the first save wins,
      and **both attempts appear in the audit log** — the loser writes no version row, only the
      audit call. A test drives two saves against the same version and asserts exactly that.
- [x] **The 409 message is authored copy, not spec copy** (delta amendment 3): 6.2.11's sentence
      names the grading scale, which this card does not touch. Write the identity-group wording on
      that pattern and record it in `backend/docs/ASSUMPTIONS.md` as awaiting confirmation.
- [x] The audit seam gains an admin attribution path. Add an optional `actorAdminId` to
      `ISystemAuditSink.RecordAsync` (default `null`, so TASK-0019's purge caller is untouched)
      rather than a third parallel interface. Internal only — no contract impact.
- [x] `Idempotency-Key` **accepted** (not required) on `PATCH /settings/identity`, per the
      approved delta. A retry with the same key returns the stored replay and writes no second
      `config_version` row; `Idempotency-Replay` is a DECLARED response header, built by
      construction, never prose (TASK-0003's reopen reason, repeated by TASK-0019).
- [x] Cursor pagination on `GET /config-versions` per §9.5 — **never offset**.
- [x] Explicit EF Core migration, reviewed, **not auto-applied on startup**.
- [x] Tests per §6: every endpoint gets happy path, validation failure, unauthorized and
      not-found; plus unit tests for the append-only guarantee and the OCC race.
- [x] `./backend/scripts/ci.ps1` green, SUMMARY block pasted. `Skipped > 0` is not a pass.
      Contract regenerated via `backend/scripts/generate-openapi.ps1 -Promote`; lock hash matches.

## Out of scope

- Logo and signature uploads (0005b) and everything registration-number (0005c). Do not model
  `stored_file` or the counters here — 0005b/c create them.
- Every group in TASK-0005's out-of-scope list: grading scale, assessment structure, traits,
  rating scales, result rules, pin defaults, fee notice, `GET /settings/impact`,
  `GET /setup/checklist`.
- Any settings screen. Frontend follows once the contract is committed.

## Notes for the implementing agent

Read `.agent/STATE.md` first and append last. Read the approved delta in
`decisions/2026-Q3-contract-deltas.md` (the TASK-0005 section) and TASK-0005's `## Log` — the DB
shapes are argued there already; do not re-derive them.

Spec: `04-module-school-settings.md` §6.2.2 (what is seeded), §6.2.3 (identity fields), §6.2.9
(snapshot rationale), §6.2.10 (identity is never locked, no reason required), §6.2.11 (the
concurrency case); `14-non-functional-requirements.md` §9.5.

**Stop on ambiguity** — write the question into this card's `## Log` and raise it. Do not choose
product behaviour.

## Log
- 2026-09-06 created by orchestrator, splitting TASK-0005 after approving its delta.
- 2026-09-06 **implementing dispatch DIED MID-RUN on a session rate limit** (the third such
  death on this project, after TASK-0003 and TASK-0019) — after writing and promoting, before
  verifying. Per the standing lesson, the orchestrator ran the gates itself rather than believing a
  silent dispatch.
- 2026-09-06 **Gates run by orchestrator, not taken from a report.** `./backend/scripts/ci.ps1`
  SUMMARY: all 10 gates PASS — Restore, Format, Build (warnings as errors), Generate OpenAPI
  document, Unit & architecture tests, Integration tests, Coverage threshold (60%), Vulnerable
  dependencies, Secret scan, **OpenAPI contract drift**.
  `Tests: total=338 passed=338 failed=0 skipped=0` · `Coverage: line=79.17% branch=60.52%`.
  `CONTRACT.lock` `9a42360a…` matches `sha256(contracts/openapi.json)` exactly.
- 2026-09-06 **Contract verified against the approved delta, endpoint by endpoint.** Exactly the
  four approved paths appeared and nothing else. `Idempotency-Key` is declared OPTIONAL (accepted,
  not required) on `PATCH /settings/identity`, and **`Idempotency-Replay` is a DECLARED response
  header on every response of that operation** — built by construction by the new
  `IdempotencyHeaderOperationTransformer`, which reads the same marker
  `RequireIdempotencyKey` attaches when it wires enforcement. That is the precise defect TASK-0003
  was reopened for and TASK-0019 repeated, now closed by construction rather than by prose.
  `/config-versions` takes `cursor`+`pageSize`, never `page`/`offset` (§9.5).
- 2026-09-06 **Amendment 3 honoured**: the `409` sentence is authored copy, recorded in
  `ASSUMPTIONS.md` §2.15, not passed off as spec copy. **Checked the failure mode the unit test
  could not see**: the stale-save path writes its audit record through `LoggingSystemAuditSink`,
  which logs and touches no transaction — so 6.2.11's "both attempts appear in the audit log"
  survives the rollback in production, not merely against a fake sink in a unit test.
- 2026-09-06 **One disclosed deviation from the card**: `actorAdminId` was added to
  `ISystemAuditSink.RecordAsync` as a REQUIRED positional parameter rather than an optional one
  defaulting to `null`. Accepted as an improvement — it forces every future caller to decide
  rather than inheriting a silent default, and TASK-0019's purge caller now passes `null`
  explicitly with the reason in its XML doc.
- 2026-09-06 **Frontend client regenerated by frontend-dev (mechanical dispatch, §4.3's
  handoff step, no feature code).** `npm run generate:api` from the committed `9a42360a…`
  contract; `npm run check:api-drift` clean; `src/api/schema.d.ts` diff is purely additive
  (637 lines, generated header intact, no hand edit). `client.ts` needed **no change** —
  `tsc -b` passed against the four new operations with the existing `apiGet`/`apiPost`
  wrappers untouched, since this card intentionally ships no caller of them. Full frontend
  gates green: `npm run verify` (typecheck clean, `oxlint --max-warnings=0`, `143 passed
  (143)` / 20 files / `Skipped: 0`, build clean) and `npx playwright test` `4 passed
  (34.5s)`, `Skipped: 0`. **Left for the settings-screen card, recorded in `STATE.md`'s
  Decisions rather than acted on here**: `client.ts` has no `apiPatch` wrapper yet (PATCH
  /settings/identity is the contract's first PATCH operation; the transport-layer
  `patchRequest` already exists in `src/lib/http/request.ts`, only the typed wrapper is
  missing), and `apiGet` has no path-parameter templating (GET /config-versions/{id} is the
  first path-param operation any real caller will exercise; `/reference/arms/{armId}/secure`
  declares one too but has never been called). Both are additive typing work for that later
  card, not contract problems.
- 2026-09-06 **CLOSED by orchestrator.** §4.3's handoff completed: the frontend client was
  regenerated off the committed document by a mechanical frontend-dev dispatch, and I re-verified
  it rather than taking the report — `check-api-schema-drift.mjs` exits 0 ("matches
  contracts/openapi.json. No drift."), `schema.d.ts` keeps its `GENERATED — DO NOT EDIT`
  header, and its diff is purely additive (+637, nothing removed or rewritten). Frontend gates:
  typecheck/lint clean, 143/143 tests, build ok, Playwright 4/4 — all still green against the
  larger contract. All four §4.4 drift checks now hold at once for the first time since the
  contract moved.
