# Architecture Decision Records

One file per significant decision: the context, what was decided, and what it costs. They exist so a
future contributor can tell a *deliberate* choice from an accident, and can reverse one knowingly.

**Never edit an accepted ADR's decision.** Supersede it with a new one and mark the old as superseded —
the point of the record is the history.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-quality-gates.md) | Warnings are errors; conventions enforced by tooling | Accepted |
| [0002](0002-mediator.md) | Hand-rolled mediator instead of MediatR | Accepted |
| [0003](0003-api-style-and-versioning.md) | Minimal APIs, URL-segment versioning | Accepted |
| [0004](0004-error-model.md) | `Result` for expected failures; RFC 9457 on the wire | Accepted |
| [0005](0005-validation-placement.md) | Validation as a pipeline behaviour, not an endpoint filter | Accepted (deviation) |
| [0006](0006-persistence-conventions.md) | EF Core conventions; migrations never auto-applied | Accepted |
| [0007](0007-testing-strategy.md) | Real PostgreSQL via Testcontainers; in-memory provider banned | Accepted |
| [0008](0008-deployment-target.md) | Container deployment | Provisional |
| [0009](0009-secret-store.md) | Provider seam; environment variables for now | Provisional |

**Provisional** means: implemented, works, and awaiting a human decision that could change it. See
[../ASSUMPTIONS.md](../ASSUMPTIONS.md).
