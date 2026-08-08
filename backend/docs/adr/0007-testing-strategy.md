# 0007 — Real PostgreSQL via Testcontainers; the in-memory provider is banned

**Status:** Accepted · **Date:** 2026-08-03

## Context

Integration tests need a database. The convenient option is EF Core's in-memory provider: fast, no
dependencies, no setup.

It is also not a relational database. It does not enforce unique constraints, foreign keys, check
constraints or `NOT NULL`. It does not translate LINQ to SQL, so a query that no provider could translate
passes. It has different null and case-sensitivity semantics, and no transaction behaviour worth the name.

So it passes exactly the tests you most need to fail: the ones that would catch a broken constraint, an
untranslatable query, or a concurrency bug.

At the time of writing, **Docker is not installed on the development machine**, which forces a second
decision: what should the tests do when no database is available?

## Decision

**Integration tests run against real PostgreSQL** (`postgres:17.6-alpine`, pinned — a floating tag means
the suite can break because someone published an image). Provided by Testcontainers, or by
`POSTGRES_TEST_CONNECTION` if set, which takes priority and is how CI supplies a service container.

**The in-memory provider is not used anywhere.**

**When no database is reachable, the tests SKIP — loudly.** They report `Skipped`, never `Passed`, and the
skip message names the prerequisite and both ways to satisfy it.

This is the important decision. The alternatives were:

- *Fail* when no database is present. Honest, but it makes `dotnet test` impossible for a contributor
  without Docker, so the practical result is people stop running tests at all.
- *Silently fall back to in-memory.* The worst option, and the tempting one: the suite reports success
  having verified almost nothing, and now actively lies.
- *Skip.* A skipped test is visibly absent from the run and counted separately. It cannot be mistaken for
  a pass by anyone reading the output.

**CI must not be allowed to skip them.** A service container supplies `POSTGRES_TEST_CONNECTION`, and a
dedicated workflow step parses the `.trx` and **fails if anything was skipped**. Otherwise a
misconfigured container would quietly shrink the suite to unit tests while still going green — which is
the exact failure this design is otherwise vulnerable to.

**Three test projects, three jobs:**
- **Unit** — construct the class directly. No container, no DI, no database. If that is hard, the class
  has too many dependencies.
- **Integration** — the real application via `WebApplicationFactory`, real middleware order, real
  pipeline, real EF Core, real migrations. Nothing stubbed; a test that replaces any of it is testing a
  different application.
- **Architecture** — the conventions, executable. Including source-text scans for rules the type graph
  cannot express (`DateTime.UtcNow`, `.Result`, `async void`).

**Isolation** is per test by `TRUNCATE`, not by re-migrating: fast enough to call every test, and it keeps
every constraint in place.

**Migrations are applied by the fixture**, not by the application at startup — because production applies
them as a deliberate step, so the test does what the deployment does.

## Consequences

**Good.** A passing integration test means something. Constraint violations, untranslatable queries and
concurrency conflicts are caught. Contributors without Docker can still build and run 100 unit and
architecture tests.

**Bad.** Integration tests are seconds, not milliseconds. They need a container runtime or a database.
And the honest cost: **on a machine without either, the "runs end to end against a real database"
guarantee is unverified** — which is why it is listed as blocking in `ASSUMPTIONS.md` rather than quietly
assumed.
