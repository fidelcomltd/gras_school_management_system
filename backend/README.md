# SchoolManagement — Backend API

A .NET 10 REST API scaffold: layered, dependency-inverted, with a mediator-based application layer.

**There is no business logic here yet, and that is deliberate.** This repository provides the
structure, conventions and quality gates that feature work slots into. The domain vocabulary has not
been decided (see [docs/ASSUMPTIONS.md](docs/ASSUMPTIONS.md)), so inventing entities would be guessing
at a product.

- Adding an endpoint? Read [AGENTS.md](AGENTS.md) — it is the step-by-step recipe.
- Changing how things are done? Read [CONTRIBUTING.md](CONTRIBUTING.md) — the rules, with reasons.
- Wondering why something is the way it is? Read [docs/adr/](docs/adr/).

---

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.100+ | Pinned in [global.json](global.json). `dotnet --version` to check. |
| PostgreSQL | 17.x | Any reachable instance. Not needed to build or to run unit tests. |
| Container runtime | any | Docker Desktop or Podman. Optional — only for the integration tests. |
| gitleaks | 8.x | Optional — only for the local secret-scan gate. |

Nothing else. No global tools to install: `dotnet-ef` is pinned as a local tool in
[dotnet-tools.json](dotnet-tools.json) and restored by `dotnet tool restore`.

---

## Ten-minute start

```bash
cd backend

# 1. Restore packages and the local dotnet-ef tool.
dotnet restore
dotnet tool restore

# 2. Build. No database needed.
dotnet build

# 3. Run the tests. No database needed — the integration tests will SKIP and say so.
dotnet test
```

That should be green. To run the service you need a database:

```bash
# 4. Point the app at PostgreSQL. NEVER put this in a file — user-secrets live outside the repo.
dotnet user-secrets set "Database:ConnectionString" \
  "Host=localhost;Port=5432;Database=schoolmanagement;Username=YOUR_USER;Password=YOUR_PASSWORD" \
  --project src/SchoolManagement.Api

# 5. Create the schema.
export SCHOOLMANAGEMENT_DESIGNTIME_CONNECTION="<the same connection string>"
dotnet ef database update \
  --project src/SchoolManagement.Infrastructure \
  --startup-project src/SchoolManagement.Infrastructure

# 6. Optional: copy the config template for local overrides. It is git-ignored.
cp src/SchoolManagement.Api/appsettings.Development.template.json \
   src/SchoolManagement.Api/appsettings.Development.json

# 7. Run.
dotnet run --project src/SchoolManagement.Api
```

Then:

| What | Where |
|---|---|
| Interactive API docs | `https://localhost:<port>/docs` (Development only) |
| OpenAPI document | `https://localhost:<port>/openapi/v1.json` (Development only) |
| Reference endpoint | `GET /api/v1/reference/ping?name=Ada` |
| Liveness probe | `GET /health/live` |
| Readiness probe | `GET /health/ready` |

`GET /api/v1/reference/whoami` returns **401 by design** — see *Authentication* below.

### No local PostgreSQL?

With a container runtime:

```bash
docker run --name schoolmanagement-db -e POSTGRES_PASSWORD=<choose-one> \
  -e POSTGRES_DB=schoolmanagement -p 5432:5432 -d postgres:17.6-alpine
```

Choose your own password and put it only in user-secrets. This repository contains no working
credentials, not even for local development — a placeholder that looks real is the thing that
eventually gets copied into a deployment.

---

## Project layout

```
backend/
├── src/
│   ├── SchoolManagement.Domain           entities, value objects, Result/Error. ZERO dependencies.
│   ├── SchoolManagement.Application      mediator contracts, handlers, behaviours, validators, DTOs
│   ├── SchoolManagement.Infrastructure   EF Core, repositories, interceptors, secret provider
│   └── SchoolManagement.Api              endpoints, DI composition, OpenAPI, middleware
├── tests/
│   ├── SchoolManagement.UnitTests         fast, no I/O
│   ├── SchoolManagement.IntegrationTests  real app + real PostgreSQL via Testcontainers
│   └── SchoolManagement.ArchitectureTests conventions enforced mechanically
├── docs/adr/                              architecture decision records
├── scripts/                               ci.ps1, generate-openapi.ps1
├── ci/                                    workflow to be moved to the repo root by the orchestrator
├── Directory.Build.props                  repo-wide compiler and analyser settings
├── Directory.Packages.props               THE ONLY place a package version may appear
└── .editorconfig                          formatting + analyser severities (a quality gate)
```

