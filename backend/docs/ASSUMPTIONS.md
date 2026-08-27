# Assumptions and open decisions

Everything decided **without explicit instruction**, and everything still waiting on a human.

Recorded because a scaffold's assumptions are invisible once code is built on them: by the time an
assumption turns out to be wrong, twenty files depend on it. Each entry says what was assumed, why, and
what it would cost to change.

Last updated: 2026-08-03.

---

## 1. Decisions taken on my recommendation

These were put to the human and confirmed before implementation.

| Decision | Chosen | Rationale |
|---|---|---|
| Product name / namespace | `SchoolManagement` | Derived from the repository name. Cheap to change now, expensive after feature work. |
| Mediator | Hand-rolled (~150 LOC) | **Verified on NuGet:** MediatR ≤ 12.5.0 is `Apache-2.0`; 13.0.0–14.2.0 publish no SPDX expression and carry an embedded commercial licence. A scaffold every contributor copies should not have an unresolved licensing question at its core. See ADR 0002. |
| Secret store | Seam + environment variables | The deployment target is undecided, so committing a cloud SDK risks adding a dependency to the wrong cloud. See ADR 0009. |
| Integration-test database | Testcontainers, skipping loudly when unavailable | Docker is not installed on the development machine. See §3.1. |

---

## 2. Decisions taken without being asked

### 2.1 Target framework: `net10.0`

The only SDK installed is 10.0.100, and the root `CLAUDE.md` §11 specifies ".NET 10". **Cost to
change:** one property in `Directory.Build.props`, plus package downgrades.

### 2.2 Validation is a pipeline behaviour, not an endpoint filter — DEVIATION

The root `CLAUDE.md` §6 says "Validation via FluentValidation registered as an endpoint filter". It is
implemented as a **mediator pipeline behaviour** instead.

**Why:** an endpoint filter is attached per endpoint, so the failure mode is "somebody adds an endpoint
and forgets" — an unvalidated endpoint that looks completely normal in review. As a behaviour it is
structural, applies to everything dispatched through `ISender`, and cannot be opted out of. It also
still satisfies the rule's intent: validation executes before the handler.

This satisfies §6's *intent* with a stronger guarantee, but it is a deviation from its *letter* and
should be either ratified or reverted. **Cost to change:** moderate — the behaviour would be deleted and
a filter added to every endpoint, plus a test asserting each endpoint has one.

### 2.3 Validation failures return 422, not 400

