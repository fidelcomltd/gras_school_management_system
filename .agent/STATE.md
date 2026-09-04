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
          is still EMPTY and reserved. Real top-level paths, recorded per §2 after TASK-0001:
          `src/api/` `src/app/` `src/components/` `src/config/` `src/features/` `src/lib/`
          `src/screens/` `src/stores/` `src/test/`. There is **no `src/shared/`** despite §7 and
          §2 naming it — see Known drift 2026-08-27 and Open question 11. See `frontend/HANDOFF.md`
          and `frontend/src/api/README.md`.
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
          `/health/live` and `/health/ready` are `.ExcludeFromDescription()` and are NOT in the document (`ASSUMPTIONS.md` §2.9) — the document holds exactly these four paths; corrected 2026-08-27 by TASK-0001. `/reference/arms/{armId}/secure` is new (TASK-0002) — reference-slice
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
| TASK-0001 | Re-audit backend and frontend scaffolds against §6/§7 | reviewer | done |
| TASK-0002 | Privilege register and authorisation enforcement | backend-dev | done |
| TASK-0003 | Admin accounts, authentication and session management | backend-dev | queued |
| TASK-0004 | Wire the OpenAPI client generator and typed API layer | frontend-dev | done |
| TASK-0005 | School settings: identity, registration number, config versioning | backend-dev | queued |
| TASK-0006 | Regenerate the frontend client against the TASK-0002 contract | frontend-dev | done |
| TASK-0007 | Clear the SSH.NET High advisory blocking the vuln gate | backend-dev | done |
| TASK-0008 | Give the integration-test connection string a durable local home | backend-dev | done |
| TASK-0009 | Move the document-property contract tests off the database | backend-dev | done |
| TASK-0010 | Make OpenAPI document generation deterministic | backend-dev | done |
| TASK-0011 | Make the secret-scan gate pass for the right reason | backend-dev | done |
| TASK-0012 | Declare the problem-detail extension members in the contract | backend-dev, then frontend-dev | queued |
| TASK-0013 | Decide and implement idempotency for retryable mutations | backend-dev | queued |
| TASK-0014 | Frontend scaffold conformance fixes from the §7 audit | frontend-dev | queued |
| TASK-0015 | Backend scaffold conformance fixes from the §6 audit | backend-dev | queued |

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

- 2026-08-27 **TASK-0010 closed.** OpenAPI generation could report `==> Generated:` over a document it had not written: the MSBuild target tracks `obj/…OpenApiFiles.cache`, not the JSON. Fixed by deleting the cache. The orchestrator's original "incremental vs clean builds diverge" framing was **overstated** — ordinary pulls and branch switches self-heal; the only confirmed trigger is a document modified outside the target.
- 2026-08-27 TASK-0007 sent to `review`: SSH.NET High advisory cleared by Option 1 (`Testcontainers.PostgreSql` 4.13.0 → 4.14.0), first option tried. Vulnerable-dependency gate verified green (offline check, no database needed). Full `ci.ps1` run otherwise SKIPPED — no Docker, no `POSTGRES_TEST_CONNECTION` in that session — so integration tests (30 skipped) and the coverage floor are unverified this pass, per the standing lesson that a skipping run must not be reported as a pass. Needs the database re-run before closing.
- 2026-08-27 TASK-0008 sent to `review`. `backend/scripts/local-env.template.ps1` (committed) resolves `POSTGRES_TEST_CONNECTION` from four sources in order — already-set env var → `$env:GRAS_LOCAL_ENV_FILE` → `$HOME/.gras/pg-test.txt` (recommended, outside the repo) → an inline slot in the developer's own copy, left empty in the template per the `appsettings.Development.template.json` precedent (no realistic-looking fake credential) — then runs a gate (`-Gate`, default `ci.ps1`). `backend/scripts/local-env.ps1` is the real, git-ignored copy; it never embeds a credential as static text since the inline slot stays empty and it resolves via source 3. `git check-ignore -v` proved the real script is ignored (exact-path rule, no wildcard) and the template is not, per the card's own warning that this is the easiest thing to get wrong. `gitleaks` (8.30.1) installed via `winget`, no admin; `ci.ps1`'s Secret scan gate now actually executes. Demonstrated the template doesn't trip it and a filled-in copy (obviously-fake demo value, never staged) does. Full detail and gate output: TASK-0008's Log.
- 2026-08-27 **gitleaks running for real, for the first time anywhere (TASK-0008), surfaces 9 pre-existing non-secret findings** it had never actually been run against before (it was uninstalled locally, so `ci.ps1` always warned-and-skipped; CI installs it but had never been exercised against this history either as far as this session could verify). All 9 are outside TASK-0008's file scope — `backend/README.md`, `appsettings.Development.template.json`, `DesignTimeDbContextFactory.cs`, `backend/ci/github-actions-backend.yml`, `.agent/STATE.md`, `contracts/CONTRACT.lock` — and `gitleaks detect` scans git history by default, so editing current file content would not clear it regardless. All are documented placeholders or content hashes, not real secrets. Recorded in `backend/docs/ASSUMPTIONS.md` §3.9 with three options (targeted `.gitleaks.toml` allowlist entries / scope the scan to `--no-git` / accept as a permanent characteristic of a history-scanning tool), none decided here — needs an owning task card or explicit sign-off, the same shape as the SSH.NET drift entry above.

