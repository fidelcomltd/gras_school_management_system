# Project State

Last reconciled: 2026-07-27 by orchestrator

## Layout
backend:  ./backend  — EMPTY. No solution, no projects, no manifests.
frontend: ./frontend — EMPTY. No package.json, no source.
contract generator: not chosen — no backend exists to generate from
client generator:   not chosen — see Open questions
repo:     NOT a git repository (`git status` → fatal: not a git repository)

## Toolchain present on this machine
dotnet SDK: 10.0.100 (only SDK installed)
node:       v22.21.0
npm:        10.9.4
yarn:       1.22.22
pnpm:       NOT INSTALLED
docker:     NOT INSTALLED  ← blocks Testcontainers (§6 Testing)
psql:       NOT INSTALLED  ← no local Postgres client
git:        2.51.1.windows.1 (installed, but repo not initialised)

## Gate commands
backend:  not yet defined — no project to build
frontend: not yet defined — no package.json

## Contract
openapi.json sha256: NOT GENERATED — no backend to emit it
regenerated: never
api version: v1 (planned, per §3 Versioning)

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

## Open questions
1. **Is this a greenfield build?** Both `backend/` and `frontend/` are empty. CLAUDE.md
   describes governance for two existing codebases. Confirm the intent is to build
   both from scratch under this spec.
2. **Initialise git?** §8 (Conventional Commits) and §4.4 (diff against the *committed*
   contract) both assume version control. Nothing can be enforced without it.
   Orchestrator did not run `git init` unasked — structural, and remote/.gitignore
   choices are the human's.
3. **Package manager for the frontend.** §2 lists pnpm|npm|yarn. pnpm is not installed;
   npm 10.9.4 and yarn 1.22.22 are. Recommend npm unless there is a reason otherwise.
4. **Docker is unavailable.** §6 mandates integration tests via Testcontainers against
   a real Postgres and forbids the in-memory provider. Options: install Docker Desktop,
   point tests at an external Postgres instance, or accept documented drift. This blocks
   the `dotnet test` gate for integration tests, not unit tests.
5. **No Postgres instance identified.** §6 mandates EF Core + PostgreSQL. Need a
   connection target for local development.
6. **Client generator choice.** §3 offers openapi-typescript + openapi-fetch, NSwag, or
   Kiota. No existing choice to inherit. Recommend openapi-typescript + openapi-fetch —
   it is the lightest, and §7 already mandates TanStack Query for state, so a heavier
   generated client layer would duplicate it.
7. **Product scope.** Repo is named `school-management-proj`. No requirements exist in
   the repo. Task cards cannot be written without knowing the domain (students,
   enrolment, attendance, grading, fees, timetabling — which of these, and for whom).

## Known drift
- (none — nothing implemented yet)
