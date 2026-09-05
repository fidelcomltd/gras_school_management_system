# Project State

Last reconciled: 2026-09-05 by orchestrator · size budget 12 KB, see `## Reading this file`

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
dotnet SDK 10.0.100 · node v22.21.0 · npm 10.9.4 · yarn 1.22.22 · pnpm ABSENT · pwsh ABSENT ·
docker ABSENT (integration tests use hosted Neon Postgres via `POSTGRES_TEST_CONNECTION`;
CI uses a service-container Postgres) · psql ABSENT · git 2.51.1.windows.1 · gitleaks 8.30.1

**PINNED BOTH WAYS (TASK-0024) — changing one side alone re-breaks CI:** `global.json`
`rollForward: latestPatch` (feature bands carry new analyzers; `latestFeature` let CI drift) ·
gitleaks **8.30.1** in `backend-ci.yml` must equal the local version · Node `22.21.0` in
`frontend-ci.yml`. CI prints `dotnet --version` so the resolved SDK is read, not deduced. · gitleaks 8.30.1
(CI matches, TASK-0024)

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
openapi.json sha256: 3b518bd951b386b270ca35b1398c515a3c9bad3fe0eba4f6c89382ede0565185
          `X-CSRF-Token` is a required header parameter on exactly the four mutating auth
          operations, emitted by the same call that wires enforcement.
regenerated: 2026-09-05 (generator + SDK under `## Layout`)
api version: v1 · 9 paths: `/auth/{csrf,sign-in,sign-out,me,refresh,password}` +
          `/reference/{ping,records,arms/{armId}/secure}`. `/health/*` excluded
          (`ASSUMPTIONS.md` §2.9); `/reference/*` is scaffolding. Client regenerated 2026-09-05
          (TASK-0025). History: `decisions/2026-Q3.md`.

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

Open cards only. Closed: TASK-0001-0004, 0006-0018, 0020, 0022-0025 — closure notes and reopen
history in [decisions/2026-Q3.md](decisions/2026-Q3.md).

| Task | Title | Owner | Status |
|---|---|---|---|
| TASK-0019 | Idempotency substrate, then admin account management | backend-dev | queued |
| TASK-0021 | Cookie auth seam and the sign-in screen | frontend-dev | queued |
| TASK-0005 | School settings: identity, registration number, config versioning | backend-dev | queued |

Full sequence and cards not yet written: [ROADMAP.md](ROADMAP.md).

## Decisions

**Standing decisions and live lessons.** Closed-card records collapse at the bottom; full text is
[decisions/2026-Q3.md](decisions/2026-Q3.md) and the card's `## Log`. Approved contract deltas:
`decisions/2026-Q3-contract-deltas.md`.

- 2026-09-04 **STANDING LESSON (0010/0011/0015/0016, again in 0022): a check that cannot be shown to fail is not a check.** Break what it guards, watch it go red, then accept it. Corollaries: fixtures must match the real artefact; a comment claiming coverage is not coverage.
- 2026-09-05 **TASK-0022: three stacked CI defects; the third means the gate self-test had NEVER been able to run in CI** — fixtures were `*.trx`-ignored, never committed. Found only by raising PR #9. Lesson: reviewing the DIFF, not the agent's report, found it. Full note in `decisions/2026-Q3.md`.
- 2026-09-05 **Open question 11 RESOLVED (human): follow §7** — `src/features/<feature>/`, react-hook-form+zod, Playwright; no bulk rename, `src/shared/` waits. Done by TASK-0020. Full note in `decisions/2026-Q3.md`.
- 2026-09-05 **STANDING LESSON (0022, 0023, 0024 ×2 — all found by PR #9): a gate is only as trustworthy as the reproducibility of its INPUTS.** Four defects, one shape — something the gates read was not committed and not version-pinned, so "green locally" and "green in CI" were never the same claim. **Before trusting a green gate, ask what it reads that is neither committed nor pinned.** Full note in `decisions/2026-Q3.md`.
- 2026-09-05 **TASK-0023/0024/0025 closed**: hermetic vitest `test.env`; SDK + gitleaks pinned both ways and CA2025/CA1873 fixed not suppressed; client regenerated. Contract unmoved. Full notes in `decisions/2026-Q3.md`.

**Closed-card records** — archive-only, `grep` the ID in `decisions/2026-Q3.md`: 0001, 0003, 0012,
0013, 0014, 0017, 0018, 0020, 0022-0025, plus 2026-08-27's four decisions (gitleaks, gate honesty,
DB-credential split, TASK-0001 close).

