# Backend specification — CLAUDE.md §6

This file **is** §6 of the root CLAUDE.md. It was moved out of that file on 2026-09-04 so that
only `backend-dev` loads it; every reference to "§6" anywhere in the repo means this document.
Binding in full. Concrete recipes that implement it: `backend/AGENTS.md`. Accepted deviations:
`backend/docs/ASSUMPTIONS.md`.


**Structure & style**
- Minimal APIs grouped per feature (`MapGroup`), endpoints thin — no business logic in the handler.
- Layering: `Api → Application → Domain`, `Infrastructure` implements `Application` interfaces. `Domain` references nothing.
- `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<ImplicitUsings>enable</ImplicitUsings>`, analyzers on, `.editorconfig` committed.
- `record` types for DTOs; entities are classes with private setters and no public parameterless constructors where avoidable.

**HTTP semantics**
- `TypedResults` everywhere (never bare `Results.Ok` with an untyped body) so the OpenAPI document is accurate.
- Errors return RFC 9457 `ProblemDetails` with a stable machine-readable `type`/error code. A global exception handler maps domain exceptions → status codes; handlers never catch broadly.
- Correct codes: `201` + `Location` on create, `204` on delete/no-body update, `409` on conflict, `422` on validation failure (or `400` — pick one and be consistent), `429` when rate-limited.
- Collection endpoints are **always** paginated with a consistent envelope (`items`, `nextCursor` or `page`/`pageSize`/`total`). No unbounded list endpoints.
- Mutating endpoints that can be retried accept an `Idempotency-Key`.

**Data**
- EF Core with PostgreSQL. Explicit migrations, reviewed, never auto-applied on startup in production.
- `AsNoTracking()` for all read paths; projections to DTO in the query — never load entities to map them in memory.
- No lazy loading. Explicit `Include` or projection.
- Never expose entities over HTTP. Never accept entities as request bodies.
- Schema changes affecting an existing column follow the expand/contract pattern (add nullable → backfill → constrain), not an in-place tighten.

**Cross-cutting**
- `CancellationToken` accepted and propagated on every async path. `async` all the way down; no `.Result`, no `.Wait()`, no `async void`.
- Validation via FluentValidation registered as an endpoint filter, executed before the handler.
- Options pattern with `ValidateOnStart()`; no `IConfiguration` string indexing in business code.
- Structured logging (Serilog or `ILogger` with message templates — never string interpolation into log messages). OpenTelemetry traces/metrics. Correlation ID propagated from an inbound header and returned in the response.
- No secrets in `appsettings.json`. User secrets locally, environment/secret store in deployment.
- Health checks: `/health/live` and `/health/ready` (ready includes DB).
- Rate limiting middleware on public endpoints.

**Testing**
- xUnit. Integration tests via `WebApplicationFactory` against a real Postgres (Testcontainers), not an in-memory provider.
- Every endpoint gets at least: happy path, validation failure, unauthorized, not-found.
- Unit tests cover domain rules and use-case branching. No tests asserting on mock call counts as a substitute for behaviour.


