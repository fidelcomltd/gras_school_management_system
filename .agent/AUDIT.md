# Scaffold audit — backend §6 and frontend §7

Produced by TASK-0001, 2026-08-27. **Supersedes the 2026-07-27 version entirely**, which was
written against two empty directories and said so.

Method: two parallel read-only audits (backend §6, frontend §7), each recording every rule as
pass / fail / N-A with the `file:line` that evidences it. Neither auditor wrote anything. Every
finding marked **[verified]** below was independently re-checked by the orchestrator by reading the
file or running the command; the rest are the auditors' reports, accepted but not re-derived.

Gate evidence at time of audit:

- Backend — `./backend/scripts/ci.ps1`, orchestrator's live run 2026-08-27: **ALL GATES PASSED**.
  ArchitectureTests 24/24, UnitTests 124/124, IntegrationTests 30/30, `Skipped: 0`. Coverage
  77.18% line / 58.61% branch enforced on a complete run. Seven projects clean of vulnerable
  packages. Secret scan `6 commits scanned -> no leaks found`. Contract drift clean at `228ae57f`.
- Frontend — `npm run verify` (typecheck, lint, test, build), run for real during the audit:
  **exit 0**, 14 test files, 124 tests, zero lint diagnostics of any severity, build clean.
- Contract — all four §4.4 checks run by the orchestrator: backend regen diff empty, frontend
  client no drift, no raw `fetch`/`axios` outside the client, `CONTRACT.lock` hash matching.

**Headline: both scaffolds materially exceed spec, and the acceptance recorded in `STATE.md`
`## Known drift` (2026-08-08 and 2026-08-26) is now evidence-based rather than assumed.** The rules
§6 and §7 care most about are not merely followed but mechanically enforced — 24 architecture
tests, `TreatWarningsAsErrors` with `AnalysisLevel latest-All`, CA1848/CA1849/CA2016 promoted to
errors, deny-by-default authorization with a boot-time route-declaration guard on the backend; and
on the frontend zero `any`, zero `@ts-ignore`, zero non-null `!`, `noUncheckedIndexedAccess`, and 35
of oxlint's 36 `jsx-a11y` rules firing as errors. Two findings are genuine blockers. The rest are
small.

---

## Blockers

### B1 — The problem schemas forbid the two members every error response carries [verified]

`contracts/openapi.json` · owned by **TASK-0012**

`components.schemas.ProblemDetails` and `HttpValidationProblemDetails` both declare
`"additionalProperties": false` and declare neither `errorCode` nor `traceId`. Verified directly:
`ProblemDetails` declares exactly `detail, instance, status, title, type`.