- 2026-08-27 **TASK-0007 and TASK-0008 both reviewed and both held at `blocked`, neither closed.** Their own deliverables are complete and were verified independently by the orchestrator (one-line dependency bump with the vuln scan re-run clean across seven projects; `git check-ignore -v` proving the real script ignored and the template not). What stops both is external: the test role lacks `CREATE` on schema `public` (Open question 10, needs a human `GRANT`), so the integration suite fails 30/30 instead of passing. §10 does not let a card close on a red gate, and the standing lesson here is that a suite which does not pass is not a pass — this time it fails loudly rather than skipping quietly, which is an improvement in visibility, not in evidence.
- 2026-08-27 **TASK-0011 created, not dispatched** — owns the nine secret-scan findings TASK-0008 revealed. All nine inspected by the orchestrator: five are placeholder connection strings (`YOUR_PASSWORD`, `...`, `<pw>`) tripping an over-broad rule that had never executed before today, four are the SHA-256 contract hash read as an API key. No real credential among them. Four of the nine exist only in git history, so no edit to current files can clear them. Awaiting a human call on whether narrowing the rule with stopwords is acceptable, since TASK-0008's own text calls an allowlist "a permanent hole".
- 2026-08-27 **TASK-0011 implemented, sent to `review`.** Human approved narrowing the rule (no baseline). While building the fix, found `postgres-connection-string-with-password`'s regex has one capture group `(host|server)` (meant only to accept both keywords) — with no `secretGroup` set, gitleaks uses "first non-empty capture group" as `finding.Secret`, so every finding's Secret was literally the word `"Host"`/`"Server"`, never the connection string. Since `stopwords` (and the default allowlist target) always check against `Secret`, a Secret-scoped `stopwords` list — the card's first-listed option — **could not have worked for this rule**; confirmed by reading gitleaks' own source, not assumed. Same reason the file's *pre-existing* global allowlist entries (`ci_user`, `ci_password`, `Username=<user>;Password=<pw>`, `Password=\.\.\.`, present since 2026-08-03) had never actually suppressed anything for this rule either. Fix used the card's other option instead: `[[allowlists]]` blocks with `targetRules` (Family A: `postgres-connection-string-with-password`, `regexTarget = "match"`, one regex per literal placeholder token; Family B: `generic-api-key`, `paths` anchored to `contracts/CONTRACT.lock` and `.agent/STATE.md`), both rule-scoped not path-scoped for A, chosen path-scope for B over forking a default rule we don't own. The file's one legacy `[allowlist]` table was converted to `[[allowlists]]` (identical behaviour) because gitleaks refuses to load both forms mixed and `targetRules` needs the plural form. Verified against a real leak: a realistic fake connection string (fake host, non-placeholder password) written to a scratch file outside the repo still trips the rule (`leaks found: 1`), then deleted; the real gate (`gitleaks detect --source . --config .gitleaks.toml --redact --no-banner`, from `backend/`) then reports `6 commits scanned` / `no leaks found` / exit 0. Full `./backend/scripts/local-env.ps1` run: Restore/Build/Format/OpenAPI-gen/Coverage/Vulnerable-deps/**Secret scan**/OpenAPI-drift all green (contract hash unchanged, `228ae57f...`); unit 124/124 and architecture 24/24 pass; integration 30/30 FAIL, every one `Npgsql 42501: permission denied for schema public` — Open question 10, external, unrelated to this change, not fixed here. Detail and full pasted output: TASK-0011's Log.
- 2026-08-27 **TASK-0007, TASK-0008 and TASK-0011 all closed on one verified live run.** `ALL GATES PASSED` with the integration suite genuinely executing — `Failed: 0, Passed: 30, Skipped: 0` — coverage enforced on a complete run (77.18% line / 58.61% branch), the secret scan actually scanning (`6 commits scanned` -> `no leaks found`), vulnerable-package scan clean across seven projects, and contract drift clean at `228ae57f`. All four §4.4 drift checks run by the orchestrator, including the two frontend-side ones `ci.ps1` does not cover (client drift: no drift; no raw `fetch`/`axios` outside the generated client). Held all three at `blocked` through three separate agent reports rather than closing any on a red Tests gate — the gate went green because a database privilege was granted, not because the bar moved.
- 2026-08-27 The gate suite is now honest end to end for the first time: before today the vulnerable-dependency gate was red (TASK-0007), the secret scan printed `PASS` while scanning nothing (TASK-0008 installed gitleaks; TASK-0011 fixed what that revealed), and the integration suite could skip silently while degrading the coverage floor with it (TASK-0008's `local-env.ps1` removes the re-typing that caused it). Three gates that could previously report green without evidence now cannot.
- 2026-08-27 **Test-database credential split confirmed deliberate** — a dedicated least-privilege role, not `neondb_owner`, now backs `POSTGRES_TEST_CONNECTION`. Closes the 2026-08-26 finding that the test credential WAS the staging credential. It cost one cycle to discover, because the new role lacked `CREATE` on schema `public` and the EF fixture runs migrations — worth knowing for the next environment: a test role needs DDL rights on its own database, and Postgres 15+ does not grant them by default.
- 2026-08-27 **TASK-0001 closed. Both scaffolds audited rule by rule and both materially exceed spec** — `.agent/AUDIT.md` rewritten from scratch, superseding the 2026-07-27 version written against empty directories. Run as two parallel read-only audits (no `reviewer` agent is configured in the roster, so both ran as `general-purpose` under a write-nothing brief). Backend §6: enforcement is mechanical rather than aspirational — 24 architecture tests, `AnalysisLevel latest-All` with warnings as errors, CA1848/CA1849/CA2016 promoted to errors, deny-by-default authorization with a boot-time route guard. Frontend §7: zero `any`, zero `@ts-ignore`, zero non-null `!`, and 35 of oxlint's 36 `jsx-a11y` rules firing as errors (measured, not assumed from the config comment). **Two blockers**, both verified by the orchestrator before carding: the problem schemas forbid the `errorCode`/`traceId` every error response carries and the defect has already reached the generated client (TASK-0012, blocks TASK-0003's frontend half); and `Idempotency-Key` appears nowhere despite §6 and spec 9.8.2 (TASK-0013, decide-now not fix-now). Eight should-fixes carded as TASK-0014 (frontend) and TASK-0015 (backend). Three ledger claims corrected as factually wrong, including "obj/ is tracked" — it is not, and never was.
## Known drift
- 2026-08-08 Substantial backend implementation (solution, tests, CI scripts, first contract promotion) landed without a corresponding task card or backend-first handoff sequence (§4.3) — done directly in a separate human-driven session rather than a dispatched backend-dev agent. No task card owns this work retroactively. Accepted as the initial scaffold; going forward, changes route through `.agent/tasks/TASK-####.md`. **2026-08-27: acceptance is now EVIDENCE-BASED, not assumed** — TASK-0001 audited this scaffold rule by rule against §6 and found it materially exceeds spec, with two blockers carded (TASK-0012, TASK-0013) and three should-fixes (TASK-0015). See `.agent/AUDIT.md`.
- 2026-08-26 The frontend scaffold (Vite/React/TS, tokens, primitives, transport, auth seam, 120 tests) likewise landed with no task card, in a 2026-08-03 session. Documented in `frontend/HANDOFF.md`. Accepted as scaffold. TASK-0001 audits both scaffolds retroactively so that acceptance is evidence-based rather than assumed. **2026-08-27: audited.** TASK-0001 found ZERO blockers under §7, gates fully green (`npm run verify` exit 0, 124 tests, zero lint diagnostics), and five should-fixes carded as TASK-0014. Test count is 124, not the 120 this entry recorded. See `.agent/AUDIT.md`.
- 2026-08-26 `frontend/src/lib/auth/` implements bearer-token auth, which the 2026-08-26 sign-off went against. It is now known-wrong code sitting in the tree. TASK-0003 replaces it with the cookie model and removes it rather than leaving it dormant. Until that card lands, no feature may build on the bearer seam.
- 2026-08-26 ~~`.agent/AUDIT.md` is stale — written 2026-07-27 against empty directories.~~ **RESOLVED 2026-08-27** — rewritten from TASK-0001's two audits. It now records every §6/§7 rule as pass/fail/N-A with file:line evidence, two blockers (TASK-0012, TASK-0013), eight should-fixes (TASK-0014, TASK-0015) and the nice-to-haves deliberately left uncarded.
- 2026-08-26 `frontend/src/screens/scaffold-status/` is a token and primitive demo, not a feature. Delete when the first real screen lands.
- 2026-08-26 ~~`backend/src/**/obj/` build output is tracked in the working tree.~~ **WRONG, struck 2026-08-27 by TASK-0001.** Nothing under any `obj/` is tracked: `git ls-files | grep -ci "/obj/"` returns 0, and `backend/.gitignore:3:[Oo]bj/` ignores them. Seven `obj/` directories exist ON DISK as ordinary build output; the entry conflated *present* with *tracked*. Same conflation applied to `frontend/dist/`, also correctly ignored (`frontend/.gitignore:11`) with 0 tracked files. Verified by both auditors and by the orchestrator.

