# Project State

Last reconciled: 2026-09-05 by orchestrator · size budget 12 KB, see `## Reading this file`

## Product

**GRAS School Management System** — Golden Royal Ark School (nursery + primary, Nigerian
three-term session). Back office for configuration, pupil register, admission, marks, result
computation/approval/publication, weekly pastoral reports and pin generation; plus one public
page where a parent enters a registration number and an access pin to read or download a
published result. Parents have no accounts.

Authoritative spec: `product-specification/` (revision 3.1), `index.md` is the entry point. The
school's `.docx` is revision 1 and is **not** authoritative. The out-of-scope list is
`00-document-overview.md` 3.2 — read it there, it is not restated here (§13).

## Layout
backend:  ./backend — solution `SchoolManagement.slnx`. Api / Application / Domain /
          Infrastructure under src/; Unit, Integration and Architecture tests under tests/.
          Reference vertical slice only (`/api/v1/reference/*`, `/health/*`). No domain code yet.
          Conventions: `backend/AGENTS.md`. Deviations: `backend/docs/ASSUMPTIONS.md`.
frontend: ./frontend — Vite 8 / React 19 / TypeScript 6, package `gra-school-portal`, npm
          (lockfile committed). Design tokens, Base UI primitives, axios transport in
          `src/lib/http/`, TanStack Query, Zustand, MSW, 130 tests green. `src/features/` is
          EMPTY and reserved. Real top-level paths per §2: `src/api/ src/app/ src/components/
          src/config/ src/features/ src/lib/ src/screens/ src/stores/ src/test/` — there is
          **no `src/shared/`** despite §7 and §2 (Open question 11). Conventions:
          `frontend/CONVENTIONS.md`, `frontend/HANDOFF.md`, `frontend/src/api/README.md`.
contract generator: Microsoft.Extensions.ApiDescription.Server/10.0.10 via
          `backend/scripts/generate-openapi.ps1 -Promote` — the ONLY sanctioned way
          `contracts/openapi.json` and `CONTRACT.lock` change.
client generator:   openapi-typescript@7.13.0 (types only, pinned exact, devDependency).
          `frontend/scripts/generate-api-schema.mjs` writes `src/api/schema.d.ts`
          (`npm run generate:api`); `check-api-schema-drift.mjs` is the §4.4 check-2 gate
          (`npm run check:api-drift`). `src/api/client.ts` is the hand-written typed request
          helper over `src/lib/http/`.
repo:     git, branch `main`, origin https://github.com/maxcotech/school-management-proj.git

## Toolchain present on this machine
dotnet SDK 10.0.100 · node v22.21.0 · npm 10.9.4 · yarn 1.22.22 · pnpm ABSENT ·
docker ABSENT (integration tests use hosted Neon Postgres via `POSTGRES_TEST_CONNECTION`;
CI uses a service-container Postgres) · psql ABSENT · git 2.51.1.windows.1

