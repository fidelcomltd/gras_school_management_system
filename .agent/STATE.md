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
          in `src/lib/http/`, TanStack Query, Zustand, MSW, 120 tests green. `src/api/` and
          `src/features/` are EMPTY and reserved. See `frontend/HANDOFF.md`.
contract generator: Microsoft.Extensions.ApiDescription.Server/10.0.10, invoked via
          `backend/scripts/generate-openapi.ps1 -Promote`. That script is the ONLY sanctioned
          way `contracts/openapi.json` / `CONTRACT.lock` change.
client generator:   openapi-typescript (types only) into `frontend/src/api/`. Decided
          2026-08-26, see Decisions. Not yet installed — TASK-0004.
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
frontend: `npm run verify` (typecheck, lint, test, build). oxlint, not ESLint.
          No CI workflow yet — orchestrator-owned, follows TASK-0004.

## Contract
openapi.json sha256: 40e14997379ecb7edb5acd25f4bb8493e30cb9dc445c9ffde67a05ae67db6eb6
regenerated: 2026-08-08 · generator Microsoft.Extensions.ApiDescription.Server/10.0.10, SDK 10.0.100
api version: v1 · surface: `/api/v1/reference/ping|records|whoami`, `/health/live|ready`

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
| TASK-0002 | Privilege register and authorisation enforcement | backend-dev | queued |
| TASK-0003 | Admin accounts, authentication and session management | backend-dev | queued |
| TASK-0004 | Wire the OpenAPI client generator and typed API layer | frontend-dev | queued |
| TASK-0005 | School settings: identity, registration number, config versioning | backend-dev | queued |

Full sequence beyond these five: `.agent/ROADMAP.md`.

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

## Known drift
- 2026-08-08 Substantial backend implementation (solution, tests, CI scripts, first contract promotion) landed without a corresponding task card or backend-first handoff sequence (§4.3) — done directly in a separate human-driven session rather than a dispatched backend-dev agent. No task card owns this work retroactively. Accepted as the initial scaffold; going forward, changes route through `.agent/tasks/TASK-####.md`.
- 2026-08-26 The frontend scaffold (Vite/React/TS, tokens, primitives, transport, auth seam, 120 tests) likewise landed with no task card, in a 2026-08-03 session. Documented in `frontend/HANDOFF.md`. Accepted as scaffold. TASK-0001 audits both scaffolds retroactively so that acceptance is evidence-based rather than assumed.
- 2026-08-26 `frontend/src/lib/auth/` implements bearer-token auth, which the 2026-08-26 sign-off went against. It is now known-wrong code sitting in the tree. TASK-0003 replaces it with the cookie model and removes it rather than leaving it dormant. Until that card lands, no feature may build on the bearer seam.
- 2026-08-26 `.agent/AUDIT.md` is stale — written 2026-07-27 against empty directories. Superseded by TASK-0001.
- 2026-08-26 `frontend/src/screens/scaffold-status/` is a token and primitive demo, not a feature. Delete when the first real screen lands.
- 2026-08-26 `backend/src/**/obj/` build output is tracked in the working tree. Confirm `.gitignore` coverage during TASK-0001.

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
9. **Weekly report Parent's Comment** — `25-open-conflicts-to-resolve.md` item 4. The
   provisional resolution (class teacher transcribes, portal read-only) is what will be built
   unless the school says otherwise. Needed before the module 6.10 card, not before.