- 2026-08-26 Two version bumps inside the 2026-08-26 dependency refresh are worth watching rather than assuming benign. `oxlint` 1.76.0 -> 1.80.0 is a four-minor jump, so the "zero warnings" gate is now measured against a rule set the scaffold was not written against — it passes today, but a future minor may surface new errors that look like a regression and are not. `@base-ui/react` 1.6.0 -> 1.7.0 moves the library every UI primitive sits on, and `frontend/HANDOFF.md` documents a Base UI `Select` trigger behaviour that `select.test.tsx` pins deliberately; that test still passes, which is the intended early-warning and should stay that way.
- 2026-08-26 ~~**`./backend/scripts/ci.ps1`'s "Vulnerable dependencies" gate is red** — `SSH.NET 2025.1.0` (transitive via `Testcontainers.PostgreSql 4.13.0` -> `Docker.DotNet`, only used for a remote-Docker SSH exec path this project never takes) carries a High-severity advisory (GHSA-q939-rpr3-3284). Confirmed pre-existing and unrelated to TASK-0002 — no `.csproj`/`Directory.Packages.props` changed. Every other TASK-0002 gate is green; this one is not, and was not fixed because a `Testcontainers.PostgreSql` bump or an `SSH.NET` version pin is a dependency decision with its own blast radius on the integration-test harness, outside a card scoped to authorisation. Full writeup and two remediation options in `backend/docs/ASSUMPTIONS.md` §3.8. Needs an owning task card or an explicit accepted-risk sign-off — not decided here.~~ **RESOLVED 2026-08-27, TASK-0007 Option 1**: bumped `Testcontainers.PostgreSql` 4.13.0 → 4.14.0 in `Directory.Packages.props`, which resolves `Docker.DotNet.Enhanced` 4.3.3 → `SSH.NET` 2026.0.0 (past the patched line). `dotnet list package --vulnerable --include-transitive` now reports zero vulnerable packages across all seven projects; the gate passes ("No known vulnerable packages"). Options 2/3 not needed. See `backend/docs/ASSUMPTIONS.md` §3.8 and TASK-0007's Log for the caveat: verified with Docker absent and no `POSTGRES_TEST_CONNECTION`, so 4.14.0's behaviour against a real container/database is still unverified — pending the database re-run TASK-0008/the orchestrator is arranging.
- 2026-08-26 TASK-0002 intentionally left `IPupilArmOfRecordLookup` and `IResultSetArmLookup` (Application abstractions, Infrastructure registers throwing stand-ins) unimplemented — no pupil/enrolment or results module exists yet, so `ScopeParameterKind.Pupil`/`ScopeParameterKind.ResultSet` are declared but no route uses them. Whichever card first adds a pupil- or result-set-scoped route must implement the matching lookup against real data before that route can work; until then those two ports are dead code by design, not a defect.

