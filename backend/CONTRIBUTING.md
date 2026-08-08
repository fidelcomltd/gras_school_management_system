# Contributing

The conventions, stated as rules **with the reason attached**. A rule whose reason is forgotten gets
deleted the first time it is inconvenient, so the reason is the important half.

Wherever a rule could be expressed as an analyser rule or an architecture test, it **is** one — see
[Enforcement](#enforcement). Documentation is the fallback, not the mechanism.

For the mechanical recipe ("how do I add an endpoint"), see [AGENTS.md](AGENTS.md).

---

## Layering

```
Api → Application → Domain
Infrastructure → Application → Domain
nothing → Api
```

- **`Domain` has zero dependencies.** Not EF Core, not ASP.NET Core, not even
  `Microsoft.Extensions.*`. The moment it takes one, business rules can only be exercised by starting
  infrastructure, and the tests that matter most become the slowest to run.
- **`Application` must not reference EF Core.** It declares ports (`IUnitOfWork`,
  `ISampleRecordRepository`) and Infrastructure implements them. Exposing `DbSet<T>` from an interface
  is the popular shortcut here; it drags the change tracker and query-provider semantics into the
  use-case layer and makes every handler's behaviour depend on EF's.
- **`Api` is the composition root.** It may reference everything; nothing may reference it.

*Enforced by `DependencyDirectionTests`.*

---

## The error model

**Handlers return `Result`; they do not throw for outcomes you can anticipate.**

Not found, conflict, forbidden, invalid input — all `Result` failures. Exceptions are for genuine
defects and infrastructure faults.

An expected failure travelling as an exception is invisible in the method signature, costs a stack
unwind on a routine path, and tempts callers into `catch` blocks that swallow real bugs.

`IRequest<TResponse>` constrains `TResponse` to `Result`, so this is a **compile-time** guarantee.

**Status codes are chosen in exactly one place** — `ApiProblem.ToStatusCode`, from the error's
`ErrorType`. No handler or endpoint names a status code. This is what stops two endpoints disagreeing
about whether a missing row is a 404 or a 400.

- **422** — the request parsed but failed a rule.
- **400** — the request itself was malformed (unparseable JSON, wrong query-parameter type). Produced
  by the framework before our code runs.

*See [docs/adr/0004-error-model.md](docs/adr/0004-error-model.md). Enforced by `ApiProblemTests`.*

---

## The mediator pipeline

Behaviour order is set by registration order in `ApplicationDependencyInjection.AddApplication`.
**First registered is outermost.**

1. `UnhandledExceptionBehavior` — observes failures in every stage below
2. `RequestLoggingBehavior` — records the attempt, duration and outcome
3. `ValidationBehavior` — rejects bad input before anything expensive
4. `PerformanceBehavior` — times the handler and its database work
5. `UnitOfWorkBehavior` — **commands only**, innermost

Reordering these changes behaviour silently, so `PipelineTests` asserts the exact sequence. If that
test fails because you reordered them, make sure you meant to.

"Commands only" is enforced by a **generic constraint**, not a runtime check: `UnitOfWorkBehavior` is
constrained to `IBaseCommand`, so the container cannot construct it for a query. There is no
`if (request is ICommand)` to get wrong.

**Validation is a pipeline behaviour, not an endpoint filter.** A filter must be attached per
endpoint, so the failure mode is "somebody adds an endpoint and forgets" — producing an unvalidated
endpoint that looks entirely normal in review. As a behaviour it is structural and there is no way to
opt out. This deviates from the wording of the root `CLAUDE.md` §6 and is recorded in
[docs/ASSUMPTIONS.md](docs/ASSUMPTIONS.md).

---

## Persistence

- **`AsNoTracking()` on every read.** A tracked read pays for change-tracking snapshots of data nobody
  will modify and keeps it alive for the rest of the request.
- **Project to a DTO in the query.** Loading entities and mapping them afterwards moves every column
  over the wire; it is the origin of most "it got slow as the table grew" problems.
- **Never expose an entity over HTTP, and never accept one as a request body.** It leaks the database
  shape into the contract and turns any schema change into a breaking API change.
- **Always order totally before `Skip`/`Take`.** PostgreSQL guarantees no order without `ORDER BY`, so
  paging over a non-deterministic order returns some rows twice and skips others. Tie-break on the key.
- **Every collection endpoint is paginated**, max page size 100. There is no unpaginated variant and
  there must never be one: it works fine on fifty rows and takes the service down at fifty thousand.
  A too-large `pageSize` is **rejected**, not clamped — silently returning fewer rows makes a client
  page incorrectly while believing it read everything.
- **Mapping lives in `IEntityTypeConfiguration<T>` classes**, one per entity, discovered by scanning. A
  400-line `OnModelCreating` is unreviewable and a permanent merge-conflict site.
- **Migrations are generated, never hand-edited**, and never auto-applied outside Development.
- **Schema changes to an existing column use expand/contract**: add nullable → backfill → constrain,
  in separate deployments. An in-place tighten breaks the moment old and new code run together, which
  is every rolling deployment.
- `AsSplitQuery()` when including two or more collections, or the join is a cartesian explosion.
  Projection usually beats both.

*See [docs/adr/0006-persistence-conventions.md](docs/adr/0006-persistence-conventions.md).*

---

## Time, logging and PII

- **`TimeProvider`, injected. Never `DateTimeOffset.UtcNow`.** Ambient time is what makes a suite that
  passes all day fail at midnight, and a month-boundary bug impossible to reproduce. Tests use
  `FakeTimeProvider`. *Enforced by `SourceConventionTests`.*
- **Timestamps are UTC `DateTimeOffset`** on the wire, ISO-8601 with offset. Convert to a local zone in
  the UI only.
- **All logging goes through source-generated `[LoggerMessage]` methods.** `CA1848` is an error, so
  interpolated log calls will not compile. The generated code allocates nothing when the level is
  disabled and — more importantly — forces every value into a **named** field, which is what makes logs
  queryable.
- **PII never enters logs, traces or error reports.** The pipeline logs request *type names*, never
  request contents. Redaction at the Serilog sink is a backstop for mistakes, not permission.
- **Money is never a float.** Minor units as an integer, or `decimal` with an explicit currency code.
- **IDs are opaque strings** on the wire, whatever they are in the database.
- **Enums cross the wire as strings**, and clients must tolerate unknown members.

---

## Testing

**A task is not done until its tests exist and pass.** Tests are not a follow-up ticket.

- **Unit** — construct the thing directly. No DI container, no `WebApplicationFactory`, no database. If
  a class is hard to test this way, it has too many dependencies.
- **Integration** — the real application over a real PostgreSQL via Testcontainers. **The EF Core
  in-memory provider is banned**: it does not enforce constraints or translate SQL, so it passes queries
  that cannot execute. It hides exactly the bugs an integration test exists to find.
- **Architecture** — conventions, executable.
- Assert **user-visible behaviour**, not implementation details. A test that asserts a mock was called
  twice documents the current code rather than the required behaviour, and it fails on every harmless
  refactor.
- Deterministic data. Fixed seeds or explicit builders. No `Thread.Sleep`, no shared mutable state.
- Test **boundaries**: `MaxLength` and `MaxLength + 1`, page 0 and page 1, empty and one and many.

### If a database is unavailable, integration tests SKIP

They never pass silently and never fall back to a weaker provider. A skipped test is visibly absent
from the run; a test that quietly substituted something else reports success having verified almost
nothing.

---

## Dependencies

- **Versions live only in `Directory.Packages.props`.** A version in a `.csproj` is a build error.
  This makes drift between projects structurally impossible.
- A new package needs a **justification** in the task card or PR description. Prefer the framework.
- **Nothing floats.** No wildcards, no `latest`. A build must produce the same result next month.
- One analyser package set (Roslynator). Stacking three produces contradictory advice and trains people
  to ignore all of it.

---

## Enforcement

Prefer a mechanism over a paragraph. These rules are checked automatically:

| Rule | Enforced by |
|---|---|
| Dependency direction; `Application` has no EF Core | `DependencyDirectionTests` |
| Handlers `internal sealed`; requests are records | `HandlerConventionTests` |
| Every request has a validator; every request returns `Result` | `ValidatorCoverageTests` |
| No ambient `DateTime`; no `.Result`/`.Wait()`; no `async void`; no untracked `TODO` | `SourceConventionTests` |
| Behaviour order; queries get no transaction; every request resolves a handler | `PipelineTests` |
| Every `ErrorType` maps to a deliberate status code | `ApiProblemTests` |
| Every operation and schema documented, with examples | `OpenApiContractTests` |
| Endpoints protected by default | `SecurityAndErrorContractTests` |
| Formatting, naming, code style | `.editorconfig` + `dotnet format` |
| Structured logging, async correctness, security rules | .NET analysers, warnings as errors |
| No committed secrets | `.gitleaks.toml` + the CI secret-scan gate |
| Committed contract matches the code | the CI contract-drift gate |

**If you find a rule you disagree with, change it deliberately** — edit the rule and its test, and say
why in the PR. Do not work around it locally, and do not suppress an analyser without an inline
justification comment. A silent local exception is how a convention becomes folklore.

---

## Commits and pull requests

- Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`.
- One logical change per commit.
- Reference the task card in the footer: `Refs: TASK-0042`.
- Run `./scripts/ci.ps1` before pushing. It is the same script CI runs.
- If any endpoint or DTO changed, regenerate the contract and include the diff. A removed or renamed
  field, a narrowed type, or a changed status code is **breaking** and needs human sign-off.
