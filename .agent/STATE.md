# Project State

Last reconciled: 2026-09-06 by orchestrator · no size cap, see `## Reading this file`

## Product

**GRAS School Management System** — Golden Royal Ark School (nursery + primary, Nigerian
three-term session). Back office for config, pupils, admission, marks, results and pins, plus one
public page where a parent redeems a reg number + access pin to read a published result.
**Parents have no accounts.**

Authoritative spec: `product-specification/` rev 3.1, entry `index.md` (the school's `.docx` is
rev 1 and is **not** authoritative). Out-of-scope list: `00-document-overview.md` 3.2 (§13).

## Layout
backend:  ./backend — solution `SchoolManagement.slnx`. Api / Application / Domain /
          Infrastructure under src/; Unit, Integration and Architecture tests under tests/.
          First real domain is `Domain/Auth/` (TASK-0003); `/api/v1/reference/*` and `/health/*`
          remain scaffolding. Conventions: `backend/AGENTS.md`. Deviations: `docs/ASSUMPTIONS.md`.
frontend: ./frontend — Vite 8 / React 19 / TypeScript 6, package `gra-school-portal`, npm
          (lockfile committed). Design tokens, Base UI, axios transport in `src/lib/http/`,
          TanStack Query, Zustand, MSW, react-hook-form + zod, Playwright. Top-level: `src/api/
          app/ components/ config/ features/ lib/ stores/ test/`. `features/<feature>/` is the
          documented home for screens (`CONVENTIONS.md` §4); `features/auth/` is its first
          tenant (TASK-0021). `screens/` DELETED 2026-09-06; `shared/` deliberately absent.
          Conventions: `CONVENTIONS.md`, `HANDOFF.md`, `src/api/README.md`.
contract generator: Microsoft.Extensions.ApiDescription.Server/10.0.10 via
          `backend/scripts/generate-openapi.ps1 -Promote` — the ONLY sanctioned way
          `contracts/openapi.json` and `CONTRACT.lock` change.
client generator:   openapi-typescript@7.13.0 (types only, pinned exact, devDependency).
          `generate-api-schema.mjs` writes `src/api/schema.d.ts` (`npm run generate:api`);
          `check-api-schema-drift.mjs` is the §4.4 check-2 gate. `src/api/client.ts` is the
          hand-written typed request helper over `src/lib/http/`.
repo:     git, branch `main`, origin https://github.com/maxcotech/school-management-proj.git

## Toolchain present on this machine
dotnet SDK 10.0.100 · node v22.21.0 · npm 10.9.4 · yarn 1.22.22 · pnpm ABSENT · pwsh ABSENT ·
docker ABSENT (integration tests use hosted Neon Postgres via `POSTGRES_TEST_CONNECTION`;
CI uses a service-container Postgres) · psql ABSENT · git 2.51.1.windows.1 · gitleaks 8.30.1