- 2026-08-26 The reference slice is now in the committed contract in three places (`/api/v1/reference/ping|records|whoami`) plus TASK-0002's `/api/v1/reference/arms/{armId}/secure`. All four are scaffolding, not product, and must be deleted together once real endpoints exist — they are currently the only proof the privilege substrate and the codegen pipeline work end to end, so deleting them earlier would remove that proof. Whichever card ships the first real protected endpoint owns the deletion.
- 2026-08-26 ~~`SecureArmResponse` is the only schema in the contract with no `example`~~ — **FIXED.** TASK-0002 was reopened per §4.5: the orchestrator ran the full gate against a real PostgreSQL and `OpenApiContractTests.EverySchema_HasAnExample` failed for `SecureArmResponse` (independently corroborated by the frontend agent noticing the same schema's MSW handler answering empty, per this entry's original text). Root cause: that test lives in `IntegrationTests`, so it never ran in the backend-dev session that first shipped the endpoint — no database was reachable there, and the 38 integration tests skipped loudly instead of failing, which is correct behaviour for a missing database but meant the gap in the *contract itself* went undetected until a database was actually available. Added the missing entry to `SchoolManagement.Api.OpenApi.OpenApiExamples`, re-promoted — contract hash is now `228ae57f...` (see `## Contract`), superseding the hash TASK-0002 first reported. The frontend must regenerate its client against the new hash.
- 2026-08-26 **Process lesson from the above, orchestrator's read, backend-dev agrees with the reasoning and was asked to record but not act on it:** `EverySchema_HasAnExample` (and its neighbours in `OpenApiContractTests`) assert a property of the *generated document* and need no database — they fetch it from a live `WebApplicationFactory` host purely as a vehicle for a value that `scripts/generate-openapi.ps1` already produces without one. As an `IntegrationTests`-only check, the assertion is invisible on any machine/session without a reachable Postgres, which is exactly how this defect escaped. Worth a small card: move (or add an architecture-test-project equivalent of) the document-shape assertions — `EverySchema_HasAnExample`, `EverySchema_HasADescription`, `EveryOperation_HasASummaryDescriptionAndOperationId`, `EveryOperation_DocumentsItsErrorResponses`, `Document_HasApiLevelDocumentation`, `EveryDateTimeProperty_HasAnExample` — to read the build-generated `artifacts/openapi/SchoolManagement.Api.json` (or the committed `contracts/openapi.json`) directly as JSON, the same source `ci.ps1`'s drift gate already regenerates without a database. `Document_DescribesTheReferenceEndpoints` and `Document_UsesConcreteVersionSegmentsNotRouteTemplates` could move with them for the same reason. Not done here — orchestrator asked for the opinion, not the change, and it is a change to shared test infrastructure a card should scope properly rather than a rider on TASK-0002's reopen.