**Dependency rule:** `Api → Application → Domain`, `Infrastructure → Application → Domain`, and
nothing depends on `Api`. `Application` must never reference EF Core.
Enforced by `DependencyDirectionTests`, not by review.

---

## Running the tests

```bash
dotnet test                                    # everything
dotnet test tests/SchoolManagement.UnitTests   # fast, no I/O
```

**Unit** — handlers, validators, behaviours, mapping. No database, no HTTP.
**Architecture** — dependency direction, naming, "every request has a validator", no ambient
`DateTime.UtcNow`, no blocking on tasks. These are the conventions, executable.
**Integration** — the real application over a real PostgreSQL.

### The integration tests will SKIP unless a database is reachable

They report `Skipped`, never `Passed`, and they never fall back to the EF Core in-memory provider —
it does not enforce constraints or translate SQL, so it would pass queries that cannot actually run.
A skipped test is visibly absent; a test that quietly used a weaker database would report success
while verifying almost nothing.

Two ways to make them run:

```bash
# Either: point them at any PostgreSQL (this is what CI does, with a service container).
export POSTGRES_TEST_CONNECTION="Host=localhost;Port=5432;Database=schoolmanagement_tests;Username=...;Password=..."

# Or: install Docker Desktop / Podman. Testcontainers then starts and destroys one automatically,
# with no configuration at all.
```

---

## Quality gates

One command runs every gate — the same one CI runs, so "it works on my machine" means something:

```bash
./scripts/ci.ps1
```

| Gate | Command |
|---|---|
| Build, warnings as errors | `dotnet build -warnaserror` |
| Formatting and code style | `dotnet format --verify-no-changes` |
| Tests + coverage | `dotnet test --settings coverlet.runsettings` |
| Coverage floor (60%) | parsed from the cobertura report |
| Vulnerable dependencies | `dotnet list package --vulnerable --include-transitive` |
| Secret scan | `gitleaks detect --config .gitleaks.toml` |
| OpenAPI contract drift | regenerate, compare hashes |

The coverage number is a **floor, not a goal**. It catches a collapse — a deleted test project, a
large untested subsystem landing at once. Chasing it produces tests that execute code without
asserting anything about it, which is worse than no test because it looks like cover.

---

## Configuration and secrets

Precedence, lowest to highest — later wins:

```
appsettings.json  →  appsettings.{Environment}.json  →  user-secrets (dev only)  →  environment variables
```

**No secret is ever committed.** Not a real one, not a placeholder that looks real.

- **Locally:** `dotnet user-secrets`, which stores values in your user profile, outside the repository.
- **Deployed:** environment variables, or a secret store's configuration provider registered in
  `Program.cs`. See [docs/adr/0009-secret-store.md](docs/adr/0009-secret-store.md).

[`appsettings.Development.template.json`](src/SchoolManagement.Api/appsettings.Development.template.json)
is committed and documents **every** key the service reads, with every value empty or non-secret.

### These keys are secrets

| Key | Notes |
|---|---|
| `Database:ConnectionString` | Contains credentials. Required; startup fails without it. |
| any future `*:ApiKey`, `*:ClientSecret`, `*:Token` | Same rule. Add to `.gitleaks.toml` if a new shape appears. |

Configuration is validated at **startup** (`ValidateOnStart`), so a bad value stops the process at
boot with a message naming the key — rather than surfacing as a 500 on whichever request first
touches it, possibly hours after deployment.

---

## Authentication

**Not yet implemented, and that is a pending human decision** (root `CLAUDE.md` §5 requires sign-off).

What exists is the correct *shape*:

- Endpoints are **protected by default** via an authorisation fallback policy. Forgetting to secure a
  new endpoint yields 401, not public access. Anonymous access is opted into explicitly with
  `.AllowAnonymous()`, which is greppable and visible in review.