## Known drift

Split by whether it can bite a dispatch. **Live triggers** are below — check them before every
dispatch. The rest are accepted deviations with no trigger, dated here, full text in
`drift/2026-Q3.md`.

**Live triggers**

- 2026-09-05 **`vite build` succeeds with NO `.env` and emits a bundle that throws on boot** (inlines `VITE_*` as `undefined`), so Build goes green on something unusable. CI copies `.env.example` to mask it; nothing checks env at build time. Unowned. **Trigger: any card touching build or deployment.**
- 2026-09-05 **`RequestLoggingBehavior`'s SUCCESS path hoists elapsed-time to a local** — satisfies CA1873 without skipping the work when logging is off, unlike the cancellation branch TASK-0024 guarded. **Trigger: next card touching it.** Owner `backend-dev`.
- 2026-09-04 **No `Idempotency-Key` mechanism.** Deferred (Option B, human sign-off). **Trigger: the first card implementing ANY retry-duplicable mutation builds it first** — §9.8.2's four operations are EXAMPLES, not the list. **Assigned to TASK-0019**; re-check before TASK-0005. `ASSUMPTIONS.md` §2.14.
- 2026-09-05 **Three accepted auth exposures (TASK-0003):** unpersisted DP key ring (**trigger: deployment, Q5**); bootstrap CLI prints the temp password to stdout; `PersistLockoutStateAsync` timing asymmetry. Owner `backend-dev`. Full text: `drift/2026-Q3.md`.
- 2026-09-05 **`UseRateLimiter()` runs before `UseAuthentication()`** (`Program.cs:298` vs `:300`), so every rate-limit partition falls back to remote IP and the per-user branch is dead code; admins behind one NAT share the sensitive bucket. **Trigger: TASK-0019.** Owner `backend-dev`.
- 2026-09-05 `scripts/local-env.ps1` untracked/stale, splats `GateArgs` positionally. **Trigger: any dispatch running gates via the wrapper** — call `ci.ps1` directly. Full text: `drift/2026-Q3.md`.
- 2026-09-05 **`gate-summary.tests.ps1:101,:108` are near-unfalsifiable** — `-match` substring passes even against a mangled path; only in-process `-eq` caught the TASK-0022 mutation. **Trigger: next card touching that suite.**
- 2026-09-04 **The DEFAULT rate-limit policy has no 429 test.** Health covered by TASK-0015, sensitive by TASK-0003. Owner `backend-dev`, no trigger.
- 2026-09-04 **The Api→Infrastructure arch test exempts `StartupEnvironmentGuard` as well as `Program.cs`.** **Trigger: a THIRD exemption must argue for itself or the type gets a port** — the rule must not erode one name at a time. Full text: `drift/2026-Q3.md`.
- 2026-08-26 **Two known-wrong frontend leftovers, both TASK-0021's to DELETE:** `src/lib/auth/` is bearer-token auth against the cookie sign-off; `src/screens/scaffold-status/` is a demo whose heading the smoke spec asserts — rewrite that assertion, never weaken it. Owner `frontend-dev`.
- 2026-08-26 **Staging and dev-test DBs share one role and password. Trigger: deployment** (Open question 5). Owner human.
- 2026-08-27 **Validation is a mediator pipeline behaviour, not §6's endpoint filter. Trigger: ratify or revert** — unowned. `ASSUMPTIONS.md` §2.2.

- 2026-09-05 **`SameSite=Lax` assumes frontend and API share a registrable domain** (TASK-0003 delta). Cross-site needs `SameSite=None` AND a reconsidered CSRF posture. **Trigger: deployment (Q5).** Owner human + `backend-dev`. Full text: `drift/2026-Q3.md`.

**Accepted, no trigger** — 8 entries, archive-only; `grep` the date in `drift/2026-Q3.md`:
2026-08-08 · 2026-08-26 ×4 · 2026-08-27 ×2 · 2026-09-04.

Eleven resolved/struck entries are archive-only — latest: the stale-client trigger, struck
2026-09-05 by TASK-0025.

## Open questions

Live only; ten resolved questions are in `decisions/2026-Q3.md`.

5. **Production database target** undecided; not blocking until deployment. Four live drift
   triggers wait on it (DP key ring, `SameSite=Lax`, shared DB role, cookie domain) — one
   decision clears all four.

## Reading this file

**Budget: 12 KB, bytes.** `wc -c` before appending. Over it, archive full text to `decisions/` or
`drift/`, leave a one-line index — never delete. Rules: CLAUDE.md §4.1, §13.