The API attaches `errorCode` to every problem response
([ResultExtensions.cs:75](../backend/src/SchoolManagement.Api/Http/ResultExtensions.cs#L75),
[GlobalExceptionHandler.cs:117](../backend/src/SchoolManagement.Api/Http/GlobalExceptionHandler.cs#L117))
and `traceId` centrally to all of them including framework-generated ones. The schema's own
`description` instructs clients to "Branch on the `errorCode` extension member — it is stable — and
never on `detail`", and its own `example` contains both members, **so the example is invalid against
the schema that illustrates it.**

Root cause: `Program.cs:93` sets `UnmappedMemberHandling.Disallow` globally, so the generator stamps
`additionalProperties: false` on all nine schemas. Correct for request bodies, wrong for RFC 9457
problem objects, whose extension members are the entire mechanism.

**It has already propagated.** The generated
[schema.d.ts:292](../frontend/src/api/schema.d.ts#L292) `ProblemDetails` type has five members and
neither extension — verified. So the first card whose UI branches on an error code (TASK-0003) must
either cast (§7 forbids `any`; `@ts-ignore` is a blocker) or hand-edit a generated file (§3 review
blocker). Violates §6's RFC 9457 requirement in its contract expression, and §3. Backend fix plus a
regeneration; additive, not breaking.
`OpenApiContractTests.EveryOperation_DocumentsItsErrorResponses:84` does not catch it.

### B2 — No `Idempotency-Key`, anywhere, and the omission is undocumented [verified]

`backend/src/SchoolManagement.Api/Endpoints/ReferenceEndpoints.cs:107-132` · owned by **TASK-0013**

§6: "Mutating endpoints that can be retried accept an `Idempotency-Key`." Verified: zero
occurrences of `Idempotency` in any `.cs`, `.csproj`, `.md` or `.ps1` under `backend/` apart from
`dotnet ef migrations script --idempotent` in `README.md`. No middleware, no endpoint filter, no
`ASSUMPTIONS.md` entry, no `## Known drift` line, no card.

Nothing is broken today — no product mutating endpoint exists — so this is **decide-now, not
fix-now**. It is a blocker because `ReferenceEndpoints` is the declared copy-from template for every
future module and its doc comment enumerates "the same five things every endpoint below
demonstrates", idempotency not among them. Every future POST copied from `MapCreateSampleRecord`
inherits the gap, and retrofitting across twenty endpoints is exactly the expensive-later case this
card existed to prevent. Note spec 9.8.2 independently requires idempotency keys on pupil
registration, pin generation, promotion commit and result publication — so the requirement arrives
with Phase 2 whether or not §6 is read.

This is the only silent omission found in an otherwise scrupulously self-documenting scaffold.

---

## Should-fix

### Backend — owned by TASK-0015

- **S1. `/health/ready` is anonymous, unthrottled, and hits the database.**
  [HealthEndpoints.cs:57-65](../backend/src/SchoolManagement.Api/Endpoints/HealthEndpoints.cs#L57-L65).
  Health checks are mapped on `app`, not the rate-limited `versionedApi` group (`Program.cs:261-264`),
  and no global limiter exists — so an unauthenticated endpoint performs a real DB round trip per
  request. A cheap amplifier against the database. §6 "Rate limiting middleware on public endpoints."
  The tension the file itself documents is real (throttling a probe causes false unhealthy signals);
  a generous dedicated policy or an ingress restriction resolves it. The current state is the one
  combination with no protection at all.
- **S2. The Api layer depends on an Infrastructure type at runtime.**
  [GlobalExceptionHandler.cs:3](../backend/src/SchoolManagement.Api/Http/GlobalExceptionHandler.cs#L3)
  imports `SchoolManagement.Infrastructure.Persistence` and calls `PersistenceErrors.TryTranslate`
  inside the HTTP pipeline, outside the "register services in DI" scope the `.csproj` header sets for
  that reference. §6 layering. `DependencyDirectionTests` has no rule covering Api → Infrastructure,
  so this is the one layering rule in the repo that is documented rather than enforced. Fix: an
  Application port implemented by `PersistenceErrors`, plus the missing architecture test.
- **S3. The relocated document tests cannot tell a stale artefact from a fresh one.**
  `ArchitectureTests/OpenApiDocument.cs:38-44` checks only `File.Exists`. Sound inside `ci.ps1`,
  which regenerates immediately before `dotnet test`; outside it, a bare `dotnet test` asserts eight
  document properties against whatever JSON is on disk. Missing fails loudly; **stale passes
  silently** — the same defect family as TASK-0010. Fix: compare mtime against the newest file under
  `src/SchoolManagement.Api`, or hash-check.

### Frontend — owned by TASK-0014

- **S4. Both READMEs prescribe bypassing the generated client.** [verified]
  [features/README.md:56-62](../frontend/src/features/README.md#L56-L62) says hooks call the
  `@/lib/http` verb helpers "and nothing else", and its worked example does
  `getRequest<IStudentListItem[]>('/api/v1/Students')` — a hand-written response type and a
  hardcoded path. [api/README.md:74-75](../frontend/src/api/README.md#L74-L75) sanctions the same
  "for anything not yet worth a typed wrapper". This is not a footnote permitting a bypass, it is
  the **prescribed pattern**, and it contradicts §7 ("all server communication goes through the
  generated client") and §3 ("never guesses a response shape"). It is the template the first feature
  agent copies. **The single most likely finding here to become a real defect in a later card.**
- **S5. `npm run lint` cannot fail on warnings.** [verified] `package.json:14` is bare `oxlint`,
  which exits 0 with warnings present (auditor verified by planting one). So the whole `suspicious`
  category plus `no-console`, `react/no-array-index-key`, `react/only-export-components`,
  `typescript/consistent-type-imports` and `jsx-a11y/no-autofocus` cannot block CI, and
  `CONVENTIONS.md:290` ("currently zero warnings; keep it that way") is aspirational.
  `--max-warnings=0` makes it real. Related: `.oxlintrc.json:56` disables
  `typescript/no-explicit-any` for test files — `any` is unguarded exactly where it slips in easiest.
- **S6. The http barrel re-exports the raw axios instance.** [verified]
  [lib/http/index.ts:1](../frontend/src/lib/http/index.ts#L1) exports `httpClient`. Nothing —
  lint, typecheck, or `http-boundary.test.ts`, which matches `from 'axios'` and `fetch(` — stops a
  feature importing it and issuing a request that skips `ApiError` normalisation and 401 handling.
  Unused outside `src/lib/http/`, so it costs nothing to stop exporting.
- **S7. The catch-all route has no `ErrorBoundary`.** [verified]
  [app-router.tsx:25-37](../frontend/src/app/router/app-router.tsx#L25-L37). Harmless today (static
  markup) but §7 says per-route, the route table is the template the next card extends, and the
  file's own doc comment at line 8 claims "Each route wraps its element in an `ErrorBoundary`" —
  false as written.
- **S8. No `src/shared/`, and the real layout is unrecorded.** §7's promotion rule and CLAUDE.md §2
  both name `src/shared/`; the tree has `src/components/`, `src/stores/`, `src/screens/`, `src/lib/`
  at root and no `src/shared/` at all. Defensible for a design system, but §2 requires the real paths
  be recorded and used, and `STATE.md ## Layout` did not mention it — **fixed in this pass**.
  `src/screens/` is the sharper edge: §7 puts screens under `src/features/<feature>/`, so where the
  first real screen goes is currently undecided. Needs a decision, not a fix.

---

## Notes for TASK-0003 specifically

From the frontend audit, and one is good news:

- **The cookie seam is unbuilt, not half-built.** No `withCredentials`, no `credentials: 'include'`,
  no CSRF header exists anywhere. Nothing to unpick.
- `session-store.ts:44-47` `signOut()` clears client state only; the server-revoke obligation lives
  in a doc comment. §5 calls client-only clearing a blocker, so TASK-0003 must make server
  revocation the only path, not a documented duty.
- `VITE_AUTH_EXPIRY_LEEWAY_SECONDS` (`env-values.ts:28`) and the scheduled-expiry timer are **dead
  under HttpOnly cookies** — JS cannot see expiry. Delete, do not port.
- The interceptor's success path — 401 → refresh succeeds → request replayed with `authRetried`
  (`http-client.ts:65-72`) — has **no test**; `request.test.ts:167` covers only the no-handler case
  and says so. That replay machinery survives the cookie migration and should gain a test.
- B1 above blocks the frontend half of TASK-0003. Sequence TASK-0012 before it.

---

## Corrections to the ledger this audit produced

- **`backend/src/**/obj/` is NOT tracked.** [verified] `git ls-files | grep -ci '/obj/'` → 0, and
  `backend/.gitignore:3:[Oo]bj/` ignores it. Seven `obj/` directories exist on disk as ordinary build
  output. The 2026-08-26 drift entry conflated *present* with *tracked* and has been struck.
- **`frontend/dist/` is correctly ignored** (`frontend/.gitignore:11`) with 0 tracked files. Same
  conflation; struck.
- **`STATE.md ## Contract` overstated the surface** — it listed `/health/live|ready`, which are
  `.ExcludeFromDescription()` and absent from the document's four paths. Corrected.
- **The coverage drop is explained and is not a regression.** [verified] 77.18% today against 87.00%
  at TASK-0002's close, on the same 178 tests. TASK-0009 moved `OpenApiContractTests` out of the
  integration project; the deleted version's `Client.GetAsync("/openapi/v1.json")` was the only
  in-process fetch of the generated document, and executing it covered the whole runtime
  document-generation path. Exactly four classes now sit at literal 0% — `OpenApiExamples` (123
  coverable lines), `ApiInfoDocumentTransformer` (31), `VersionedPathDocumentTransformer` (23),
  `SchemaExampleTransformer` (17) = 194 lines. 1502/1946 = 77.18%; add 194 back and 1696/1946 =
  87.15% against 87.00% recorded. `OpenApiSetup` still reads 100% — the registration path, exercised
  at every host boot. This is a loss of coverage *attribution*, not of *checking*: all eight
  assertions still run, and now run without a database. **No card. Restoring the number would mean
  re-adding an in-process document fetch purely to move it.**
- The validation-as-pipeline-behaviour deviation (`ASSUMPTIONS.md` §2.2) was documented in the
  backend docs but absent from `STATE.md`. Now recorded as drift, with the ratify-or-revert question
  it asks for still open.

---

## Nice-to-have, recorded and deliberately not carded

Backend: `SampleRecord.cs:11`'s doc comment still claims an `xmin` concurrency token that was
rejected for an application-maintained GUID (`ASSUMPTIONS.md` §2.4) — will mislead whoever writes the
first real aggregate from that template. Nothing enforces that async methods *accept* a
`CancellationToken` (CA2016 enforces forwarding one that exists); holds by discipline today across
all 17. `RateLimitingOptions.SensitivePolicyName` is registered and used by nothing — correct as
staged infrastructure, TASK-0003's login is its first user, but its 10/60s limits are untested.

Frontend: `.oxlintrc.json:61` ignores `scripts/**` and no tsconfig `include` covers `scripts/*.mjs`,
so the three contract-pipeline scripts — including the §4.4 drift gate itself — are unlinted,
untypechecked and untested. `index.html:9-26` reimplements the theme store's storage key and
persisted shape in untested inline JS, so a change to `STORAGE_KEY` or `partialize` silently breaks
the no-flash path. `vite.config.ts:37-42` configures coverage with no `thresholds`, unlike the
backend's 60% floor. Sixteen source files have no colocated test; the ones that matter are
`lib/http/http-client.ts` and `stores/session-store.ts`. `features/README.md:35-41` prescribes a
query-key **enum** rather than the factory §7 asks for, composing parameterised keys inline at each
call site — precisely what a factory prevents. No loading/skeleton/empty/unauthorized primitives
exist, so the first feature card must invent all four required states. `react-hook-form` and
Playwright are both absent and documented as deliberate in `HANDOFF.md` — each is a §7 requirement
the moment a form or a flow exists, and no card owns either. `HANDOFF.md:19` still says 120 tests;
it is 124.

## What could not be determined

- Neither auditor re-ran the backend gate suite; §9 verdicts rest on the orchestrator's 2026-08-27
  run.
- The TASK-0009 coverage move could not be isolated by diffing coverage across commits — `93df8da`
  squashes the move with other work. The confirmation is arithmetic plus the deleted file's content,
  not a before/after measurement.
- Whether the accepted frontend deviations — no `src/shared/`, no react-hook-form, no Playwright —
  are still the intended call or merely inherited. All were decided 2026-08-03, **before the product
  spec landed**, so they were chosen without knowing what gets built. That is a human decision, not
  an audit finding.