- 2026-08-26 **The staging and dev-test databases share one role and one password.** Both Neon connection strings supplied on 2026-08-26 use `neondb_owner` with the same password against the same host, differing only in database name (`schoolmanagement` vs `schoolmanagement_tests`). So the credential handed to the test harness IS the staging credential: anyone or anything holding `POSTGRES_TEST_CONNECTION` — a developer machine, a CI secret, a leaked log — can reach staging data with it. The test fixture also truncates every table EF maps, and only the database name in a connection string stands between that and staging. Recommend a separate least-privilege role scoped to `schoolmanagement_tests` only. Raised with the human 2026-08-26; not yet decided. **RESOLVED 2026-08-27 — confirmed deliberate by the human.** The role behind `POSTGRES_TEST_CONNECTION` is a dedicated least-privilege role, deliberately split off from the staging owner, scoped to `schoolmanagement_tests`; it is not `neondb_owner` and shares neither its name nor its password (verified by the orchestrator without printing either). The recommendation in this entry was carried out. Residual, unverified and not chased: unless `CONNECT` was explicitly revoked, a Postgres role can by default open a connection to other databases in the same cluster — it would hold no table privileges on staging, but `REVOKE CONNECT ON DATABASE schoolmanagement FROM <role>` would close that door properly.

- 2026-08-27 **The local `Secret scan` gate is red for reasons unrelated to any pending card's own changes**, discovered by TASK-0008 installing `gitleaks` for the first time (it was previously absent locally; `ci.ps1` warned and skipped). 9 findings, all pre-existing, all non-secret (documented placeholders / SHA-256 content hashes), spanning files no in-flight card is scoped to touch. Detail and options: `backend/docs/ASSUMPTIONS.md` §3.9. No owning card yet. **RESOLVED 2026-08-27 by TASK-0011** — rule narrowed, gate verified executing (`6 commits scanned` -> `no leaks found`) and verified still catching a realistic planted credential.
- 2026-08-27 **`Docker.DotNet.Enhanced` is unverified in practice, and cannot be verified on this machine.** TASK-0007's bump to `Testcontainers.PostgreSql` 4.14.0 did not merely raise a version — it swapped the transitive dependency family `Docker.DotNet` -> `Docker.DotNet.Enhanced` 4.3.3 (seven packages), which is what carries the patched `SSH.NET` 2026.0.0. Test-only, never ships. But when `POSTGRES_TEST_CONNECTION` is set the fixture uses the external database and never instantiates Testcontainers, and Docker is absent here — so no run on this machine exercises the new dependency at all. Accepted: the gate is green for the right reason, and the swapped code path is the remote-Docker SSH transport this project does not take. The first developer with Docker available should run the suite with `POSTGRES_TEST_CONNECTION` UNSET and confirm the container path still works. No owning card; recorded so the gap is known rather than assumed away.

