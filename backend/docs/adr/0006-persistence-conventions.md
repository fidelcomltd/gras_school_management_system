# 0006 — EF Core conventions, and migrations are never auto-applied

**Status:** Accepted · **Date:** 2026-08-03

## Context

EF Core's defaults are tuned for getting started, not for a service that must stay fast and safe as it
grows. Left alone, they produce tracked reads on every query, entities serialised straight to clients,
unbounded list endpoints, and migrations applied at startup.

Each of those is fine at fifty rows and a problem at scale, and by then they are load-bearing.

## Decision

**Naming.** snake_case identifiers via `EFCore.NamingConventions`. PostgreSQL folds unquoted identifiers
to lower case, so PascalCase tables force every hand-written query, `psql` session and migration script to
double-quote everything. Worth one small single-purpose package.

**Mapping** lives in `IEntityTypeConfiguration<T>` classes, one per entity, discovered by assembly
scanning. `OnModelCreating` contains only conventions. A 400-line `OnModelCreating` is unreviewable and a
permanent merge-conflict site.

**Conventions applied to the whole model** (so a new entity gets them without opting in):
- `DateTimeOffset` → `timestamptz`. `timestamp without time zone` is banned: it silently discards the
  offset, turning an ordering bug into a data bug.
- Every `string` bounded to 256 characters unless a configuration says otherwise.
- A soft-delete global query filter for every `ISoftDeletable`, so no query can forget to exclude deleted
  rows. Including them requires `IgnoreQueryFilters()`, which is greppable — unlike its absence.
- An optimistic-concurrency token on every `IAuditableEntity` (see below).
- Audit fields maintained by an interceptor, never by hand, so they cannot disagree between code paths.

**Concurrency: an application-maintained shadow `Version` GUID, not `xmin`.** `xmin` is the better
mechanism in principle — no column, no application code — but **Npgsql's EF Core 10 provider no longer
exposes any xmin API** (verified). Mapping it by hand makes the migration try to CREATE a column named
`xmin`, which PostgreSQL rejects as colliding with the system column, and every future entity's migration
would need hand-editing. Generated migrations must not be hand-edited, so the GUID wins: 16 bytes per row
and one assignment per save, in exchange for something portable that needs no surgery.

**A filtered unique index is mandatory on a soft-deletable unique column** (`WHERE NOT is_deleted`).
Without the filter, a deleted row reserves its value forever and a user can never reuse the name of
something they deleted.

**Read-path rules** — non-negotiable, and demonstrated in `SampleRecordRepository`:
`AsNoTracking()` on every read; project to a DTO **in the query**; a **total** order before
`Skip`/`Take` (PostgreSQL guarantees no order without `ORDER BY`, so paging over a partial order returns
some rows twice and skips others); forward the `CancellationToken` everywhere.

**Pagination is mandatory**, max page size 100, and a larger request is **rejected** rather than clamped.
Silently returning 100 for a request of 500 makes a client page incorrectly while believing it read
everything. `PageRequest.Clamp()` is a defensive backstop, not the primary control.

**Compiled queries are not used.** They pay off for a hot query in a tight loop and cost readability
everywhere else; putting one in a reference slice would advertise it as the default.

**Retry-aware transactions.** The connection uses `EnableRetryOnFailure`, and EF refuses a user-initiated
transaction under a retrying execution strategy unless the whole unit is inside `strategy.ExecuteAsync`.
That is why `IUnitOfWork` takes the **whole operation** as a delegate rather than exposing
Begin/Commit/Rollback — the latter would throw the first time a retry fired.

**Migrations are never applied automatically outside Development.**

Auto-migrate at startup means: every instance in a rolling deployment races to migrate the same database;
a failed migration becomes a crash-loop rather than a failed deployment step; and nobody reads the SQL
before it runs against production data.

Intended path: generate → **read the SQL** → commit → apply as a discrete deployment step (init container
or pipeline stage) → then roll out the new version. Existing-column changes use **expand/contract** (add
nullable → backfill → constrain) in separate deployments, because an in-place tighten breaks the moment
old and new code run together — which is every rolling deployment.

## Consequences

**Good.** The fast, safe path is the default and the dangerous ones are hard to reach accidentally.
Hand-written SQL is pleasant. A new entity inherits every convention.

**Bad.** More configuration up front than `dotnet ef` gives you. The GUID concurrency token costs a
column. Repository ports mean more files than injecting a `DbContext` — the price of `Application` not
referencing EF Core. Deployment now has an ordered step that must not be skipped, which is a real
operational burden and the correct one.
