# Project State

Last reconciled: 2026-08-08 by orchestrator

## Layout
backend:  ./backend — real solution `SchoolManagement.slnx`. Api / Application / Domain /
          Infrastructure under src/; Unit, Integration, and Architecture test projects under tests/.
          Reference vertical slice implemented (`/api/v1/reference/ping`).
frontend: ./frontend — still EMPTY. No package.json, no source.
contract generator: Microsoft.Extensions.ApiDescription.Server/10.0.10 (built-in ASP.NET Core OpenAPI
          generator), invoked via `backend/scripts/generate-openapi.ps1 -Promote`. That script is the
          ONLY sanctioned way `contracts/openapi.json` / `CONTRACT.lock` change (backend-dev writes them
          only as this script's output — never by hand).
client generator:   not chosen — see Open questions (frontend work hasn't started)
repo:     git-initialised. branch `main`, remote `origin` →
          https://github.com/maxcotech/school-management-proj.git, up to date with origin/main.

## Toolchain present on this machine
dotnet SDK: 10.0.100 (only SDK installed)
node:       v22.21.0
npm:        10.9.4
yarn:       1.22.22
pnpm:       NOT INSTALLED
docker:     NOT INSTALLED — Testcontainers unavailable, but integration tests were run against a
            hosted Neon PostgreSQL instance via `POSTGRES_TEST_CONNECTION` instead. 132 tests,
            0 failed, 0 skipped, 86.43% line / 63.59% branch coverage. See
            backend/docs/ASSUMPTIONS.md §3.1 — this resolves Open question #4 below for CI purposes;
            Docker remains optional.
psql:       NOT INSTALLED  ← no local Postgres client
git:        2.51.1.windows.1, repo now initialised (see Layout)

## Gate commands
backend:  `./backend/scripts/ci.ps1` runs every gate in order (restore, build -warnaserror,
          dotnet format --verify-no-changes, tests+coverage, coverage floor, vulnerable-package scan,
          gitleaks secret scan, OpenAPI contract drift). CI wrapper: `.github/workflows/backend-ci.yml`
          (moved here from `backend/ci/github-actions-backend.yml` 2026-08-08 — see Decisions).
frontend: not yet defined — no package.json

## Contract
openapi.json sha256: 40e14997379ecb7edb5acd25f4bb8493e30cb9dc445c9ffde67a05ae67db6eb6
regenerated: 2026-08-08, via `backend/scripts/generate-openapi.ps1 -Promote`
generator: Microsoft.Extensions.ApiDescription.Server/10.0.10, dotnet SDK 10.0.100
api version: v1 (confirmed — `/api/v1/reference/ping` is live)

## Auth decision
mechanism: NOT DECIDED — §5 requires an explicit choice before any auth work.
           §5 recommends HttpOnly cookie session for a first-party web app.
refresh: n/a
logout:  n/a

## In flight
| Task | Title | Owner | Status |
|---|---|---|---|
| (none) | — | — | — |

## Decisions
- 2026-07-27 Scaffolding created (CLAUDE.md, .agent/, .claude/agents/, contracts/) — orchestrator owns these per §11; needed before any dispatch is possible.
- 2026-07-27 contracts/openapi.json deliberately NOT created as a placeholder — §3 says it is never written by hand, and a stub would make every §4.4 drift check meaningless. It appears at the first backend build.
- 2026-08-08 Git initialised (resolves Open question #2). Remote `origin` points at https://github.com/maxcotech/school-management-proj.git.
- 2026-08-08 Backend reference vertical slice implemented and its OpenAPI contract promoted for the first time — `contracts/openapi.json` + `CONTRACT.lock` now real, hash verified to match (see Contract). Integration tests verified against a hosted Neon Postgres rather than Testcontainers — accepted per backend/docs/ASSUMPTIONS.md §3.1; revisit if a team Docker/Postgres standard is set later.
- 2026-08-08 `.claude/settings.json` created: Edit/Write on `contracts/openapi.json` and `contracts/CONTRACT.lock` only are allowed without prompting, so a backend session running `generate-openapi.ps1 -Promote` isn't blocked. Nothing else under `contracts/**` or `.agent/**` is exempted — hand-edits there still prompt. This is a permission convenience, not a substitute for the §4.4 drift check.
- 2026-08-08 CI workflow activated: moved `backend/ci/github-actions-backend.yml` → `.github/workflows/backend-ci.yml` (root-level CI is orchestrator-owned per §11; the file was staged by backend-dev with an explicit note for the orchestrator to relocate it). Not yet pushed/verified against a live Actions run.

## Known drift
- 2026-08-08 Substantial backend implementation (solution, tests, CI scripts, first contract promotion) landed without a corresponding task card or backend-first handoff sequence (§4.3) — done directly in a separate human-driven session rather than a dispatched backend-dev agent. No task card owns this work retroactively. Accepted as the initial scaffold; going forward, changes should route through `.agent/tasks/TASK-####.md` so STATE.md doesn't fall behind reality again.

## Open questions
1. **Is this a greenfield build?** Frontend is still empty; backend has begun. Confirm
   the intent is still to build the frontend from scratch under this spec, and get
   product scope (see #7) before writing its task cards.
2. ~~Initialise git?~~ RESOLVED 2026-08-08 — repo is git-initialised with a GitHub remote.
3. **Package manager for the frontend.** §2 lists pnpm|npm|yarn. pnpm is not installed;
   npm 10.9.4 and yarn 1.22.22 are. Recommend npm unless there is a reason otherwise.
4. ~~Docker is unavailable.~~ PARTIALLY RESOLVED 2026-08-08 — integration tests run against a
   hosted Neon Postgres via `POSTGRES_TEST_CONNECTION`; CI uses a service-container Postgres
   instead. Docker/Testcontainers remains unused; revisit only if that becomes a problem.
5. ~~No Postgres instance identified.~~ RESOLVED for testing 2026-08-08 — hosted Neon instance in
   use for integration tests. Production database target is still undecided.
6. **Client generator choice.** §3 offers openapi-typescript + openapi-fetch, NSwag, or
   Kiota. No existing choice to inherit — moot until frontend work starts. Recommend
   openapi-typescript + openapi-fetch — it is the lightest, and §7 already mandates
   TanStack Query for state, so a heavier generated client layer would duplicate it.
7. **Product scope.** Repo is named `school-management-proj`. No requirements exist in
   the repo. Task cards cannot be written without knowing the domain (students,
   enrolment, attendance, grading, fees, timetabling — which of these, and for whom).
8. **Auth mechanism** (§5) still not decided — blocks any protected-endpoint work beyond
   the anonymous reference slice.
