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

Full log: [logs/TASK-0005a.log.md](logs/TASK-0005a.log.md) — moved 2026-09-14. Card closed; final entry kept here:

- 2026-09-06 **CLOSED by orchestrator.** §4.3's handoff completed: the frontend client was
  regenerated off the committed document by a mechanical frontend-dev dispatch, and I re-verified
  it rather than taking the report — `check-api-schema-drift.mjs` exits 0 ("matches
  ... full closure account (5 more lines) in [logs/TASK-0005a.log.md](logs/TASK-0005a.log.md).