- 2026-08-27 **`ci.ps1`'s coverage gate recognises skipped tests but not FAILED ones.** On the 2026-08-27 database run it printed `PASS: Coverage threshold (60%)` at 60.48% line on a run where 30 integration tests FAILED — so the number was measured against a partial run and means nothing, yet it read as a pass. The existing skip-detector (which correctly stands the floor down when anything skips, per `ASSUMPTIONS.md` §2.10) has no equivalent for failures. Low severity because a failing suite already turns the Tests gate red, so this cannot manufacture a false overall green — but the printed line is misleading and the same family of defect as the skip problem that cost this project two false "done" reports. No owning card yet; do not fold it into TASK-0011, which is about the secret scanner.

- 2026-08-27 **TASK-0011's Family B allowlist leaves one narrow gap, measured rather than assumed.** Scoping `generic-api-key` off `contracts/CONTRACT.lock` and `.agent/STATE.md` means a bare high-entropy string with no provider signature, pasted into one of those two files, would not be caught by THAT rule. Orchestrator probed it: a planted GitHub PAT in the allowlisted path was still caught (`RuleID: github-pat`), because the allowlist is rule-scoped so every other rule still applies there — a planted AWS key id was not. Accepted: the exposure is two files, one of them generated, and the alternative (forking the default `generic-api-key` regex to teach it about hex digests) carries upstream-drift risk on every gitleaks upgrade. A tighter form exists if wanted — allowlist by a regex matching only `openapi.json[.sha256] = <64 hex>` inside those paths, keeping `generic-api-key` live for everything else in them. Not done; no card.

