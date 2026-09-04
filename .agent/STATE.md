# Project State

Last reconciled: 2026-09-04 by orchestrator · size budget 12 KB, see `## Reading this file`

## Product

**GRAS School Management System** — Golden Royal Ark School (nursery + primary, Nigerian
three-term session). Back office for configuration, pupil register, admission, marks, result
computation/approval/publication, weekly pastoral reports and pin generation; plus one public
page where a parent enters a registration number and an access pin to read or download a
published result. Parents have no accounts.

Authoritative spec: `product-specification/` (revision 3.1), `index.md` is the entry point. The
school's `.docx` is revision 1 and is **not** authoritative. Out of scope
(`00-document-overview.md` 3.2): fees/payments, HR/payroll, timetabling, standalone attendance,
messaging/SMS, library/transport, CBT, parent accounts.

## Layout
backend:  ./backend — solution `SchoolManagement.slnx`. Api / Application / Domain /
          Infrastructure under src/; Unit, Integration and Architecture tests under tests/.
          Reference vertical slice only (`/api/v1/reference/*`, `/health/*`). No domain code yet.
          Conventions: `backend/AGENTS.md`. Deviations: `backend/docs/ASSUMPTIONS.md`.
frontend: ./frontend — Vite 8 / React 19 / TypeScript 6, package `gra-school-portal`, npm
          (lockfile committed). Design tokens, Base UI primitives, axios transport in
          `src/lib/http/`, TanStack Query, Zustand, MSW, 124 tests green. `src/features/` is
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
backend:  `./backend/scripts/ci.ps1` — restore, build -warnaserror, format --verify-no-changes,
          tests + coverage, coverage floor, vulnerable-package scan, gitleaks, OpenAPI drift.
          CI: `.github/workflows/backend-ci.yml`.
frontend: `npm run verify` (typecheck, lint, test, build) plus `npm run check:api-drift`
          (§4.4 check 2, deliberately not folded into `verify`). oxlint, not ESLint.
          `npm run generate:api` regenerates the client when the contract moves.
          CI: `.github/workflows/frontend-ci.yml`.

## Contract
openapi.json sha256: 228ae57fd80c22f7e0511a665efce5d4722bb3280c66f5e3f4c96fff53795a65
regenerated: 2026-08-26 · generator Microsoft.Extensions.ApiDescription.Server/10.0.10, SDK 10.0.100
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

Open cards only. Closed: TASK-0001, 0002, 0004, 0006, 0007, 0008, 0009, 0010, 0011 — closure
notes and reopen history in [decisions/2026-Q3.md](decisions/2026-Q3.md).

| Task | Title | Owner | Status |
|---|---|---|---|
| TASK-0003 | Admin accounts, authentication and session management | backend-dev | queued |
| TASK-0005 | School settings: identity, registration number, config versioning | backend-dev | queued |
| TASK-0012 | Declare the problem-detail extension members in the contract | backend-dev, then frontend-dev | queued |
| TASK-0013 | Decide and implement idempotency for retryable mutations | backend-dev | queued |
| TASK-0014 | Frontend scaffold conformance fixes from the §7 audit | frontend-dev | queued |
| TASK-0015 | Backend scaffold conformance fixes from the §6 audit | backend-dev | queued |
| TASK-0016 | Make the gate suites fail fast and fail honestly | backend-dev, then frontend-dev | queued |

Full sequence and cards not yet written: [ROADMAP.md](ROADMAP.md).

## Decisions

Index only, newest last. **Full text and reasoning:** [decisions/2026-Q3.md](decisions/2026-Q3.md)
and the `## Log` of the card each entry names. Bootstrap decisions (2026-07-27 to 2026-08-08) are
in the same file and still in force, as are the six 2026-08-27 infra-card entries condensed out
on 2026-09-04.

