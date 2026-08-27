# Project State

Last reconciled: 2026-08-26 by orchestrator

## Product

**GRAS School Management System** — Golden Royal Ark School (nursery + primary, Nigerian
three-term session). Back office for configuration, pupil register, admission, marks, result
computation/approval/publication, weekly pastoral reports and pin generation; plus one public
page where a parent enters a registration number and an access pin to read or download a
published result. Parents have no accounts.

Authoritative specification: `product-specification/` (revision 3.1, added 2026-08-26).
`index.md` is the entry point. The `.docx` in the school's possession is revision 1 and is
**not** authoritative. Design reference images in `product-specification/assets/`.

Explicitly out of scope (`00-document-overview.md` 3.2): fees/payments, HR/payroll,
timetabling, standalone attendance, messaging/SMS, library/transport, CBT, parent accounts.

## Layout
backend:  ./backend — solution `SchoolManagement.slnx`. Api / Application / Domain /
          Infrastructure under src/; Unit, Integration and Architecture test projects under
          tests/. Reference vertical slice only (`/api/v1/reference/*`, `/health/*`). No
          domain code yet.
frontend: ./frontend — Vite 8 / React 19 / TypeScript 6 scaffold, package `gra-school-portal`,
          npm (package-lock.json committed). Design tokens, Base UI primitives, axios transport
          in `src/lib/http/`, TanStack Query, Zustand, MSW, 124 tests green. `src/api/` now
          holds the generated schema + a hand-written typed helper (TASK-0004); `src/features/`
          is still EMPTY and reserved. See `frontend/HANDOFF.md` and `frontend/src/api/README.md`.
contract generator: Microsoft.Extensions.ApiDescription.Server/10.0.10, invoked via
          `backend/scripts/generate-openapi.ps1 -Promote`. That script is the ONLY sanctioned
          way `contracts/openapi.json` / `CONTRACT.lock` change.
client generator:   openapi-typescript@7.13.0 (types only, pinned exact), devDependency,
          wired TASK-0004. `frontend/scripts/generate-api-schema.mjs` writes
          `frontend/src/api/schema.d.ts` (`npm run generate:api`);
          `frontend/scripts/check-api-schema-drift.mjs` is the §4.4 check 2 drift gate
          (`npm run check:api-drift`). `frontend/src/api/client.ts` is the hand-written typed
          request helper layered over `src/lib/http/` — see `frontend/src/api/README.md`.
repo:     git, branch `main`, remote origin points at
          https://github.com/maxcotech/school-management-proj.git

## Toolchain present on this machine
dotnet SDK 10.0.100 · node v22.21.0 · npm 10.9.4 · yarn 1.22.22 · pnpm ABSENT ·
docker ABSENT (integration tests use a hosted Neon Postgres via `POSTGRES_TEST_CONNECTION`;
CI uses a service-container Postgres) · psql ABSENT · git 2.51.1.windows.1

## Gate commands
backend:  `./backend/scripts/ci.ps1` — restore, build -warnaserror, format --verify-no-changes,
          tests + coverage, coverage floor, vulnerable-package scan, gitleaks, OpenAPI drift.
          CI wrapper: `.github/workflows/backend-ci.yml`.
frontend: `npm run verify` (typecheck, lint, test, build) plus `npm run check:api-drift`
          (contract gate, §4.4 check 2 — not folded into `verify`; see
          `frontend/src/api/README.md` "What CI should run"). oxlint, not ESLint.
          `npm run generate:api` regenerates the client when the contract moves.
          CI wrapper: `.github/workflows/frontend-ci.yml`.

## Contract
openapi.json sha256: 228ae57fd80c22f7e0511a665efce5d4722bb3280c66f5e3f4c96fff53795a65
regenerated: 2026-08-26 · generator Microsoft.Extensions.ApiDescription.Server/10.0.10, SDK 10.0.100
api version: v1 · surface: `/api/v1/reference/ping|records|whoami|arms/{armId}/secure`,
          `/health/live|ready`. `/reference/arms/{armId}/secure` is new (TASK-0002) — reference-slice
          test scaffolding proving the privilege substrate, not a product endpoint. Hash supersedes
          the one first reported for TASK-0002 — that promotion shipped `SecureArmResponse` with no
          `OpenApiExamples` entry; see Known drift below and TASK-0002's Log for the fix.