## Gate commands
backend:  `./backend/scripts/ci.ps1` — ten gates cheapest-first, STOPS at the first failure:
          restore, format, build -warnaserror, generate-OpenAPI, unit+arch tests, integration
          tests, coverage floor, vulnerable packages, gitleaks, drift. `-NoFailFast` runs all
          (CI's mode); `Failed > 0`/`Skipped > 0` exit non-zero, the latter unless
          `-AllowSkipped`. Ends in a fixed `SUMMARY` block — paste it alone to satisfy §9.
          Verdict: `scripts/lib/gate-summary.ps1`, self-tested in `scripts/tests/`.
          `scripts/local-env.ps1` resolves the DB and passes `-GateArgs` switches through.
          CI: `.github/workflows/backend-ci.yml`.
frontend: `npm run verify` (typecheck, lint, test, build) plus `npm run check:api-drift`
          (§4.4 check 2, deliberately not folded into `verify`). oxlint, not ESLint.
          `npm run generate:api` regenerates the client when the contract moves.
          CI: `.github/workflows/frontend-ci.yml`.

## Contract
openapi.json sha256: b287da0394da7d25ced6f25c50da761b5135b4c0557ea64ac839fdd69ae09f2c
regenerated: 2026-09-04 · generator Microsoft.Extensions.ApiDescription.Server/10.0.10, SDK 10.0.100
api version: v1 · the document holds exactly four paths:
          `/api/v1/reference/ping|records|whoami|arms/{armId}/secure`. `/health/live` and
          `/health/ready` are `.ExcludeFromDescription()` and are NOT in it (`ASSUMPTIONS.md` §2.9).
          `/reference/arms/{armId}/secure` (TASK-0002) is test scaffolding proving the privilege
          substrate, not a product endpoint. Hash history: `decisions/2026-Q3.md`.

## Auth decision
mechanism: **HttpOnly cookie session + CSRF token.** Human sign-off 2026-08-26 per §5.
           Token format fixed by spec 9.1: opaque 32-byte CSPRNG, stored hashed server-side,
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

Open cards only. Closed: TASK-0001, 0002, 0004, 0006, 0007, 0008, 0009, 0010, 0011, 0012, 0013,
0014, 0015, 0016, 0017, 0018 — closure notes and reopen history in
[decisions/2026-Q3.md](decisions/2026-Q3.md).

| Task | Title | Owner | Status |
|---|---|---|---|
| TASK-0003 | Admin accounts, authentication and session management | backend-dev | queued |
| TASK-0005 | School settings: identity, registration number, config versioning | backend-dev | queued |

Full sequence and cards not yet written: [ROADMAP.md](ROADMAP.md).

## Decisions

Index only, newest last. **Full text:** [decisions/2026-Q3.md](decisions/2026-Q3.md) and the
`## Log` of the card each entry names. Bootstrap decisions (2026-07-27 to 2026-08-08) live there
too and are still in force.

- 2026-08-27 Four decisions (gitleaks, gate honesty, DB-credential split, TASK-0001 close) — `decisions/2026-Q3.md:156-158`.
- 2026-09-04 **Context budget restructured** — this file capped in bytes, `## Decisions`/`## Known drift` reduced to indexes, §6/§7 given one home.
- 2026-09-04 TASK-0012 CLOSED — problem schemas declare `errorCode`/`traceId`; client regenerated; an `ApiError.code` bug fixed as a consequence.
- 2026-09-04 TASK-0016 CLOSED — gates fail fast and honestly. Lesson: fixtures must reproduce the real artefact's shape, names included. `decisions/2026-Q3.md:299,319`.
- 2026-09-04 TASK-0017 CLOSED — secret scan restored via fingerprint-pinned `backend/.gitleaksignore`.
- 2026-09-04 **TASK-0013 Option B (human sign-off), CLOSED** — idempotency deferred, see the drift trigger below. Lesson: XML doc comments are contract content here, so even a docs-only backend diff needs a hash check.
- 2026-09-04 Sequencing: Phase 0b ends with TASK-0015; TASK-0003 opens the first product surface.
- 2026-09-04 TASK-0014 CLOSED — §7 audit S4-S7; no route around the generated client, and that rule is now a test. `decisions/2026-Q3.md:335`.
- 2026-09-04 TASK-0018 CLOSED — `-GateArgs` splats as a hashtable, so switches bind by name.
- 2026-09-04 **TASK-0015 CLOSED — Phase 0b complete.** §6 audit S1-S3: health rate-limit policy, `IPersistenceErrorTranslator` port, Api→Infrastructure arch test, stale-artefact check. Reopened twice in review, both times because a criterion was met in letter while the thing it protects stayed unenforced. Lesson: ask what deleting the new line would break, not whether it was written.
- 2026-09-05 **Open question 11 RESOLVED (human): follow §7.** Screens go under `src/features/<feature>/`; `react-hook-form`+zod at the first form; Playwright at the first flow — all three at TASK-0003's frontend half, which therefore splits. Proviso "only where it buys better structure" means no bulk rename of the working scaffold: `src/shared/` appears when something genuinely shared needs it, and `src/screens/` retires with `scaffold-status`. `decisions/2026-Q3.md`.

## Known drift

Split by whether it can bite a dispatch. **Live triggers** are below in full — check them before
every dispatch. Everything else is an accepted deviation with no trigger: named and dated here,
full text in [drift/2026-Q3.md](drift/2026-Q3.md) only, found by its date.

**Live triggers**

- 2026-09-04 **No `Idempotency-Key` mechanism; mutating endpoints do not accept the header** — §6 and spec §9.8.2 require it. Deferred (Option B, human sign-off). **Trigger: the first card implementing ANY retry-duplicable mutation builds it first** — §9.8.2's four operations are examples, not the whole list. Check before TASK-0003 and TASK-0005. Owner `backend-dev`. `ASSUMPTIONS.md` §2.14.
- 2026-09-04 **No test asserts a 429; the default and sensitive rate-limit policies are unverified.** `ApiTestFixture.cs:124` claimed a dedicated test existed — it did not. TASK-0015 fixed the claim and covered the health policy only. **Trigger: TASK-0003**, first real user of `SensitivePolicyName`; login rate limiting is a security control, so it lands with a rejection test or not at all. Owner `backend-dev`.
- 2026-09-04 **The Api→Infrastructure arch test exempts `StartupEnvironmentGuard` as well as `Program.cs`** — it reads `DatabaseOptions` at `StartAsync`. Accepted as startup composition, not request-path code. **Trigger: a THIRD exemption must argue for itself or the type gets a port** — the rule must not erode one name at a time. Owner `backend-dev`.
- 2026-08-26 **`frontend/src/lib/auth/` implements bearer-token auth, against the cookie sign-off.** Known-wrong code. **Trigger: TASK-0003's frontend half**, which replaces it. Owner `frontend-dev`.
- 2026-08-26 `frontend/src/screens/scaffold-status/` is a demo, not a feature. **Trigger: delete it when the first real screen lands** (TASK-0003), which also retires `src/screens/` per the 2026-09-05 decision. Owner `frontend-dev`.
- 2026-08-26 **Staging and dev-test databases share one role and one password.** Both Neon strings use the same credential. **Trigger: deployment** — see Open question 5. Owner human.
- 2026-08-27 **Validation runs as a mediator pipeline behaviour, not the endpoint filter §6 specifies.** **Trigger: ratify or revert** — the question is open and unowned; decide it before §6 is cited against a card. `backend/docs/ASSUMPTIONS.md` §2.2.

**Accepted, no trigger** — archive-only, `grep` the date in `drift/2026-Q3.md`: 2026-08-08 backend
landed uncarded (audited, TASK-0015 closed its should-fixes) · 2026-08-26 two dependency bumps
watched not assumed benign · 2026-08-26 TASK-0002's `IPupilArmOfRecordLookup`/`IResultSetArmLookup`
as Application abstractions · 2026-08-26 the reference slice sits in the committed contract in four
places · 2026-08-26 a process lesson recorded and deliberately not acted on · 2026-08-27
`Docker.DotNet.Enhanced` unverified on this machine · 2026-08-27 TASK-0011's Family B allowlist gap,
measured · 2026-09-04 `Format` runs before `Build` though it measures slower.

Ten resolved or struck entries live in the archive only, including 2026-08-27 `src/shared/`
(resolved 2026-09-05).

## Open questions

Live only. The nine resolved questions are in [decisions/2026-Q3.md](decisions/2026-Q3.md).

5. **Production database target** undecided. Not blocking until deployment.
11. RESOLVED 2026-09-05 — follow §7. See `## Decisions`, and `decisions/2026-Q3.md`.

## Reading this file

**Budget: 12 KB, in bytes.** `wc -c .agent/STATE.md` before appending. Over it, archive a section's
full text to `decisions/` or `drift/` and leave a one-line index entry — never delete. Rationale and
the rest of the rules: CLAUDE.md §4.1 and §13.