- 2026-08-27 TASK-0011 — human approved narrowing the gitleaks rule rather than adding a baseline.
- 2026-08-27 TASK-0007, 0008 and 0011 all closed on one verified live run — `ALL GATES PASSED`, integration suite genuinely executed.
- 2026-08-27 The gate suite is honest end to end for the first time — vuln gate green, secret scan real, coverage enforced on a complete run.
- 2026-08-27 Test-database credential split confirmed deliberate — a least-privilege role, not `neondb_owner`, backs `POSTGRES_TEST_CONNECTION`.
- 2026-08-27 TASK-0001 closed — both scaffolds audited rule by rule against §6/§7; both materially exceed spec.
- 2026-09-04 **Context budget restructured** — this file capped in bytes, `## Decisions` and `## Known drift` reduced to indexes, whole-file contract reads dropped from the dev agents, §6/§7 given one home each.

## Known drift

Index only. **Full text:** [drift/2026-Q3.md](drift/2026-Q3.md) — find an entry there by its date.
Read the entries your card names, not all of them.

- 2026-08-08 Backend implementation landed with no task card and no boundary review. Audited retroactively by TASK-0001.
- 2026-08-26 The frontend scaffold likewise landed with no task card. Also audited by TASK-0001.
- 2026-08-26 `frontend/src/lib/auth/` implements bearer-token auth, against the cookie sign-off. Known-wrong code awaiting TASK-0003 frontend half.
- 2026-08-26 `frontend/src/screens/scaffold-status/` is a demo, not a feature. Delete when the first real screen lands.
- 2026-08-26 Two dependency bumps are being watched rather than assumed benign (`oxlint` 1.76.0 → 1.80.0 among them).
- 2026-08-26 TASK-0002 left `IPupilArmOfRecordLookup` and `IResultSetArmLookup` as Application abstractions with Infrastructure registrations.
- 2026-08-26 The reference slice sits in the committed contract in four places, none of them product endpoints.
- 2026-08-26 A process lesson recorded but deliberately not acted on — orchestrator read, backend-dev concurring.
- 2026-08-26 **Staging and dev-test databases share one role and one password.** Both Neon strings use the same credential.
- 2026-08-27 **The local `Secret scan` gate is red for reasons unrelated to any pending card**, found by TASK-0008 installing gitleaks.
- 2026-08-27 **`Docker.DotNet.Enhanced` is unverified in practice and unverifiable on this machine** — TASK-0007 transitive consequence.
- 2026-08-27 **`ci.ps1` coverage gate recognises skipped tests but not FAILED ones** — it printed `PASS` on a run with failures.
- 2026-08-27 TASK-0011 Family B allowlist leaves one narrow gap, measured rather than assumed.
- 2026-08-27 **Validation runs as a mediator pipeline behaviour, not the endpoint filter §6 specifies** (`backend/docs/ASSUMPTIONS.md`).
- 2026-08-27 **The frontend tree has no `src/shared/`**, which §7 and §2 both name. See Open question 11.

Four entries resolved or struck (`AUDIT.md` staleness, tracked `obj/`, the SSH.NET gate, the
missing `SecureArmResponse` example) live in the archive only.

## Open questions

Live only. The nine resolved questions are in [decisions/2026-Q3.md](decisions/2026-Q3.md).

5. **Production database target** undecided. Not blocking until deployment.
11. **Where do frontend screens live, and are three 2026-08-03 deviations still intended?**
    Raised by TASK-0001. (a) §7 puts screens under `src/features/<feature>/`; the scaffold has a
    top-level `src/screens/` and no `src/shared/`, so the first real screen has no agreed home.
    (b) `react-hook-form` is absent, yet §7 mandates it plus a zod resolver. (c) Playwright is
    absent, yet §7 mandates it for auth, the primary create path and one failure path. All three
    are called deliberate in `frontend/HANDOFF.md` — written **2026-08-03, before the product
    specification landed**, so they were chosen without knowing what gets built. Each becomes
    binding the moment a screen, a form or a flow exists, i.e. at the TASK-0003 frontend half.
    Not blocking today. Needs a human call, not an agent call.

## Reading this file

**Budget: 12 KB.** Every subagent loads this file on session start, so its size is multiplied by
every dispatch. The cap is on **bytes, not lines** — the old ~200-line cap read green at 36 KB
because 45 "one-line" entries averaged 550 bytes each.

When a section grows past the budget: move the full text to `decisions/` or `drift/` and leave a
one-line index entry pointing there. Never delete — archive. Closed cards leave the `## In flight`
table. Check `wc -c .agent/STATE.md` before appending.