- Named policies in `AuthorizationPolicies`; no role strings scattered through endpoints.
- A placeholder authentication scheme that authenticates nobody, so the authorisation pipeline is
  genuinely exercised (and tested) before an identity provider is chosen.

The application **refuses to start** with the placeholder scheme outside Development. Choosing the
mechanism is tracked in [docs/ASSUMPTIONS.md](docs/ASSUMPTIONS.md).

---

## Adding a migration

Migrations are **never applied automatically at startup** outside Development — see
[docs/adr/0006-persistence-conventions.md](docs/adr/0006-persistence-conventions.md) for why, and for
the intended deployment path.

```bash
# The design-time tooling needs a connection string. None is committed, on purpose: a committed
# default is the file somebody edits to point at a real database. A scratch database is fine —
# the generated SQL depends only on the model and the provider, never on the data.
export SCHOOLMANAGEMENT_DESIGNTIME_CONNECTION="Host=localhost;Port=5432;Database=schoolmanagement_designtime"

dotnet ef migrations add <DescriptiveName> \
  --project src/SchoolManagement.Infrastructure \
  --startup-project src/SchoolManagement.Infrastructure \
  --output-dir Persistence/Migrations

# READ THE GENERATED SQL BEFORE COMMITTING:
dotnet ef migrations script --idempotent \
  --project src/SchoolManagement.Infrastructure \
  --startup-project src/SchoolManagement.Infrastructure

# Apply locally.
dotnet ef database update \
  --project src/SchoolManagement.Infrastructure \
  --startup-project src/SchoolManagement.Infrastructure
```

Changing an existing column follows **expand/contract** — add nullable, backfill, then constrain — in
separate deployments. An in-place tighten fails the moment old and new code run at once, which is
exactly what happens during any rolling deployment.

---

## Regenerating the OpenAPI contract

`../contracts/openapi.json` is **build output**. It is never hand-edited (root `CLAUDE.md` §3): the
frontend generates a typed client from it, so a hand edit produces a client that does not match the
server.

```bash
# Generate for inspection. Writes to artifacts/openapi/ (git-ignored); the contract is untouched.
./scripts/generate-openapi.ps1

# Update the committed contract and CONTRACT.lock. Review the diff before committing.
./scripts/generate-openapi.ps1 -Promote
```

The document is produced by **starting the application** and reading its route metadata, which is
what makes it impossible to fake. Because startup validation would otherwise demand a configured
database, the script sets `SCHOOLMANAGEMENT_CONTRACT_GENERATION`, which skips only the startup checks
that need live infrastructure. Nothing else changes and no connection is opened — see the `HostMode`
class for why that is a flag and not a dummy credential.

A removed or renamed field, a narrowed type, or a changed status code is a **breaking** change and
needs human sign-off.

---

## Observability

- **Logs** — Serilog, structured, via source-generated `[LoggerMessage]` methods (`CA1848` is an
  error, so `logger.LogInformation($"...")` will not compile). Every record carries `TraceId`.
  Property names that look like credentials are redacted at the sink as a backstop.
- **Traces and metrics** — OpenTelemetry for ASP.NET Core, HttpClient and Npgsql. Exported over OTLP
  only when `Observability:OtlpEndpoint` is set; empty means console-only, which is the right local
  default.
- **Correlation** — `X-Correlation-Id` is accepted, validated (never echoed unvalidated — it reaches
  a response header and the logs), attached to the trace, and returned.
- **Errors** — RFC 9457 `application/problem+json` with a stable `errorCode` and a `traceId`. No
  stack traces outside Development.

---

## Deployment shape

Container, per [docs/adr/0008-deployment-target.md](docs/adr/0008-deployment-target.md).

1. `docker build` the Api project (no Dockerfile yet — see ASSUMPTIONS).
2. Supply configuration through environment variables, including `Database__ConnectionString`.
3. Apply migrations as a **separate step** before rolling out the new version.
4. Point the orchestrator's liveness probe at `/health/live` and readiness at `/health/ready`.
   Do not point liveness at `/health/ready`: a brief database outage would then fail liveness on
   every instance and the orchestrator would restart the whole fleet.