## Auth decision
mechanism: **HttpOnly cookie session + CSRF token.** Human sign-off 2026-08-26 per CLAUDE.md §5.
           Token format is fixed by spec 9.1: opaque 32-byte CSPRNG, stored hashed server-side,
           rotated on privilege change and on password change. Not a JWT. The cookie carries
           that opaque token; JS never holds it.
cookie:    `HttpOnly`, `Secure`, `SameSite`, domain and the frontend's `credentials` mode are
           configured as ONE coherent set end to end (§5). A frontend sending
           `credentials: 'include'` against a backend not configured for it is a blocker.
csrf:      required on every mutating request. Implemented once in the API layer.
refresh:   one path, single-flight — concurrent 401s queue behind one refresh attempt (§5).
logout:    server-authoritative. Revoke server-side, then clear client state. Clearing client
           state alone is a blocker (§5).
password:  Argon2id, 150-300 ms per hash, cost parameters stored with the hash (spec 9.1). Fixed.
2FA:       none in v1 (spec 9.1, Appendix A entry 5). Fixed.
portal:    no authentication in the account sense — pin validation only (spec 6.8, 6.9).

## In flight
| Task | Title | Owner | Status |
|---|---|---|---|
| TASK-0001 | Re-audit backend and frontend scaffolds against §6/§7 | reviewer | queued |
| TASK-0002 | Privilege register and authorisation enforcement | backend-dev | done |
| TASK-0003 | Admin accounts, authentication and session management | backend-dev | queued |
| TASK-0004 | Wire the OpenAPI client generator and typed API layer | frontend-dev | done |
| TASK-0005 | School settings: identity, registration number, config versioning | backend-dev | queued |
| TASK-0006 | Regenerate the frontend client against the TASK-0002 contract | frontend-dev | done |
| TASK-0007 | Clear the SSH.NET High advisory blocking the vuln gate | backend-dev | queued |
| TASK-0008 | Give the integration-test connection string a durable local home | backend-dev | queued |
| TASK-0009 | Move the document-property contract tests off the database | backend-dev | done |
| TASK-0010 | Make OpenAPI document generation deterministic | backend-dev | in-progress |

**TASK-0002 closed 2026-08-26** after a verified live re-run: 178 tests, Skipped: 0, coverage
87.00% line / 65.58% branch, contract drift clean, hash matching the lock. Closed with one gate
knowingly red — the SSH.NET advisory, owned by TASK-0007 — recorded as an explicit exception.

**TASK-0006 is reopened**, not superseded: its purpose is "the client matches TASK-0002's
contract", and the reopen fix moved that contract from `d229f804` to `228ae57f`. One more
regeneration closes it.

**The lesson, kept because it cost real time:** TASK-0002 was reported done twice on runs where the
integration suite skipped. Run for real it failed, on a defect that had already reached the
committed contract. A skipped suite is not a passing suite, and the coverage floor is deliberately
not enforced when anything skipped — so a skipping run degrades two gates at once while looking
green. TASK-0009 moves the checks that never needed a database out of that blast radius.

Full sequence and the cards not yet written: `.agent/ROADMAP.md`.

