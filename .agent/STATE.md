# Project State

Last reconciled: 2026-09-05 by orchestrator · size budget 12 KB, see `## Reading this file`

## Product

**GRAS School Management System** — Golden Royal Ark School (nursery + primary, Nigerian
three-term session). Back office for configuration, pupil register, admission, marks, results and
pin generation, plus one public page where a parent redeems a registration number + access pin to
read a published result. **Parents have no accounts.**

Authoritative spec: `product-specification/` rev 3.1, entry point `index.md`. The school's `.docx`
is rev 1 and is **not** authoritative. Out-of-scope list: `00-document-overview.md` 3.2 — read it
there (§13).

## Layout
backend:  ./backend — solution `SchoolManagement.slnx`. Api / Application / Domain /
          Infrastructure under src/; Unit, Integration and Architecture tests under tests/.
          First real domain is `Domain/Auth/` (TASK-0003); `/api/v1/reference/*` and `/health/*`
          remain scaffolding. Conventions: `backend/AGENTS.md`. Deviations: `docs/ASSUMPTIONS.md`.
frontend: ./frontend — Vite 8 / React 19 / TypeScript 6, package `gra-school-portal`, npm
          (lockfile committed). Design tokens, Base UI, axios transport in `src/lib/http/`,
          TanStack Query, Zustand, MSW, react-hook-form + zod, Playwright. Top-level: `src/api/
          app/ components/ config/ features/ lib/ screens/ stores/ test/`. `features/<feature>/`
          is the documented home for screens (`CONVENTIONS.md` §4), still EMPTY — TASK-0021 is
          its first user. `screens/` deprecated; `shared/` deliberately absent (2026-09-05).
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
dotnet SDK 10.0.100 · node v22.21.0 · npm 10.9.4 · yarn 1.22.22 · pnpm ABSENT ·
docker ABSENT (integration tests use hosted Neon Postgres via `POSTGRES_TEST_CONNECTION`;
CI uses a service-container Postgres) · psql ABSENT · git 2.51.1.windows.1

## Gate commands
backend:  `./backend/scripts/ci.ps1` — ten gates cheapest-first, STOPS at the first failure.
          `Failed > 0`/`Skipped > 0` exit non-zero (the latter unless `-AllowSkipped`);
          `-NoFailFast` runs all, as CI does. Ends in a fixed `SUMMARY` block — paste that alone
          to satisfy §9. Run it via `scripts/local-env.ps1`, which resolves the DB and splats
          `-GateArgs` through. CI: `.github/workflows/backend-ci.yml`.
frontend: `npm run verify` (typecheck, lint, test, build) plus `npm run check:api-drift`
          (§4.4 check 2, deliberately NOT folded into `verify`) plus `npm run test:e2e`
          (Playwright, own CI job). oxlint, not ESLint. `npm run generate:api` regenerates the
          client when the contract moves. CI: `.github/workflows/frontend-ci.yml`.

## Contract
openapi.json sha256: 3b518bd951b386b270ca35b1398c515a3c9bad3fe0eba4f6c89382ede0565185
          `X-CSRF-Token` is a required header parameter on exactly the four mutating auth
          operations, emitted by the same call that wires enforcement.
regenerated: 2026-09-05 (generator + SDK under `## Layout`)
api version: v1 · nine paths: `/api/v1/auth/{csrf,sign-in,sign-out,me,refresh,password}` and
          `/api/v1/reference/{ping,records,arms/{armId}/secure}`. `/health/*` are
          `.ExcludeFromDescription()`, not in it (`ASSUMPTIONS.md` §2.9). `/reference/*` is
          scaffolding. History (incl. `whoami` removal): `decisions/2026-Q3.md`.

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

Open cards only. Closed: TASK-0001, 0002, 0003, 0004, 0006-0018, 0020 — closure notes and reopen history in
[decisions/2026-Q3.md](decisions/2026-Q3.md).

| Task | Title | Owner | Status |
|---|---|---|---|
| TASK-0022 | Cross-platform gate self-test + fixture tracking | backend-dev | done, UNCOMMITTED |
| TASK-0019 | Idempotency substrate, then admin account management | backend-dev | queued |
| TASK-0021 | Cookie auth seam and the sign-in screen | frontend-dev | queued |
| TASK-0005 | School settings: identity, registration number, config versioning | backend-dev | queued |

Full sequence and cards not yet written: [ROADMAP.md](ROADMAP.md).

## Decisions

**Standing decisions and live lessons.** Closed-card records collapse at the bottom; full text is
[decisions/2026-Q3.md](decisions/2026-Q3.md) and the card's `## Log`. Approved contract deltas:
`decisions/2026-Q3-contract-deltas.md`.

- 2026-09-04 **TASK-0013 Option B (human sign-off): idempotency deferred**, trigger armed — see `## Known drift`. Lesson: XML doc comments are contract content, so a docs-only backend diff still needs a hash check.
- 2026-09-04 **STANDING LESSON (0010/0011/0015/0016, again in 0022): a check that cannot be shown to fail is not a check.** Break what it guards, watch it go red, then accept it. Corollaries: fixtures must match the real artefact; a comment claiming coverage is not coverage.
- 2026-09-05 **TASK-0003 SPLIT** into 0003 (auth), 0019 (idempotency + admin accounts), 0020 (frontend §7), 0021 (cookie seam). Idempotency trigger fires on 0019; 0003's bootstrap stays off the wire as a CLI command.
- 2026-09-05 **TASK-0003 CLOSED** — six `/api/v1/auth/*` endpoints, `3b518bd9…`, 245/245 green. **Reopened four times, always the same shape: the feature worked and its test passed while the RULE went unenforced.** Full note in `decisions/2026-Q3.md`.
- 2026-09-05 **TASK-0022: three stacked CI defects; the third means the gate self-test had NEVER been able to run in CI** — fixtures were `*.trx`-ignored, never committed. Found only by raising PR #9. Lesson: reviewing the DIFF, not the agent's report, found it. Full note in `decisions/2026-Q3.md`.
- 2026-09-05 **Open question 11 RESOLVED (human): follow §7** — `src/features/<feature>/`, react-hook-form+zod, Playwright; no bulk rename, `src/shared/` waits. Done by TASK-0020. Full note in `decisions/2026-Q3.md`.