**PINNED — changing one side alone re-breaks CI (0024, 0026):** `global.json`
`rollForward: latestPatch` · gitleaks **8.30.1** in `backend-ci.yml` must equal local · Node
`22.21.0` · **`AnalysisLevel 10.0-All` + `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.100, forced
over the SDK copy by `backend/Directory.Build.targets`** · `TestingPlatformDotnetTestSupport=false`.
CI prints `dotnet --version`. Re-run the `/analyzer:` check in that targets file after any bump.

## Gate commands
backend:  `./backend/scripts/ci.ps1` — 10 gates cheapest-first, STOPS at the first failure.
          `Failed`/`Skipped > 0` exit non-zero (latter unless `-AllowSkipped`); `-NoFailFast` runs
          all, as CI does. Ends in a `SUMMARY` block — paste that alone for §9. Set
          `POSTGRES_TEST_CONNECTION` (`$HOME/.gras/pg-test.txt`) and call it DIRECTLY — the
          `local-env.ps1` wrapper here is stale (drift). CI: `backend-ci.yml`.
frontend: `npm run verify` (typecheck, lint, test, build) plus `npm run check:api-drift`
          (§4.4 check 2, deliberately NOT folded into `verify`) plus `npm run test:e2e`
          (Playwright, own CI job). oxlint, not ESLint. `npm run generate:api` regenerates the
          client when the contract moves. CI: `.github/workflows/frontend-ci.yml`.

## Contract
openapi.json sha256: 9a42360a8cf3741bea8a261eac405d31c71fdc08b13db3fd60303043880d5a20
          `X-CSRF-Token` is a required header parameter on exactly the four mutating auth
          operations, emitted by the same call that wires enforcement.
regenerated: 2026-09-06 by TASK-0005a, `-Promote` (generator + SDK under `## Layout`).
          **13 paths now** — the four settings/config-version paths joined the six auth and three
          reference ones. **The frontend client is STALE against this hash until it is regenerated
          (§4.4 check 2, §4.3's handoff).**
api version: v1 · 13 paths: `/auth/{csrf,sign-in,sign-out,me,refresh,password}` +
          `/settings`, `/settings/identity`, `/config-versions`, `/config-versions/{id}` +
          `/reference/{ping,records,arms/{armId}/secure}`. `/health/*` excluded
          (`ASSUMPTIONS.md` §2.9); `/reference/*` is scaffolding. Client regenerated 2026-09-06
          (frontend-dev, mechanical dispatch off TASK-0005a) and now matches the `9a42360a…`
          hash — `check:api-drift` clean. History: `decisions/2026-Q3.md`.

## Auth decision
mechanism: **HttpOnly cookie session + CSRF token.** Human sign-off 2026-08-26 per §5. Token
           fixed by spec 9.1: opaque 32-byte CSPRNG, stored hashed, rotated on privilege and
           password change. Not a JWT. JS never holds it. Implemented by TASK-0003.
cookie:    `HttpOnly`, `Secure`, `SameSite`, domain and the frontend's `credentials` mode are ONE
           coherent set (§5); a mismatch is a blocker. Attributes, CSRF construction and error
           codes: the approved delta in `decisions/2026-Q3-contract-deltas.md`.
csrf:      required on every mutating request, applied in the API layer.
refresh:   one path, single-flight (§5). All 401 variants are TERMINAL — a reactive 401 goes to
           sign-in, never to `/refresh`; only a proactive pre-expiry refresh has a job (delta §3a).
logout:    server-authoritative. Revoke, THEN clear client state; clearing alone is a blocker.
password:  Argon2id, 150-300 ms per hash, cost stored with the hash (spec 9.1). Fixed.
2FA:       none in v1 (spec 9.1, Appendix A 5). Fixed.
portal:    pin validation only, no accounts (spec 6.8, 6.9).

## In flight

Open cards only. Closed: TASK-0001-0004, 0006-0026, 0005a (0021 and 0005a closed 2026-09-06) — closure notes and reopen
history in [decisions/2026-Q3.md](decisions/2026-Q3.md).

| Task | Title | Owner | Status |
|---|---|---|---|
| TASK-0027 | Admin account management | backend-dev | **blocked — human sign-off (§5)** |
| TASK-0028 | Roles, assignments, privilege register | backend-dev | queued (stub card) |
| TASK-0005b | Logo and signature uploads | backend-dev | queued (stub card) |
| TASK-0005c | Registration number configuration | backend-dev | queued (stub card) |

Full sequence and cards not yet written: [ROADMAP.md](ROADMAP.md).

## Decisions

- 2026-09-06 **Frontend API client regenerated off TASK-0005a's committed contract
  (`9a42360a…`, 13 paths) — mechanical dispatch, no feature code.** `npm run generate:api`
  rewrote `src/api/schema.d.ts` (purely additive, 637 lines, generated header intact);
  `npm run check:api-drift` came back clean. **No `client.ts` change was forced**: `tsc -b`
  passed with zero errors against the four new operations untouched by any caller (none
  exist yet, by design), so the `QueryOf`/`RequestBodyOf`/`never`-normalisation machinery
  TASK-0021 built stayed sufficient without modification this time. Full `npm run verify`
  green — typecheck clean, `oxlint --max-warnings=0` clean, `143 passed (143)` across 20
  test files (`Skipped: 0`), build clean (`vite build` succeeded, 9 chunks emitted); `npx
  playwright test` `4 passed (34.5s)`, `Skipped: 0`. **Flagged for the next settings-screen
  card, not acted on here (out of this card's scope):** `client.ts` currently exposes only
  `apiGet`/`apiPost` (`PathsWithMethod<'get'|'post'>`) — `PATCH /settings/identity` is the
  first PATCH operation in the contract, and calling it will need a new `apiPatch` wrapper
  over the already-existing `patchRequest` in `src/lib/http/request.ts` (the transport layer
  already has it; only the typed `client.ts` wrapper is missing). Separately, `GET
  /config-versions/{id}` is the first operation with a **path** parameter
  (`operations["GetConfigVersion"]["parameters"]["path"].id`) that any real caller will
  exercise — `apiGet`'s current signature only threads query params through
  `RequestOptions.params` and has no path-templating; `/reference/arms/{armId}/secure`
  declares a path param too but has never been called from frontend code, so this substrate
  gap has never been exercised until now. Both are typing additions for whoever builds the
  settings screen, not contract problems. Also noted: `versionNumber`/`expectedVersion` on
  the identity DTOs generate as `number | string` (pattern-constrained int32-as-string) —
  same shape as other cursor/version fields elsewhere in the schema, nothing new to handle.

**Standing decisions and live lessons.** Closed-card records collapse at the bottom; full text is
[decisions/2026-Q3.md](decisions/2026-Q3.md) and the card's `## Log`. Approved contract deltas:
`decisions/2026-Q3-contract-deltas.md`.

- 2026-09-06 **TASK-0005a implemented; its dispatch DIED MID-RUN on a session rate limit — the
  THIRD time on this project (TASK-0003, TASK-0019, now this).** The work was on disk, the contract
  already promoted, and nothing verified. The standing lesson held: the orchestrator ran
  `ci.ps1` itself rather than believing a silent dispatch, and it was green — **all 10 gates,
  `total=338 passed=338 failed=0 skipped=0`, line 79.17%**, including the OpenAPI contract-drift
  gate, with `CONTRACT.lock` `9a42360a…` matching the document byte for byte. This third
  occurrence promotes the lesson from forming to **standing**: *a dispatch that dies after writing
  and before verifying leaves a working tree that looks finished and is not — and the tell is
  silence, not a failure message.* **New contract hash `9a42360a…`, 13 paths.**
  Verified against the approved delta rather than against the report: exactly the four approved
  paths, nothing extra; `Idempotency-Key` optional and **`Idempotency-Replay` a DECLARED
  response header built by construction** from the marker `RequireIdempotencyKey` attaches
  (closing, structurally, the defect TASK-0003 was reopened for); cursor pagination with no offset
  parameter anywhere. Also checked the thing the unit tests could not: the stale-save audit record
  goes through `LoggingSystemAuditSink`, which touches no transaction, so 6.2.11's "both attempts
  appear in the audit log" survives the rollback in production and not just against a fake.
  One disclosed deviation, accepted: `actorAdminId` is a required parameter on
  `ISystemAuditSink.RecordAsync` rather than an optional one — every caller must now decide,
  and TASK-0019's purge job passes `null` explicitly.
- 2026-09-06 **TASK-0005 delta APPROVED WITH FOUR AMENDMENTS; card SPLIT three ways
  (0005a identity+ledger, 0005b uploads, 0005c registration numbers).** Reviewing the delta against
  the SPEC rather than against itself paid again. **The costliest catch: `serial_reset` is an enum
  of `per_year` OR `continuous` (6.2.4), and the proposed counter keyed on `admission_year`
  alone can only express the first** — `continuous` would have been accepted by the API, stored in
  the database, and changed no behaviour whatsoever. That is this project's recurring defect family
  (a control that reports success and does nothing) arriving in a new module. Also amended:
  `abbreviation.issuedCount` must be NULLABLE rather than `0`, because no pupil register exists
  to count and `0` is a claim, not an absence; 6.2.11's stale-save sentence names the GRADING
  SCALE, so the identity-group wording is authored copy and must be recorded as such rather than
  passed off as spec; and no `DELETE` logo route gets invented to satisfy an acceptance criterion —
  6.2.12 enumerates none, so the rejection becomes a domain invariant on the set→null transition
  (an AC whose only proof is a route that does not exist is vacuous). Confirmed as proposed: the
  client-echoed integer `expectedVersion` concurrency design, one global append-only ledger holding
  the WHOLE serialised configuration per write (6.2.9's snapshot rationale, never a reference),
  `Idempotency-Key` **accepted not required** on all five mutating routes (TASK-0019 Part 2's
  precedent), the two privilege-checked serving endpoints §9.6 requires and spec 6.2.12 omits, and a
  non-empty abbreviation reason with no 10-character floor. Verified independently rather than taken
  on report: all five privilege strings exist verbatim in `Privileges.cs`, and the preview's
  current-abbreviation reading is exactly what 6.2.4 states. Full text:
  `decisions/2026-Q3-contract-deltas.md`.
- 2026-09-06 **TASK-0021 implemented by frontend-dev — cookie auth seam + sign-in/landing
  screens; status → review.** Bearer deleted (not disabled) from `src/lib/auth/auth-session.ts`
  and `src/lib/http/http-client.ts`; both rewritten to HttpOnly cookie + double-submit CSRF per
  the eight orchestrator rulings — CSRF read from the `GET /auth/csrf` response BODY only (never
  `document.cookie`), rotated after sign-in/password, single retry-once on a `csrf.missing`/
  `csrf.invalid` 403. Amended AC-5 implemented as ONE synchronous `terminateSession()` guarded by
  a `sessionActive` flag: idempotent by construction, so several concurrent 401s in the same tick
  collapse to exactly one sign-out notification and zero `POST /auth/refresh` calls (delta §3a —
  every 401 variant is terminal, only a *proactive* call earns `/refresh`'s keep). The proactive
  keepalive is derived from `sessionExpiresAt` clamped by `sessionAbsoluteExpiresAt` (never a
  hardcoded duration — grepped the diff to confirm), re-armed centrally from every
  `AuthSessionResponse` by a response-interceptor path match rather than per-hook wiring, and is
  itself single-flight. New: `src/features/auth/` (sign-in screen + form, a protected `/` landing
  screen, `api.ts` query/mutation hooks, `types.ts`, a zod sign-in schema) — TASK-0020's promised
  first tenant of `src/features/`. `src/screens/` and `src/stores/session-store.ts` deleted.
  **Spread beyond the two named seam files, disclosed rather than silent**: `request.ts` (renamed
  `expireSession`→`terminateSession`), `lib/http/index.ts` (exports `ensureCsrfToken`), a new
  `lib/http/http-client-guards.ts` (pure predicates split out only to hold `http-client.ts` under
  the 180-line cap), `app.tsx` (CSRF bootstrap replaces the deleted store's wiring), and a
  necessary fix to `src/api/client.ts`'s `QueryOf`/`RequestBodyOf` — `openapi-typescript` emits a
  literal `never` for a declared-empty query/body, distinct from the property being absent, and
  `me`/`sign-out` are the first zero-query/zero-body endpoints this generated client had ever been
  asked to call. **Contract observation, not a blocker**: delta §2's `423` body documents a
  `lockedUntil` extension, but the committed `ProblemDetails` schema is `additionalProperties:
  false`, so it isn't typed yet — the sign-in form shows the generic `detail` message for a `423`
  rather than the exact unlock time. **New drift** below (`mustChangePassword` has no
  change-password screen yet, ruling 6). Gates: `npm run verify` 143/143, `Skipped: 0`,
  typecheck/lint/build clean; `check:api-drift` clean (contract hash unmoved since TASK-0025);
  `test:e2e` 4/4 (Playwright stubbed per ruling 7, no backend). CORS/cookie coherence proved live
  (ruling 8) against a running API started via env overrides only, nothing under `backend/**`
  touched: `Set-Cookie: __Host-XSRF-TOKEN=…; secure; samesite=lax` (no `Domain`, matching the
  `__Host-` prefix), `Access-Control-Allow-Credentials: true`, `Access-Control-Allow-Origin`
  echoing the exact caller origin (`http://localhost:5173` and `:4173`, never a wildcard) on both
  the CSRF `GET` and a mutating-route `OPTIONS` preflight. Full text: the card's `## Log`.
- 2026-09-06 **TASK-0021 dispatched; its AC-5 was STALE and is amended.** The card asked for a test
  proving "exactly one refresh request" behind concurrent 401s — wording that predates approved
  contract delta §3a, under which all three 401 variants are TERMINAL and a reactive 401 must
  never call `/refresh` (a cookie session has no second credential). Left alone, the card would
  have driven an agent to build the very bearer-retry interceptor the delta says to replace
  wholesale. Single-flight now means **collapse concurrent 401s into ONE sign-out-and-redirect**,
  tested as one transition plus ZERO `/refresh` calls; the proactive keepalive gets its own
  single-flight test. Eight further rulings written into the card so the dispatch guesses at
  nothing: CSRF token read from the `/auth/csrf` BODY not `document.cookie` (the `__Host-`
  prefix makes the cookie unreadable cross-host in any real deployment, so reading it works
  locally and fails in production), keepalive derived from `sessionExpiresAt` clamped by
  `sessionAbsoluteExpiresAt`, `/` as a minimal protected landing, `mustChangePassword` surfaced
  but not implemented, Playwright stubbing `page.route` with no backend, and the CORS proof taken
  from a running API started via env overrides only. Full text: the card's `## Orchestrator rulings`.

- **STANDING LESSONS ON GATES** (0010/0011/0015/0016, 0022-0026). **(1) A check that cannot be shown to fail is not a check** — break what it guards, watch it go red, then accept it; fixtures must match the real artefact, and a comment claiming coverage is not coverage. **(2) A gate is only as trustworthy as the reproducibility of its INPUTS** — before trusting green, ask what it reads that is neither committed nor pinned. TASK-0026 satisfied both at once: it made CI's failure reproduce locally, which then exposed 4 more sites. Full notes in `decisions/2026-Q3.md`.
- 2026-09-05 **TASK-0022: the gate self-test had NEVER been able to run in CI** — its fixtures were `*.trx`-ignored, never committed. Reviewing the DIFF, not the agent's report, is what found it. `decisions/2026-Q3.md`.
- 2026-09-05 **Open question 11 RESOLVED (human): follow §7** — `src/features/<feature>/`, react-hook-form+zod, Playwright; no bulk rename, `src/shared/` waits. Done by TASK-0020. Full note in `decisions/2026-Q3.md`.
- 2026-09-06 **TASK-0019 CLOSED — idempotency substrate shipped, contract-neutral.** 10/10 gates, 264/264, `Skipped: 0`, line 79.52%. **Second card running whose implementing agent hit a session limit mid-dispatch** (TASK-0003 was the first): the work was on disk and correct, but unverified and unrecorded. The orchestrator ran the gates and reconciled. Standing lesson forming: *a dispatch that dies after writing and before verifying leaves a working tree that looks finished and is not* — run the gates yourself before believing a silent dispatch. Proven-not-vacuous on review: real-concurrency test, and redaction asserted against the STORED row, not the replayed response.
- 2026-09-06 **TASK-0019 delta reviewed; card SPLIT THREE WAYS.** Part 1 (`Idempotency-Key`) APPROVED with two amendments — `Idempotency-Replay` must be a DECLARED response header built by construction, not prose (the exact defect TASK-0003 was reopened for), and the stored replay copy REDACTS `temporaryPassword`, because 6.1.9/6.1.14 say "never displays it again" and 6.1.14's `password-reset` already provides the lost-response recovery path. Part 2 (accounts) AMENDED and HELD. **Reviewing the delta against the spec rather than against itself found two endpoints missing from a list spec 6.1.14 literally enumerates** (`password-reset`, `DELETE /sessions`), a self-edit carve-out 6.1.2 requires and the draft omitted (name+phone only — NOT email), and 6.1.10's session-revocation-on-suspend. Now: TASK-0019 substrate (contract-neutral), TASK-0027 accounts (§5 sign-off), TASK-0028 roles/assignments. Full text: `decisions/2026-Q3-contract-deltas.md`.
- 2026-09-06 **STATE.md size cap REMOVED (human directive).** The 12 KB byte budget is gone from §4.1, §13, this file, and both dev subagent prompts. Rationale for keeping it — a line cap let this file reach 36 KB unnoticed — is preserved in `decisions/2026-Q3.md`; what replaces the cap is the archive discipline alone (long sections move to `decisions/`/`drift/` with a one-line index). **No agent may now defer, trim, or skip a STATE.md append to save bytes** — that behaviour blocked TASK-0003's reconciliation and is exactly what the removal is meant to end.
- 2026-09-05 **TASK-0023/0024/0025/0026 closed**: hermetic vitest `test.env`; SDK, gitleaks, analyzer VERSION and test bridge all pinned; 6 CA1873 + 1 CA2025 fixed not suppressed; client regenerated. **`AnalysisLevel: latest-All` was the real culprit — `latest` means "whatever SDK is installed", so the rule set was a property of the machine.** Contract unmoved throughout. Full notes in `decisions/2026-Q3.md`.
- 2026-09-06 **TASK-0019 dispatch 1 (contract delta only) delivered by backend-dev, awaiting orchestrator approval.** No code/migration/contract file touched. Idempotency-Key required only on `POST /api/v1/admins`, 24h retention (flagged: the create response's one-time temp password sitting in that store is an open question, not decided). New cursor envelope for `/api/v1/admins` (list/detail/create/edit/status). Seven open questions raised, including whether real `role`/`role_assignment` CRUD (escalation rules 1-3) belongs in this card or a follow-up — proposed a split. Full detail in the card's Log.
- 2026-09-06 **TASK-0005 dispatch 1 (contract delta only) delivered by backend-dev, awaiting
  orchestrator approval.** No code/migration/contract file touched — verified via `git status`/
  `git diff`; the sole file edit is `backend/docs/ASSUMPTIONS.md` §2.14, corrected (see `## Known
  drift` below) since it still read "deferred" though TASK-0019 shipped the mechanism before this
  dispatch even opened. **Proposed three-way split**, sequenced `a → b → c`, all depending on `a`'s
  `config_version` ledger: (a) identity + the `config_version` substrate itself + version-history
  read endpoints, (b) logo/signature uploads, (c) registration-number configuration (preview,
  width-reduction rejection, the abbreviation `CHANGE`-token flow). None of the three alone is
  close to the ~400-line dispatch cap. **Idempotency**: none of this card's routes are `POST
  /admins`-shaped (no independently-addressable entity created), so `PATCH /settings/identity`,
  both upload POSTs and `PATCH /settings/abbreviation|reg-number` are all **accepted**, not
  required — a naive retry converges state but would double a `config_version` row, the same class
  TASK-0019's Part 2 delta used for `PATCH /admins/{id}`. **Real gap found**: the existing
  fingerprint builder JSON-serialises the bound command, which doesn't meaningfully capture an
  uploaded file's bytes — TASK-0005b (uploads) must fold a content hash into the fingerprint or a
  reused key against a different image would misclassify as a replay. Recorded in the corrected
  `ASSUMPTIONS.md` §2.14, not silently deferred. **`ProblemDetails additionalProperties: false`**:
  no extension member needed anywhere in this card, on the explicit condition that the
  abbreviation-change affected-pupil-count is designed as ordinary `200` response data (surfaced via
  `GET /settings` and echoed by the `PATCH`'s own success body) rather than folded into an error —
  stated as a design choice, not a quiet workaround. **New DB design surfaced, not previously
  precedented anywhere in the codebase**: this is the first "edit something, detect a stale
  concurrent read" endpoint the system has — `SampleRecord` has no update command, so there was
  nothing to copy. Proposed a client-echoed integer `expectedVersion` per settings group (identity,
  abbreviation, registration-number each get their **own** OCC pointer despite abbreviation
  physically living in the same `school_profile` row as identity, so unrelated groups never
  spuriously conflict each other's saves), checked against the group's pointer inside the same
  transaction that appends a new row to one **global** monotonic `config_version` ledger holding
  the WHOLE serialised configuration every time (not per-group chains) — this is what lets
  `result_set.config_version_id` later be a single FK sufficient on its own, per 6.2.9's snapshot
  rationale and the card's own "do not optimise it into a reference" note. Explicitly asked for
  this to be confirmed rather than built silently. **Two endpoints the card's own table omitted,
  flagged rather than silently added or silently skipped**: a privilege-checked serving GET for the
  logo (`/settings/identity/logo/{size}`, sizes `original|200|64`) and the signature — §9.6 requires
  uploads be served back through an endpoint and none of the six listed paths does. Also flagged: no
  endpoint exists to reject 6.2.11's "logo deleted with no replacement" case against — that
  rejection has nothing to reject **on** without either a dedicated remove route or treating a
  file-omitting re-POST as the trigger. **Audit seam gap found**: neither existing audit seam
  (`ISystemAuditSink`, `IAuthorizationAuditSink`) carries an acting-admin id; proposed extending
  `ISystemAuditSink.RecordAsync` with an optional `actorAdminId` (default `null`, existing
  system-purge caller untouched) rather than a third interface — internal-only, no contract impact.
  **Two open questions raised, not resolved by the agent**: whether abbreviation's mandatory reason
  should carry 6.2.9's 10-character floor for consistency even though 6.2.9's own text doesn't
  reach this group (proceeding on "any non-empty reason" pending an answer), and confirmation of the
  two added serving-endpoint paths/shapes. Full delta, DB shapes and all four dispatch questions
  answered in full: the card's Log.

**Closed-card records** — archive-only, `grep` the ID in `decisions/2026-Q3.md`: 0001, 0003, 0012,
0013, 0014, 0017, 0018, 0020, 0022-0026, plus 2026-08-27's four decisions (gitleaks, gate honesty,
DB-credential split, TASK-0001 close).

## Known drift

Split by whether it can bite a dispatch. **Live triggers** are below — check them before every
dispatch. The rest are accepted deviations with no trigger, dated here, full text in
`drift/2026-Q3.md`.

**Live triggers**

- 2026-09-05 **`vite build` succeeds with NO `.env` and emits a bundle that throws on boot** (inlines `VITE_*` as `undefined`), so Build goes green on something unusable. CI copies `.env.example` to mask it; nothing checks env at build time. Unowned. **Trigger: any card touching build or deployment.**
- 2026-09-05 **`Microsoft.Testing.Platform.MSBuild` is an unpinned transitive floor; `backend/` has NO NuGet lock file.** Pinning it broke restore. **Trigger: next `xunit.v3` upgrade.** `drift/2026-Q3.md`.
- 2026-09-06 **Idempotency mechanism now EXISTS** (TASK-0019); the 2026-09-04 build-it-first trigger is RETIRED. What replaces it is lighter and still live: **any card shipping a retry-duplicable mutation must DECLARE `Idempotency-Key` on that route** and mark one-time credentials with `RedactFromIdempotencyReplayAttribute` — §9.8.2's four operations are EXAMPLES, not the list. **Trigger: TASK-0027, then TASK-0005 (checked in its dispatch-1 delta: all five mutating routes accept the header, none require it — none creates an independently-addressable entity; the two uploads additionally need the fingerprint to fold in a content hash, a real gap the delta found and recorded rather than deferring silently).** `backend/docs/ASSUMPTIONS.md` §2.14 **CORRECTED 2026-09-06 by TASK-0005's dispatch-1** (it read deferred; now states the mechanism as shipped and names TASK-0005 as the live per-route-declaration trigger). Owner `backend-dev`.
- 2026-09-05 **Three accepted auth exposures (TASK-0003):** unpersisted DP key ring (**trigger: deployment, Q5**); bootstrap CLI prints the temp password to stdout; `PersistLockoutStateAsync` timing asymmetry. Owner `backend-dev`. Full text: `drift/2026-Q3.md`.
- 2026-09-05 **`UseRateLimiter()` runs before `UseAuthentication()`** (`Program.cs:298` vs `:300`), so every rate-limit partition falls back to remote IP and the per-user branch is dead code; admins behind one NAT share the sensitive bucket. **Trigger: TASK-0027** (moved from TASK-0019 on 2026-09-06 — 0019 is now contract-neutral and ships no route; 0027 is where a per-user-partitioned route first exists). Owner `backend-dev`.
- 2026-09-05 `scripts/local-env.ps1` untracked/stale, splats `GateArgs` positionally. **Trigger: any dispatch running gates via the wrapper** — call `ci.ps1` directly. Full text: `drift/2026-Q3.md`.
- 2026-09-05 **`gate-summary.tests.ps1:101,:108` are near-unfalsifiable** — `-match` substring passes even against a mangled path; only in-process `-eq` caught the TASK-0022 mutation. **Trigger: next card touching that suite.**
- 2026-09-04 **The DEFAULT rate-limit policy has no 429 test.** Health covered by TASK-0015, sensitive by TASK-0003. Owner `backend-dev`, no trigger.
- 2026-09-04 **The Api→Infrastructure arch test exempts `StartupEnvironmentGuard` as well as `Program.cs`.** **Trigger: a THIRD exemption must argue for itself or the type gets a port** — the rule must not erode one name at a time. Full text: `drift/2026-Q3.md`.
- 2026-08-26 ~~Two known-wrong frontend leftovers, both TASK-0021's to DELETE~~ **STRUCK
  2026-09-06 — TASK-0021 closed both**: `src/lib/auth/` bearer deleted (cookie + CSRF model);
  `src/screens/scaffold-status/` and `src/screens/` deleted, `/` now a real protected landing.
  Full text: `drift/2026-Q3.md`.
- 2026-09-06 **`abbreviation.issuedCount` will ship as `null` and stay null.** 6.2.4's change
  dialogue names a real count ("412 pupils already hold registration numbers beginning GRAS"), but
  no pupil register exists until a later card. TASK-0005c ships the field nullable; `null` means
  "no register to count", never `0`. **Trigger: the pupil-registration card wires the count, and
  the settings-screen frontend card must suppress the count sentence while it is null.** Owner
  `backend-dev` then `frontend-dev`.
- 2026-09-06 **The logo-removal affordance is unrouted.** 6.2.11 requires "logo deleted with no
  replacement" to be rejected, and spec 6.2.12 enumerates no `DELETE` route to reject on. Held as
  a domain invariant with the verbatim message (TASK-0005b), not an invented endpoint. **Trigger:
  the settings-screen frontend card — if the UI needs a remove affordance, that is a missing
  endpoint to add deliberately, not a gap to paper over.** Owner `backend-dev`.
- 2026-09-06 **The committed contract FORBIDS a field the backend actually sends.**
  `ResultExtensions.cs:85` attaches a `lockedUntil` extension member to the `423` sign-in body
  (approved delta §2 documents it), but `ProblemDetails` is generated with
  `additionalProperties: false`, so the document says that response cannot carry it. **§4.4's
  drift check cannot catch this** — it compares the regenerated document against the committed
  one, and both are equally wrong; only reading the emitting code against the schema finds it.
  Found by orchestrator review of TASK-0021, where the frontend correctly fell back to the
  generic message. Fix is ADDITIVE (declare the member, or stop emitting it). Owner
  `backend-dev`. **Trigger: the next backend card touching auth or `ProblemDetails` —
  TASK-0027.**
- 2026-09-06 **The session-end redirect is subscribed per-screen, not once.**
  `features/auth/landing-screen.tsx` calls `onSessionEnded(… navigate(signIn))` itself.
  Correct today because `/` is the only protected screen, but §5's "implemented once" is
  satisfied by accident, not by construction. **Trigger: the SECOND protected screen — hoist the
  subscription into the router/shell rather than copy it.** Owner `frontend-dev`.
- 2026-09-06 **`mustChangePassword` is detected (gates the keepalive, per delta §2a/ruling 6) but
  there is still no change-password screen** — a flagged account can only read a plain message and
  sign out. **Trigger: the follow-up card wiring `POST /auth/password`.** Owner `frontend-dev`.
  Full text: `drift/2026-Q3.md`.
- 2026-08-26 **Staging and dev-test DBs share one role and password. Trigger: deployment** (Open question 5). Owner human.
- 2026-08-27 **Validation is a mediator pipeline behaviour, not §6's endpoint filter. Trigger: ratify or revert** — unowned. `ASSUMPTIONS.md` §2.2.

- 2026-09-05 **`SameSite=Lax` assumes frontend and API share a registrable domain** (TASK-0003 delta). Cross-site needs `SameSite=None` AND a reconsidered CSRF posture. **Trigger: deployment (Q5).** Owner human + `backend-dev`. Full text: `drift/2026-Q3.md`.

**Accepted, no trigger** — 8 entries, archive-only; `grep` the date in `drift/2026-Q3.md`:
2026-08-08 · 2026-08-26 ×4 · 2026-08-27 ×2 · 2026-09-04.

Thirteen resolved/struck entries are archive-only — latest: the two frontend leftovers
(bearer auth, scaffold-status), struck 2026-09-06 by TASK-0021.

## Open questions

Live only; ten resolved questions are in `decisions/2026-Q3.md`.

12. **TASK-0027 admin account management awaits §5 human sign-off.** Seven endpoints, two of which
   operate sessions and credentials on another account's behalf (`POST /admins/{id}/password-reset`,
   `DELETE /admins/{id}/sessions`); suspend and deactivate also revoke sessions. The auth MECHANISM
   is TASK-0003's and unmoved — what needs the ruling is a Super Admin operating it on someone
   else. Delta ready in `decisions/2026-Q3-contract-deltas.md`. **Blocks TASK-0027 only; TASK-0019
   is contract-neutral and proceeds.**

5. **Production database target** undecided; not blocking until deployment. Four live drift
   triggers wait on it (DP key ring, `SameSite=Lax`, shared DB role, cookie domain) — one
   decision clears all four.

## Reading this file

**No size cap** (removed 2026-09-06). Append what the ledger needs; never trim or defer an entry to
hit a byte target. When a section grows long, archive full text to `decisions/` or `drift/` and
leave a one-line index — never delete. Rules: CLAUDE.md §4.1, §13.