- 2026-08-27 **Validation runs as a mediator pipeline behaviour, not the endpoint filter §6 specifies.** Documented in `backend/docs/ASSUMPTIONS.md` §2.2 since the scaffold landed but never recorded here, so a future agent reading only the ledger would see §6 and the code disagree with no reason given. Surfaced by TASK-0001. The scaffold's argument is sound and the orchestrator agrees with it — a behaviour cannot be forgotten on a new endpoint, a filter can, and `PipelineOrderTests` asserts it runs before the handler. Two things remain open and are the human's, not an agent's: the assumptions doc explicitly asks for this to be "either ratified or reverted", and it has been neither. Recorded as accepted drift until then.
- 2026-08-27 **The frontend tree has no `src/shared/`, which both §7 and CLAUDE.md §2 name as the home for shared code.** Real layout is `src/components/`, `src/stores/`, `src/screens/`, `src/lib/`, `src/api/`, `src/config/`, `src/test/` plus the reserved-and-empty `src/features/`. Defensible for a design-system-first scaffold, and now recorded in `## Layout` as §2 requires. The sharper edge is `src/screens/`: §7 puts screens under `src/features/<feature>/`, so where the FIRST real screen goes is currently undecided — see `## Open questions` 11. Surfaced by TASK-0001; explicitly out of scope for TASK-0014, which must not restructure the tree.

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
10. **The integration-test role lacks `CREATE` on schema `public` in `schoolmanagement_tests`.**
    A dedicated least-privilege role now backs `POSTGRES_TEST_CONNECTION` — it is **not**
    `neondb_owner`, which if intentional resolves the shared staging/test credential concern
    recorded in `## Known drift` (2026-08-26). The EF fixture runs migrations, so it needs DDL
    rights on that database: all 30 integration tests now fail at collection-fixture init with
    `Npgsql 42501: permission denied for schema public` (Postgres 15+ no longer grants schema
    `public` CREATE to PUBLIC). Fix is one `GRANT` run by an owner role against
    `schoolmanagement_tests`; SQL prepared for the human outside the repo with the role name
    substituted. **Blocks TASK-0007's close and TASK-0008's verification.** Raised 2026-08-27.
    **RESOLVED 2026-08-27** — the human ran the grant against `schoolmanagement_tests`. Verified by
    a full `ci.ps1` run: integration suite `Failed: 0, Passed: 30, Skipped: 0`, coverage enforced on
    a complete run at 77.18% line / 58.61% branch, ALL GATES PASSED. Unblocked TASK-0007, TASK-0008
    and TASK-0011, all three closed on that run.
11. **Where do frontend screens live, and are three 2026-08-03 deviations still intended?**
    Raised by TASK-0001. (a) §7 puts screens under `src/features/<feature>/`; the scaffold has a
    top-level `src/screens/` and no `src/shared/`, so the first real screen has no agreed home.
    (b) `react-hook-form` is absent, yet §7 mandates it plus a zod resolver for forms. (c) Playwright
    is absent, yet §7 mandates it for auth, the primary create path and one failure path. All three
    are documented as deliberate in `frontend/HANDOFF.md` — but that was written **2026-08-03, before
    the product specification landed**, so they were chosen without knowing what gets built. Each
    becomes binding the moment a screen, a form or a flow exists, i.e. at TASK-0003's frontend half.
    Not blocking today. Needs a human call, not an agent's.