**Closed-card records** — archive-only, `grep` the ID in `decisions/2026-Q3.md`: 2026-08-27 four
decisions (gitleaks, gate honesty, DB-credential split, TASK-0001 close) · TASK-0012 problem-schema
extension members · TASK-0014 §7 audit S4-S7 · TASK-0017 secret scan restored · TASK-0018
`-GateArgs` splat · TASK-0020 §7 structure/form/E2E mandates adopted.

## Known drift

Split by whether it can bite a dispatch. **Live triggers** are below in full — check them before
every dispatch. The rest are accepted deviations with no trigger: named and dated here, full text
in [drift/2026-Q3.md](drift/2026-Q3.md), found by date.

**Live triggers**

- 2026-09-04 **No `Idempotency-Key` mechanism; mutating endpoints do not accept the header** — §6 and spec §9.8.2 require it. Deferred (Option B, human sign-off). **Trigger: the first card implementing ANY retry-duplicable mutation builds it first** — §9.8.2's four operations are examples, not the whole list. **Assigned to TASK-0019**; re-check before TASK-0005. `ASSUMPTIONS.md` §2.14.
- 2026-09-05 **Three accepted auth exposures (TASK-0003):** DP key ring unpersisted, so a restart or second replica 403s outstanding CSRF cookies — **trigger: deployment (Q5)**; bootstrap CLI prints the temp password to stdout; `PersistLockoutStateAsync` opens a DbContext only when the account exists (timing asymmetry). Owner `backend-dev`.
- 2026-09-05 **`UseRateLimiter()` runs before `UseAuthentication()`** (`Program.cs:298` vs `:300`), so every rate-limit partition falls back to remote IP and the per-user branch is dead code; admins behind one NAT share the sensitive bucket. **Trigger: TASK-0019.** Owner `backend-dev`.
- 2026-09-05 **`scripts/local-env.ps1` is UNTRACKED** (per-dev copy of `local-env.template.ps1`); this machine's predates TASK-0018 and still splats `GateArgs` positionally — the bug TASK-0018 fixed *in the template only*. Nothing detects copy-vs-template drift. **Trigger: any dispatch running gates via the wrapper** — call `ci.ps1` directly. Needs a card.
- 2026-09-05 **`gate-summary.tests.ps1:101,:108` are near-unfalsifiable** — `-match` substring passes even against a mangled path; only in-process `-eq` caught the TASK-0022 mutation. **Trigger: next card touching that suite.**
- 2026-09-04 **The DEFAULT rate-limit policy has no 429 test.** Health covered by TASK-0015, sensitive by TASK-0003. Owner `backend-dev`, no trigger.
- 2026-09-04 **The Api→Infrastructure arch test exempts `StartupEnvironmentGuard` as well as `Program.cs`** (it reads `DatabaseOptions` at `StartAsync`). **Trigger: a THIRD exemption must argue for itself or the type gets a port** — the rule must not erode one name at a time.
- 2026-08-26 **`frontend/src/lib/auth/` is bearer-token auth, against the cookie sign-off.** Known-wrong. **Trigger: TASK-0021**, which deletes it. Owner `frontend-dev`.
- 2026-08-26 `frontend/src/screens/scaffold-status/` is a demo. **Trigger: TASK-0021 deletes it**, retiring `src/screens/` and rewriting the smoke spec on its heading.
- 2026-08-26 **Staging and dev-test DBs share one role and password. Trigger: deployment** (Open question 5). Owner human.
- 2026-08-27 **Validation is a mediator pipeline behaviour, not §6's endpoint filter. Trigger: ratify or revert** — unowned. `ASSUMPTIONS.md` §2.2.

- 2026-09-05 **`SameSite=Lax` on both auth cookies assumes frontend and API share a registrable domain** (TASK-0003 delta). Cross-site needs `SameSite=None` and a different CSRF posture — not a one-line tweak. **Trigger: the deployment decision (Open question 5).** Owner human + `backend-dev`.

**Accepted, no trigger** — 8 entries, archive-only; `grep` the date in `drift/2026-Q3.md`:
2026-08-08 backend landed uncarded · 2026-08-26 ×4 (dep bumps; TASK-0002 Application ports;
reference slice in the contract ×4 places; a process lesson not acted on) · 2026-08-27 ×2
(`Docker.DotNet.Enhanced` unverified; TASK-0011 Family B gap) · 2026-09-04 `Format` before `Build`.

Ten resolved or struck entries live in the archive only, including 2026-08-27 `src/shared/`
(resolved 2026-09-05).

## Open questions

Live only. The nine resolved questions are in [decisions/2026-Q3.md](decisions/2026-Q3.md).

5. **Production database target** undecided. Not blocking until deployment.
11. RESOLVED 2026-09-05 — follow §7. See `## Decisions`, and `decisions/2026-Q3.md`.

## Reading this file

**Budget: 12 KB, bytes.** `wc -c` before appending. Over it, archive full text to `decisions/` or
`drift/`, leave a one-line index — never delete. Rules: CLAUDE.md §4.1, §13.
