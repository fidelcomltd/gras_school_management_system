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
| TASK-0010 | Make OpenAPI document generation deterministic | backend-dev | done |

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

One line each, per §4.1. Verbose originals and full reasoning: [decisions/2026-Q3.md](decisions/2026-Q3.md)
and the `## Log` of the task card named in each entry.

> Bootstrap decisions (2026-07-27 to 2026-08-08) are archived in the same file. Still in force.

- 2026-08-26 Product scope resolved (OQ7): `product-specification/` rev 3.1 is authoritative; cards cite spec sections rather than restating them, so a spec revision cannot silently invalidate a card.
- 2026-08-26 Package manager **npm** (OQ3) — already in use, lockfile committed, pnpm absent.
- 2026-08-26 Client generator **openapi-typescript, types only** (OQ6) — the frontend already owns a working axios transport with auth interceptors; a runtime client would duplicate it and force a rewrite of that seam. TASK-0004.
- 2026-08-26 Sequencing departs from `index.md` once: authorisation infrastructure (TASK-0002) precedes settings, because spec 9.2 makes an undeclared route a boot failure and settings endpoints would otherwise be written against an enforcement layer that does not exist.
- 2026-08-26 **Auth carrier: HttpOnly cookie + CSRF**, human sign-off per §5. Rejected bearer-in-memory: readable by any XSS, and dies on reload so it must either sign the user out or be persisted — the exposure it was avoiding. Token model unchanged (spec 9.1 opaque, hashed, rotated). TASK-0003.
- 2026-08-26 Spec conflicts items 1/2/5 resolved by the school; 3/6/7/8/9 built to their provisional resolutions. No subagent may amend one — raise it in Open questions.
- 2026-08-26 TASK-0004 closed: contract pipeline wired end to end — generate, drift-check, typed helper over the existing transport, contract-derived MSW handlers, HTTP-boundary test.
- 2026-08-26 Frontend dependency refresh accepted **after the fact**: closing TASK-0004 regenerated the lockfile wholesale, sweeping 73 versions beyond scope. Accepted — all inside declared caret ranges, gates green, `npm audit` 0 vulnerabilities where the old tree carried a high-severity `nanoid` advisory. Process was wrong, result was not; must land as its own commit (§8).
- 2026-08-26 Frontend CI workflow written (orchestrator-owned per §11): path-filtered, `npm ci` never `npm install`, Node pinned to 22.21.0 to match the dev machine, drift check as its own final step so a stale client reads differently from a broken frontend.
- 2026-08-26 `/api/v1/reference/whoami` made explicitly `.AllowAnonymous()` rather than inventing a privilege for it, per TASK-0002's own instruction. Response shape unchanged.
- 2026-08-26 New reference-slice endpoint `GET /api/v1/reference/arms/{armId}/secure` proves the privilege substrate end to end. Scaffolding, not product — deleted with the rest of the reference slice when real endpoints land.
- 2026-08-26 Fixed a latent `ci.ps1` coverage-gate bug found in passing (`-LiteralPath`), unrelated to authorisation but blocking any clean gate run.
- 2026-08-26 **TASK-0002 closed** on a verified live run: 178 tests, Skipped 0, coverage 87.00%/65.58%, drift clean. Closed with ONE gate knowingly red (SSH.NET, TASK-0007) as an explicit recorded exception, not an unnoticed one.
- 2026-08-26 TASK-0002 was first reported done twice on runs where its integration suite SKIPPED; run for real it failed on a defect already in the committed contract. **A skipped suite is not a passing suite**, and the coverage floor deliberately stands down when anything skipped — so a skipping run degrades two gates at once while looking green. TASK-0009 removed the checks that never needed a database from that blast radius.
- 2026-08-26 TASK-0006 closed twice — once per contract move (`d229f804`, then `228ae57f`). The client regenerates in one command; the drift gate caught staleness on its own both times.
- 2026-08-26 **Weekly Parent's Comment resolved** (conflict item 4, last one affecting what gets built): teacher transcribes, portal read-only, and the field is **optional — never required**. No validation, completeness gate or warning may treat an empty comment as missing data. Option B (parent-facing write) explicitly not built.
- 2026-08-27 **TASK-0010 closed.** OpenAPI generation could report `==> Generated:` over a document it had not written: the MSBuild target tracks `obj/…OpenApiFiles.cache`, not the JSON. Fixed by deleting the cache. The orchestrator's original "incremental vs clean builds diverge" framing was **overstated** — ordinary pulls and branch switches self-heal; the only confirmed trigger is a document modified outside the target.

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