## Decisions
- 2026-07-27 Scaffolding created (CLAUDE.md, .agent/, .claude/agents/, contracts/) — orchestrator owns these per §11; needed before any dispatch is possible.
- 2026-07-27 contracts/openapi.json deliberately NOT created as a placeholder — §3 says it is never written by hand, and a stub would make every §4.4 drift check meaningless. It appears at the first backend build.
- 2026-08-08 Git initialised (resolves Open question #2). Remote `origin` points at https://github.com/maxcotech/school-management-proj.git.
- 2026-08-08 Backend reference vertical slice implemented and its OpenAPI contract promoted for the first time — `contracts/openapi.json` + `CONTRACT.lock` now real, hash verified to match (see Contract). Integration tests verified against a hosted Neon Postgres rather than Testcontainers — accepted per backend/docs/ASSUMPTIONS.md §3.1; revisit if a team Docker/Postgres standard is set later.
- 2026-08-08 `.claude/settings.json` created: Edit/Write on `contracts/openapi.json` and `contracts/CONTRACT.lock` only are allowed without prompting, so a backend session running `generate-openapi.ps1 -Promote` isn't blocked. Nothing else under `contracts/**` or `.agent/**` is exempted — hand-edits there still prompt. This is a permission convenience, not a substitute for the §4.4 drift check.
- 2026-08-08 CI workflow activated: moved `backend/ci/github-actions-backend.yml` to `.github/workflows/backend-ci.yml` (root-level CI is orchestrator-owned per §11; the file was staged by backend-dev with an explicit note for the orchestrator to relocate it). Not yet pushed/verified against a live Actions run.
- 2026-08-26 Product scope resolved (Open question 7). `product-specification/` revision 3.1 is the authoritative requirements source for every task card. Cards cite spec section numbers rather than restating requirements, so a spec revision does not silently invalidate a card.
- 2026-08-26 Package manager: **npm** (Open question 3). Already in use, lockfile committed, pnpm absent. No reason to churn.
- 2026-08-26 Client generator: **openapi-typescript, types only** (Open question 6). Rejected openapi-fetch / NSwag / Kiota because the frontend already owns a working axios transport with auth interceptors and a single-flight refresh seam (`src/lib/http/`); a runtime client would duplicate it and force a rewrite of that seam. `src/api/` holds generated types plus a thin typed request helper over that transport, which satisfies §7's "all server communication goes through `src/api/`" without running two HTTP stacks.
- 2026-08-26 Build sequencing departs from `index.md`'s suggested order in one place: the privilege register and authorisation middleware (TASK-0002) come **before** school settings, not after. Spec 9.2 requires every route to declare the privilege it needs and to fail registration at boot if it does not — building settings endpoints first would mean writing routes against an enforcement layer that does not exist, then retrofitting it.
- 2026-08-26 **Auth carrier: HttpOnly cookie session + CSRF token** (Open question 8), human sign-off per §5. Rejected the bearer-in-memory approach the frontend scaffold provisionally built: a token held in JS is readable by any XSS, and it dies on page reload, so it must either sign the user out on refresh or be persisted somewhere — which is exactly the exposure the in-memory choice was trying to avoid. The cookie carries the same opaque server-side token spec 9.1 mandates, so nothing about the backend token model changes. Cost is a CSRF token on every mutation and one coherent cookie/CORS configuration end to end; the frontend change is confined to `src/lib/auth/auth-session.ts` and `src/lib/http/http-client.ts` per `frontend/HANDOFF.md`.
- 2026-08-26 Spec conflicts `25-open-conflicts-to-resolve.md` items 1, 2 and 5 are resolved by the school; items 3, 6, 7, 8 and 9 have provisional resolutions the build follows as written. Item 4 (weekly Parent's Comment) is the only unresolved item that changes what gets built; it is sequenced late (weekly reports) and blocks nothing before it. No subagent may amend a provisional resolution — raise it in Open questions instead.
- 2026-08-26 TASK-0004 closed: contract pipeline wired. `openapi-typescript@7.13.0` (pinned exact) generates `frontend/src/api/schema.d.ts`; `frontend/src/api/client.ts` (`apiGet`/`apiPost`) is a hand-written typed helper over the existing `src/lib/http/` transport — no second HTTP stack, auth seam untouched. MSW defaults (`src/test/msw/handlers.ts`) are now derived from `contracts/openapi.json` via `src/test/msw/openapi-handlers.ts` instead of hand-written. A new Vitest boundary test (`src/test/http-boundary.test.ts`) fails on any raw `axios`/`fetch(` outside `src/lib/http/` — §4.4 check 3. Deviation: `openapi-typescript@7.13.0` declares `peerDependencies: { typescript: "^5.x" }` and this repo runs TypeScript 6.0.3, which a plain `npm ci` rejects with ERESOLVE. Resolved with a scoped `package.json` `overrides` entry (`{ "openapi-typescript": { "typescript": "$typescript" } }`), not a repo-wide `.npmrc` `legacy-peer-deps` flag — the orchestrator caught that the broader flag would silently let a genuine future peer conflict install broken instead of erroring. The override pins only openapi-typescript's own `typescript` resolution to this repo's `typescript` devDependency range; every other package's peers are still checked normally. Verified with a fully clean `npm install` (no lockfile, no node_modules, no `.npmrc`) — resolves without ERESOLVE — and `npm ci` from the resulting lockfile. Justification recorded in `frontend/CONVENTIONS.md` §1. Revisit (and drop the override) once openapi-typescript ships a TS 6-aware peer range upstream. `.oxlintrc.json` ignorePatterns changed from blanket `src/api/**` to `src/api/schema.d.ts` only (hand-written `client.ts` is now linted) plus `scripts/**` added (Node codegen scripts, not shipped/bundled code, same rationale as excluding `dist`/`coverage`).

- 2026-08-26 **Frontend dependency refresh accepted, reviewed after the fact.** Closing TASK-0004 the frontend agent regenerated `package-lock.json` from scratch rather than adding one devDependency surgically. That swept 73 resolved versions, added 21 packages and removed 7. Every bump is inside a caret range `package.json` already declared, so nothing violates a pin the team chose; all gates are green on the new tree (124/124, zero lint warnings, clean build, no drift) and `npm audit` reports 0 vulnerabilities where the old tree carried a `nanoid` advisory. Accepted on that basis — reverting to re-pin 73 packages would be churn against a healthier tree. What was wrong was the process, not the result: this was never in the card, and §8 requires one logical change per commit. It therefore lands as its OWN commit, separate from the codegen work, so a later bisect can tell a base-ui regression from a codegen bug.

- 2026-08-26 Frontend CI workflow written (`.github/workflows/frontend-ci.yml`), root-level CI being orchestrator-owned per §11. Mirrors backend-ci.yml's house style: path-filtered on `frontend/**` and `contracts/**`, `npm ci` never `npm install`, Node pinned to 22.21.0 to match the dev machine (raising it and the React Router 7 pin in HANDOFF.md decision 2 must be revisited together), and the contract drift check as its own final step so a stale generated client reads differently from a broken frontend. Neither workflow has been verified against a live Actions run yet.

- 2026-08-26 **TASK-0002 closed.** The 93-privilege register, scope resolution (spec 4.2.1's four rules), the effective-privilege-set decision (`PrivilegeDecision.IsAuthorized`), the escalation guard (`RoleScopeGuard`), and the boot-time privilege-declaration guard are all built and tested. Two deliberate design choices worth recording:
  - **No `admin_account`/`role`/`role_assignment` persistence was added**, contrary to the task card's fallback permission to add it "if unavoidable to make resolution testable." It was avoidable: scope resolution and the escalation rule are pure functions over ports (`IPupilArmOfRecordLookup`, `IResultSetArmLookup`) and plain data (`PrivilegeGrant`), fully unit-testable with fakes, and the HTTP-level 401/403/200 states are integration-tested against a test-only auth scheme + a controllable `IEffectivePrivilegeProvider`/`IAuthorizationAuditSink` — no database schema needed. This keeps TASK-0003's future `admin_account` entity from being pre-empted or duplicated.
  - `IEffectivePrivilegeProvider` (returns empty — deny-by-default), `IAuthorizationAuditSink` (logs, doesn't persist), `IPupilArmOfRecordLookup`/`IResultSetArmLookup` (throw — no route uses them yet) are registered SEAMS in `SchoolManagement.Infrastructure/Authorization/`, each documented as such, each with a `TODO(TASK-0002)` where persistence should eventually land. The real role/assignment module and the audit log module (spec 6.1.12) replace them.
- 2026-08-26 **`/api/v1/reference/whoami` changed from implicitly-protected to `.AllowAnonymous()`.** Before TASK-0002's boot-time privilege-declaration guard, this endpoint deliberately declared no authorisation metadata at all, relying purely on the deny-by-default fallback policy, to prove that policy protects a route nobody remembered to secure. The guard now makes exactly that pattern — protected-by-bare-fallback-only, no explicit declaration — a STARTUP failure for every route (spec 9.2), so the endpoint could no longer exist in its old form. Per the task card's own instruction for this exact collision ("mark it explicitly anonymous rather than inventing a privilege for it"), it is now anonymous; it still reports whatever identity a future auth mechanism attaches. The deny-by-default guarantee itself is now proven statically (`PrivilegeDeclarationGuardTests`, a route with neither declaration fails to register) and at runtime for anything unmapped (`AnUnknownRoute_Returns401NotFound_BecauseOfTheFallbackPolicy`, unchanged). **Worth a human glance** — it is a behaviour change on an existing, previously-tested endpoint, even though the response shape did not change.
- 2026-08-26 New reference-slice endpoint `GET /api/v1/reference/arms/{armId}/secure` (privilege `arm.view`, scope `ScopeParameterKind.Arm`) added to `ReferenceEndpoints.cs` purely as a worked example of `.RequirePrivilege(...)` and as the fixture the integration tests exercise for anonymous/wrong-privilege/out-of-scope/in-scope. Same category as `/whoami` and `/records` — reference scaffolding, not a product surface — and documented as such in its own XML doc comments.
- 2026-08-26 `backend/scripts/ci.ps1`'s coverage-threshold gate had a latent bug: `Get-Content $trx.FullName -Raw` (positional path) breaks when a `.trx` filename contains a literal `[1]` disambiguation suffix — PowerShell's provider resolves unbracketed `[...]` as a wildcard character class. TASK-0002 hit this once a fourth trx-producing test run existed. Fixed with `-LiteralPath`. Unrelated to authorisation; fixed because it blocked getting a clean gate run at all.

- 2026-08-26 TASK-0002 reviewed by the orchestrator against §6 and moved back to `review`, not closed. `ScopeResolver` implements spec 4.2.1's four rules exactly and refuses a client-supplied arm id for pupil-scoped routes as 9.2 requires; `PrivilegeDeclarationGuard` is deliberately STRICTER than the deny-by-default fallback — a route relying on the fallback alone still fails the guard, because the fallback is a runtime backstop, not a declaration. Both are the right calls. What blocks closure is evidence, not design: the 38 integration tests that cover anonymous / wrong-privilege / out-of-scope / in-scope were never executed. Accepting the card on unit and architecture tests alone would accept the exact failure mode `backend-ci.yml` was written to prevent, and which ASSUMPTIONS.md §4 shows already caught three real defects that no unit test could.

- 2026-08-26 **TASK-0006: frontend client regenerated against contract `d229f8042c8c826e0b561deb31139e1fb332217e72e7c6d79d2eb66ecd658a79`** (unchanged from the hash already recorded in `## Contract` above — TASK-0002's promoted contract, first consumed here). `frontend/src/api/schema.d.ts` regenerated via `npm run generate:api`; `npm run check:api-drift` clean. The pre-regeneration diff matched exactly the documented delta: new `GET /api/v1/reference/arms/{armId}/secure` path + `SecureArmResponse` schema + `GetSecureArm` operation (401/403/429), and a new 429 on `whoami`. Contract-derived MSW handlers (`src/test/msw/openapi-handlers.ts`) picked up the new operation with no hand edit — the derivation walks the committed contract directly, no per-operation code exists to update. `ApiError` normalisation in `src/lib/http/http-error.ts` already mapped `403 -> 'forbidden'` before this card (nothing wire-specific about 403 needed adding); the contract now documents explicitly what the transport already handled generically. `npm run verify` green: typecheck, oxlint zero warnings, 124/124 tests, build. No dependency added; `package.json`/`package-lock.json` untouched by this card (their existing diff in the tree predates it, from TASK-0004's already-recorded dependency refresh). One non-blocking finding for a future card: the new `SecureArmResponse` schema has no `example` in the contract (every other schema does), so its derived MSW default handler returns 200 with an empty body rather than a shaped one — fine today since no test exercises the endpoint, but whichever card first writes tests against `/reference/arms/{armId}/secure` will need a `server.use()` override or a backend-side `example` added to that schema. Status left `review`, not `done` — orchestrator closes per this repo's convention (see TASK-0004).

- 2026-08-26 **TASK-0006 reopened and closed again: frontend client regenerated against contract `228ae57fd80c22f7e0511a665efce5d4722bb3280c66f5e3f4c96fff53795a65`**, superseding the `d229f804…` hash the previous entry recorded. Cause: the backend re-promoted after fixing the missing `SecureArmResponse` example (see the `~~SecureArmResponse~~ FIXED` Known-drift entry above). Confirmed drift red first; the diff was exactly the fix implied — `SecureArmResponse` gained an object-level `example` (`{"armId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40"}`) and `armId` gained a property-level `example` of the same UUID — nothing else in the generated `schema.d.ts` moved. Ran `npm run generate:api`; `npm run check:api-drift` clean afterward. `npm run verify` green: typecheck, oxlint zero warnings, 124/124 tests, build.
  Verified the specific finding from the first pass: the contract-derived MSW handler for `GET /api/v1/reference/arms/{armId}/secure` now resolves a real body. Traced `openapi-handlers.ts`'s `exampleFor()` against the new contract directly (it follows the response schema's `$ref` to `components.schemas.SecureArmResponse.example`) and confirmed it now returns `{"armId":"0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40"}` instead of `undefined` — no derivation defect; it was reading the right place all along, the example just wasn't there yet. No source-mismatch found.
  Same constraints held: no dependency added, `package.json`/`package-lock.json` untouched by this pass, `src/lib/auth/` untouched, no UI shipped. Status left `review` — orchestrator closes.

- 2026-08-26 **Weekly report Parent's Comment resolved** (`25-open-conflicts-to-resolve.md` item 4, the last open conflict that changed what gets built). Confirmed as option A with an explicit addition: the class teacher transcribes what the parent wrote on the paper copy, **and the field is OPTIONAL — never required**. A teacher may leave any day's Parent's Comment empty, and an empty comment must never block saving a day panel, submitting a weekly report, or publishing one. No validation rule, no completeness gate, no warning banner treats it as missing data, because on paper most days genuinely have no parent comment. Option B is explicitly NOT built: there is no parent-facing write endpoint, the portal stays read-only, and the anonymous-write moderation / length-limit / abuse questions the conflicts file raises therefore do not arise. If option B is ever wanted it needs its own module and its own sign-off, per that file.
  NOTE: `product-specification/25-open-conflicts-to-resolve.md` still records item 4 as "Still open" in both its item body and its Current status table. The spec files are the human's authoritative document and the orchestrator has not edited them; this ledger entry is the decision of record until they are reconciled.

- 2026-08-26 **TASK-0009 implemented, moved to `review`.** The eight document-property assertions
  (`EverySchema_HasAnExample` and its seven neighbours, exact list in the card) moved verbatim from
  `SchoolManagement.IntegrationTests/OpenApiContractTests.cs` (deleted — nothing else in that file needed
  a database) to a new `SchoolManagement.ArchitectureTests/OpenApiContractTests.cs`, reading
  `backend/artifacts/openapi/SchoolManagement.Api.json` directly via a new `OpenApiDocument` static
  loader instead of starting the app under `WebApplicationFactory`. `backend/scripts/ci.ps1` gained one
  new gate, "Generate OpenAPI document (no database)", between Format and Tests, so that artefact exists
  before `dotnet test` runs; the existing "OpenAPI contract drift" gate no longer regenerates it a second
  time, it reuses that same file — still exactly one call site of `generate-openapi.ps1` in the script.
  `.github/workflows/backend-ci.yml`'s skip-derivation guard (`total - passed - failed` per trx) needed no
  change — verified, not touched, still root-owned.
  Both proof criteria demonstrated live on this machine (no `POSTGRES_TEST_CONNECTION`, no Docker/Podman):
  the 8 tests ran and passed (0 skipped) reading the no-database artefact; with `SecureArmResponse`'s
  example temporarily removed from `OpenApiExamples.cs` and the document regenerated WITHOUT `-Promote`,
  `EverySchema_HasAnExample` failed with the expected message, then passed again once restored — the
  source file was diff-checked back to byte-identical before rebuilding. `-Promote` was never run while
  the example was missing. Committed contract hash confirmed unchanged at
  `228ae57fd80c22f7e0511a665efce5d4722bb3280c66f5e3f4c96fff53795a65` before, during, and after. Full
  `ci.ps1 -Configuration Release` run (no database) green apart from the pre-existing SSH.NET
  "Vulnerable dependencies" gate (TASK-0007's, untouched) — `ArchitectureTests` 24/24 passed (16
  pre-existing + these 8), `IntegrationTests` 0/30 all skipped as designed (was 38 before this card; the
  8 moved tests account for the difference, everything else there still genuinely needs a database and
  was checked individually, not moved by file), `UnitTests` 124/124 passed. Full command output and
  per-test names are in the card's Log. Left for the orchestrator to raise if wanted: making the
  remaining integration tests fail-rather-than-skip when no database is reachable — explicitly out of
  scope for this card, arguably TASK-0008's territory.

- 2026-08-26 **OpenAPI generation is not deterministic between incremental and clean builds.** Reproduced by the orchestrator while verifying TASK-0009: from unchanged source, an incremental `generate-openapi.ps1` produced a document MISSING `SecureArmResponse`'s object-level example (25807 bytes, sha `6a5d51fe`), while a clean rebuild of the Api project produced the correct document byte-identical to the committed contract (25138 bytes, sha `228ae57f`). The script prints `==> Generated:` either way, so it reports success without indicating whether it actually regenerated. Source, committed contract, drift gate and tests were all correct throughout — only the generator was wrong. Owned by TASK-0010. Until it lands, treat an unexpected drift failure as possibly stale-build noise, re-check with a clean rebuild before believing it, and never run `-Promote` from a build not verified clean.

## Known drift
- 2026-08-08 Substantial backend implementation (solution, tests, CI scripts, first contract promotion) landed without a corresponding task card or backend-first handoff sequence (§4.3) — done directly in a separate human-driven session rather than a dispatched backend-dev agent. No task card owns this work retroactively. Accepted as the initial scaffold; going forward, changes route through `.agent/tasks/TASK-####.md`.
- 2026-08-26 The frontend scaffold (Vite/React/TS, tokens, primitives, transport, auth seam, 120 tests) likewise landed with no task card, in a 2026-08-03 session. Documented in `frontend/HANDOFF.md`. Accepted as scaffold. TASK-0001 audits both scaffolds retroactively so that acceptance is evidence-based rather than assumed.
- 2026-08-26 `frontend/src/lib/auth/` implements bearer-token auth, which the 2026-08-26 sign-off went against. It is now known-wrong code sitting in the tree. TASK-0003 replaces it with the cookie model and removes it rather than leaving it dormant. Until that card lands, no feature may build on the bearer seam.
- 2026-08-26 `.agent/AUDIT.md` is stale — written 2026-07-27 against empty directories. Superseded by TASK-0001.
- 2026-08-26 `frontend/src/screens/scaffold-status/` is a token and primitive demo, not a feature. Delete when the first real screen lands.
- 2026-08-26 `backend/src/**/obj/` build output is tracked in the working tree. Confirm `.gitignore` coverage during TASK-0001.

- 2026-08-26 Two version bumps inside the 2026-08-26 dependency refresh are worth watching rather than assuming benign. `oxlint` 1.76.0 -> 1.80.0 is a four-minor jump, so the "zero warnings" gate is now measured against a rule set the scaffold was not written against — it passes today, but a future minor may surface new errors that look like a regression and are not. `@base-ui/react` 1.6.0 -> 1.7.0 moves the library every UI primitive sits on, and `frontend/HANDOFF.md` documents a Base UI `Select` trigger behaviour that `select.test.tsx` pins deliberately; that test still passes, which is the intended early-warning and should stay that way.
- 2026-08-26 **`./backend/scripts/ci.ps1`'s "Vulnerable dependencies" gate is red** — `SSH.NET 2025.1.0` (transitive via `Testcontainers.PostgreSql 4.13.0` -> `Docker.DotNet`, only used for a remote-Docker SSH exec path this project never takes) carries a High-severity advisory (GHSA-q939-rpr3-3284). Confirmed pre-existing and unrelated to TASK-0002 — no `.csproj`/`Directory.Packages.props` changed. Every other TASK-0002 gate is green; this one is not, and was not fixed because a `Testcontainers.PostgreSql` bump or an `SSH.NET` version pin is a dependency decision with its own blast radius on the integration-test harness, outside a card scoped to authorisation. Full writeup and two remediation options in `backend/docs/ASSUMPTIONS.md` §3.8. Needs an owning task card or an explicit accepted-risk sign-off — not decided here.
- 2026-08-26 TASK-0002 intentionally left `IPupilArmOfRecordLookup` and `IResultSetArmLookup` (Application abstractions, Infrastructure registers throwing stand-ins) unimplemented — no pupil/enrolment or results module exists yet, so `ScopeParameterKind.Pupil`/`ScopeParameterKind.ResultSet` are declared but no route uses them. Whichever card first adds a pupil- or result-set-scoped route must implement the matching lookup against real data before that route can work; until then those two ports are dead code by design, not a defect.

- 2026-08-26 The reference slice is now in the committed contract in three places (`/api/v1/reference/ping|records|whoami`) plus TASK-0002's `/api/v1/reference/arms/{armId}/secure`. All four are scaffolding, not product, and must be deleted together once real endpoints exist — they are currently the only proof the privilege substrate and the codegen pipeline work end to end, so deleting them earlier would remove that proof. Whichever card ships the first real protected endpoint owns the deletion.
- 2026-08-26 ~~`SecureArmResponse` is the only schema in the contract with no `example`~~ — **FIXED.** TASK-0002 was reopened per §4.5: the orchestrator ran the full gate against a real PostgreSQL and `OpenApiContractTests.EverySchema_HasAnExample` failed for `SecureArmResponse` (independently corroborated by the frontend agent noticing the same schema's MSW handler answering empty, per this entry's original text). Root cause: that test lives in `IntegrationTests`, so it never ran in the backend-dev session that first shipped the endpoint — no database was reachable there, and the 38 integration tests skipped loudly instead of failing, which is correct behaviour for a missing database but meant the gap in the *contract itself* went undetected until a database was actually available. Added the missing entry to `SchoolManagement.Api.OpenApi.OpenApiExamples`, re-promoted — contract hash is now `228ae57f...` (see `## Contract`), superseding the hash TASK-0002 first reported. The frontend must regenerate its client against the new hash.
- 2026-08-26 **Process lesson from the above, orchestrator's read, backend-dev agrees with the reasoning and was asked to record but not act on it:** `EverySchema_HasAnExample` (and its neighbours in `OpenApiContractTests`) assert a property of the *generated document* and need no database — they fetch it from a live `WebApplicationFactory` host purely as a vehicle for a value that `scripts/generate-openapi.ps1` already produces without one. As an `IntegrationTests`-only check, the assertion is invisible on any machine/session without a reachable Postgres, which is exactly how this defect escaped. Worth a small card: move (or add an architecture-test-project equivalent of) the document-shape assertions — `EverySchema_HasAnExample`, `EverySchema_HasADescription`, `EveryOperation_HasASummaryDescriptionAndOperationId`, `EveryOperation_DocumentsItsErrorResponses`, `Document_HasApiLevelDocumentation`, `EveryDateTimeProperty_HasAnExample` — to read the build-generated `artifacts/openapi/SchoolManagement.Api.json` (or the committed `contracts/openapi.json`) directly as JSON, the same source `ci.ps1`'s drift gate already regenerates without a database. `Document_DescribesTheReferenceEndpoints` and `Document_UsesConcreteVersionSegmentsNotRouteTemplates` could move with them for the same reason. Not done here — orchestrator asked for the opinion, not the change, and it is a change to shared test infrastructure a card should scope properly rather than a rider on TASK-0002's reopen.

- 2026-08-26 **The staging and dev-test databases share one role and one password.** Both Neon connection strings supplied on 2026-08-26 use `neondb_owner` with the same password against the same host, differing only in database name (`schoolmanagement` vs `schoolmanagement_tests`). So the credential handed to the test harness IS the staging credential: anyone or anything holding `POSTGRES_TEST_CONNECTION` — a developer machine, a CI secret, a leaked log — can reach staging data with it. The test fixture also truncates every table EF maps, and only the database name in a connection string stands between that and staging. Recommend a separate least-privilege role scoped to `schoolmanagement_tests` only. Raised with the human 2026-08-26; not yet decided.

## Open questions
1. ~~Greenfield build?~~ RESOLVED 2026-08-26 — yes; spec supplied, both scaffolds stand.
2. ~~Initialise git?~~ RESOLVED 2026-08-08.
3. ~~Frontend package manager?~~ RESOLVED 2026-08-26 — npm.
4. ~~Docker unavailable?~~ RESOLVED 2026-08-08 — hosted Neon Postgres for integration tests.
5. **Production database target** still undecided. Not blocking until deployment.
6. ~~Client generator?~~ RESOLVED 2026-08-26 — openapi-typescript, types only.
7. ~~Product scope?~~ RESOLVED 2026-08-26 — `product-specification/`.
8. ~~Auth carrier?~~ RESOLVED 2026-08-26 — HttpOnly cookie session + CSRF token, human
   sign-off. See `## Auth decision`. TASK-0003 unblocked.
9. ~~Weekly report Parent's Comment?~~ RESOLVED 2026-08-26 by the human. See `## Decisions`.