§6 says "422 on validation failure (or 400 — pick one and be consistent)". 422 was chosen so that
"malformed request" (400, produced by the framework's model binding before our code runs) stays
distinguishable from "parsed but rejected by a rule" (422). **Cost to change:** one line in
`ApiProblem.ToStatusCode`, plus test updates. It is a **breaking** contract change once clients exist.

### 2.4 Concurrency token is an application-maintained GUID, not `xmin`

The brief suggested `rowversion`/`xmin`. PostgreSQL's `xmin` is the better mechanism in principle — no
column, no application code — but **Npgsql's `UseXminAsConcurrencyToken()` no longer exists in the EF
Core 10 provider** (verified: no xmin-related member in its public API). Mapping `xmin` by hand makes
the migration attempt to CREATE a column of that name, which PostgreSQL rejects as a collision with the
system column, and every future entity's migration would then need hand-editing.

So every auditable entity gets a shadow `Version` GUID, applied by convention, reassigned by the
auditing interceptor. Costs 16 bytes per row and one assignment per save. **Cost to change:** a
migration plus a change to one convention method.

### 2.5 Contract generation needs an explicit mode flag

OpenAPI generation **starts the application** to read its route metadata, so `ValidateOnStart` would
demand a configured database merely to produce a JSON file — meaning no developer could `dotnet build`
without one. `SCHOOLMANAGEMENT_CONTRACT_GENERATION` skips only the startup checks that need live
infrastructure.

The alternative — a dummy connection string in the build script — was rejected: a realistic-looking
placeholder credential is exactly the value that later gets copied into a deployment. Generation is also
therefore **opt-in per build** (`-p:GenerateOpenApiContract=true`), not on by default.

### 2.6 Line endings are LF, enforced by `.gitattributes`

CI runs on Linux and the sibling `frontend/` is a Node project. Mixed endings make
`dotnet format --verify-no-changes` pass locally and fail in CI, or vice versa.

### 2.7 One extra analyser package set: Roslynator

The brief said pick one, do not stack three. `AnalysisLevel` is `latest-All` with a curated list of
relaxations in `.editorconfig`, **each carrying a written justification**.

### 2.8 `AuditingInterceptor` handles soft delete as well as auditing

They are order-dependent: soft deletion rewrites `Deleted` → `Modified`, and auditing stamps
`ModifiedAtUtc` on modified entries. Split across two interceptors, correctness would depend on
registration order — invisible at the point of failure and one careless reorder from silently losing the
audit trail on deletes.

### 2.9 Health endpoints are excluded from the OpenAPI document

A probe URL is infrastructure configured in a deployment manifest, not part of the versioned API
contract, and the frontend client should not generate methods for it.

### 2.10 The coverage gate is skip-aware, and the floor is 60%

The floor is only **enforced when the test suite was complete**. When integration tests skip (no
PostgreSQL), the whole HTTP layer is never executed and coverage is structurally ~19% — so enforcing the
floor would fail for a reason unrelated to code quality, and the predictable response would be to lower
the floor until it passed, destroying the gate for everyone.

When the suite is incomplete the gate prints the number and states **loudly that the floor was not
enforced**. It never silently passes a check it did not perform. CI supplies a database, so the floor is
enforced there, and a separate CI step fails the build if anything was skipped at all.

### 2.11 `Microsoft.OpenApi` is pinned to 2.11.0 for a security advisory

`Microsoft.AspNetCore.OpenApi 10.0.10` resolves `Microsoft.OpenApi 2.0.0`, which is affected by
**CVE-2026-49451** (GHSA-v5pm-xwqc-g5wc, High, CVSS 7.5): a crafted document with circular schema
references causes a stack overflow and terminates the process parsing it. Affected `>= 2.0.0-preview.11,
<= 2.7.4`; patched at 2.7.5 in the 2.x line.

Pinned via central transitive pinning to the latest **2.x** rather than 3.x, because the ASP.NET Core
package is built against the 2.x object model. **Found by the vulnerability-scan gate on its first real
run** — the gate earned its place immediately. Remove the pin once
`Microsoft.AspNetCore.OpenApi` resolves a patched version itself.

### 2.12 An unknown path returns 401, not 404

ASP.NET Core's authorisation middleware applies the fallback policy to requests matching **no**
endpoint, not only to endpoints lacking authorisation metadata. With deny-by-default, an unknown path is
therefore rejected before routing can produce a 404.

Kept rather than worked around: an unauthenticated caller cannot enumerate routes by probing 404 versus
401. The cost is that a typo'd URL looks like an auth failure, so it is stated in the OpenAPI description
and asserted by `AnUnknownRoute_Returns401NotFound_BecauseOfTheFallbackPolicy`. Changing it would mean
adding a terminal 404 middleware ahead of authorisation, which would leak route existence.

**This was found by the integration tests on their first real run** — the test originally asserted 404,
an assumption I had not verified. The behaviour was right; the expectation was wrong.

### 2.13 `SampleRecord` exists purely to prove the wiring

**It is not a business entity and carries no product decision.** It proves EF Core mapping, the auditing
interceptor, the soft-delete filter, the concurrency token, snake_case naming, pagination, migrations,
and the integration-test harness all work end to end.

**Delete it with the first real aggregate** — the entity, its configuration, repository, endpoints, DTOs,
examples and tests, plus a migration dropping the table.

---

## 3. Open — a human must decide or supply

Ordered by how much they block.

### 3.1 RESOLVED — verified against a real PostgreSQL

Docker is still not installed, but the `POSTGRES_TEST_CONNECTION` path was exercised against a hosted
Neon PostgreSQL instance. **All 32 integration tests pass**, so the Definition of Done item *"the
reference vertical slice runs end to end against a real database"* is now **verified**, not assumed.

Full suite: **132 passed, 0 failed, 0 skipped**. Coverage 86.43% line / 63.59% branch, above the 60%
floor with the floor genuinely enforced (it is only enforced when nothing was skipped).

That run found three real defects that no unit or architecture test could have caught — see §4.

Docker remains optional. Either a container runtime or `POSTGRES_TEST_CONNECTION` works; CI uses the
latter with a service container.

**Note on the test database:** it now contains the `sample_records` table and `__ef_migrations_history`.
The fixture truncates only tables EF maps, so nothing else in that database can be affected.

### 3.2 BLOCKING for production — the authentication mechanism

**Undecided, and requires human sign-off** (root `CLAUDE.md` §5). Root §5 recommends an HttpOnly cookie
session for a first-party web app.

What exists: deny-by-default authorisation, named policies, and a placeholder scheme that authenticates
nobody so the pipeline is genuinely exercised and tested. **The application refuses to start with the
placeholder scheme outside Development.**

Whatever is chosen must match the frontend exactly — `SameSite`, `Secure`, domain, and credentials mode
are one coherent set, and a mismatch presents as an unexplained CORS error.

### 3.3 Deployment target, and no Dockerfile yet

Assumed to be a container (ADR 0008), which is what the health-check split and environment-variable
configuration are shaped for. **No Dockerfile is included** — base image, non-root user, and whether
migrations run as an init container or a pipeline step are deployment decisions I should not invent.

### 3.4 The CI workflow must be moved by the orchestrator

`ci/github-actions-backend.yml` is complete and ready, but GitHub only reads `.github/workflows` at the
**repository root**, which the orchestrator owns (root `CLAUDE.md` §11). Copy it to
`.github/workflows/backend-ci.yml`. Until then, **no CI is running.**

### 3.5 Coverage floor is 60%

Chosen without instruction. It is a **floor to catch a collapse**, not a target. Adjust once the real
codebase exists; raising it to chase a number produces tests that execute code without asserting
anything about it.

### 3.6 No product requirements exist

No entities, endpoints or rules beyond the reference slice, because none were specified. The repository
is named `school-management-proj`; which of students, enrolment, attendance, grading, fees or
timetabling is in scope — and for whom — is unknown. **Feature instructions are expected from the
orchestrator.**

### 3.7 `git` is not initialised

The repository is not a git repository. The secret-scan and contract-drift gates, Conventional Commits,
and the entire CI workflow assume version control. `git init` was not run: remote and branching choices
are the human's.

*(Superseded — resolved 2026-08-08, see `STATE.md` Decisions. Left in place rather than deleted so the
paragraph trail is not rewritten; §1's append-only rule applies here too.)*

### 3.8 BLOCKING the "Vulnerable dependencies" gate — `SSH.NET` 2025.1.0, High severity — PRE-EXISTING, unrelated to TASK-0002

`dotnet list package --vulnerable --include-transitive` reports **GHSA-q939-rpr3-3284 (High)** against
`SSH.NET 2025.1.0`, a transitive dependency of `Testcontainers.PostgreSql 4.13.0` (via `Docker.DotNet`,
used for the SSH exec path against remote Docker hosts — not a path this project's test fixture uses,
which only ever talks to a local daemon or an externally supplied `POSTGRES_TEST_CONNECTION`).

**Found while running `./scripts/ci.ps1` for TASK-0002** (privilege register and authorisation
enforcement). Verified pre-existing and NOT introduced by that card: `git status`/`git diff` show no
`.csproj` or `Directory.Packages.props` change from TASK-0002, and the package graph is unchanged. It
would fail this gate identically on `main` before TASK-0002's commits.

Not fixed here because upgrading `Testcontainers.PostgreSql` (or pinning `SSH.NET` directly, the way
§2.11 pins `Microsoft.OpenApi`) is a dependency change with its own blast radius — the integration-test
harness — and is outside a card scoped to the authorisation substrate. Per root `CLAUDE.md` §8,
dependency changes need their own justification in a task card.

**Not marked as an accepted risk** — that is a security-posture call for the orchestrator/human, not
mine to make unilaterally while implementing an unrelated card. Two ways to close this:
1. Check whether a newer `Testcontainers.PostgreSql` resolves a patched `SSH.NET`, and bump if so.
2. If no patched version exists yet, pin `SSH.NET` to a fixed version via central transitive pinning
   (same mechanism as §2.11), or explicitly accept the risk here with a named owner and a review date.

Until one of those happens, `./scripts/ci.ps1`'s "Vulnerable dependencies" gate fails locally and in CI.
This is the one gate TASK-0002 could not turn green — every other gate passes.

---

## 4. Defects found by the first real integration run

Recorded because each one argues for keeping a class of test that is otherwise easy to cut, and because
all three were invisible to the build, the analysers and 100 unit/architecture tests.

| Defect | Impact if shipped | Fix |
|---|---|---|
| `GlobalExceptionHandler` ignored `BadHttpRequestException.StatusCode` | **Every malformed request became a 500.** Unparseable JSON, an unknown field, or an oversized body would tell the client the SERVER failed, and each occurrence would inflate the error rate and page somebody | Honour the exception's status code; 400 for malformed, 413 for too large |
| `MapOpenApi` / Scalar had no `.AllowAnonymous()` | The authorisation fallback policy protected them, so the docs and the OpenAPI document returned **401 to everyone** in Development | Added `.AllowAnonymous()` to both |
| An unknown path returns 401, not 404 | No impact — the behaviour was correct and the TEST was wrong. See §2.12 | Test corrected to assert 401, and the behaviour documented in the OpenAPI description |

The first is the one that matters: it is a defect in the error contract itself, on the most common
client mistake there is, and only an end-to-end request could surface it.

## 5. Verified facts worth recording

Established by checking rather than assuming, and easy to get wrong from memory:

- **MediatR licensing** — versions ≤ 12.5.0 are `Apache-2.0`; 13.0.0+ publish no SPDX expression and
  carry an embedded commercial licence.
- **Microsoft.OpenApi 2.0** (shipped with .NET 10) flattened its object model out of
  `Microsoft.OpenApi.Models` into the root namespace, and replaced the `OperationType` enum with
  `System.Net.Http.HttpMethod` as the key of `PathItem.Operations`. Code written against 1.x will not
  compile.
- **Npgsql EF Core 10** no longer exposes any `xmin` concurrency-token API.
- **`.NET 10` `dotnet new sln`** produces the XML `.slnx` format, and `dotnet new tool-manifest` writes
  `dotnet-tools.json` at the project root rather than under `.config/`.
- **XML doc comments flow into the OpenAPI document automatically** in .NET 10 (a source generator reads
  the documentation file), which is why `GenerateDocumentationFile` is on repo-wide and `CS1591` is an
  error in `Api` and `Application`. **Examples do not** — they need a schema transformer.
- **`Microsoft.OpenApi` 2.0.0 carries a High-severity advisory** (CVE-2026-49451); patched at 2.7.5 in
  the 2.x line. See §2.11.
- **xunit v3's `Assert.Skip` does NOT increment the trx `notExecuted` counter.** A fully skipped project
  reports `total=31 passed=0 failed=0 notExecuted=0`. Any tooling that counts skips must derive it as
  `total - passed - failed`; reading `notExecuted` silently concludes the suite was complete. Both
  `scripts/ci.ps1` and the CI workflow do the former, and the mistake is easy to make.
- **Each test project emits its own coverage report**, and the same production assembly appears in
  several of them. Reading one file, or summing them naively, gives a number that swings with whichever
  file was picked. They must be merged by class and line — `reportgenerator` is pinned as a local tool
  for exactly this.
- **An XML comment may not contain a double hyphen.** This breaks `Directory.Packages.props`,
  `.csproj` and `.runsettings` files, and the resulting error never mentions comments — a broken
  `Directory.Packages.props` surfaces as `NU1010: PackageReference items do not define a corresponding
  PackageVersion item` for *every* package, and a broken `.runsettings` as "Settings file provided does
  not conform to required format". Writing a CLI flag in its long form inside a comment is the usual
  cause. **A restore that fails this way also makes the vulnerability scan report no vulnerabilities**,
  because there is nothing to analyse — so never trust a clean scan without a successful restore.
