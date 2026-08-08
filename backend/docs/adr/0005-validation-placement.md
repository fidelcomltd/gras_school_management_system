# 0005 — Validation runs as a pipeline behaviour, not an endpoint filter

**Status:** Accepted — **records a deliberate deviation** · **Date:** 2026-08-03

## Context

The root `CLAUDE.md` §6 states: *"Validation via FluentValidation registered as an endpoint filter,
executed before the handler."*

The brief for this scaffold instead lists validation as step 3 of the **mediator pipeline**. The two
instructions conflict, so the conflict is resolved here explicitly rather than silently.

## Decision

Validation is a **mediator pipeline behaviour** (`ValidationBehavior`).

The deciding argument is the failure mode. An endpoint filter must be attached per endpoint:

```csharp
group.MapPost("/things", Handler).AddEndpointFilter<ValidationFilter<CreateThingCommand>>();
//                                ↑ forget this line and the endpoint is unvalidated
```

An endpoint missing that line looks completely normal in review. Nothing fails; the endpoint simply
accepts anything, and the gap is found by whoever finds it first.

As a pipeline behaviour it is **structural**: everything dispatched through `ISender` passes through it,
there is no per-endpoint opt-in, and therefore nothing to forget.

This still satisfies §6's stated *intent* — validation executes before the handler — with a stronger
guarantee. It deviates from its *letter*, which is why this ADR and an entry in `ASSUMPTIONS.md` exist.
**The orchestrator should ratify or revert it.**

### Supporting details

- **All validators run and all failures are aggregated.** A client fixing a form should not need five
  round trips to discover five bad fields. "Fail fast" here means the handler is never entered, not that
  validation stops at the first error.
- **A request with no validator passes through at runtime.** Failing closed would turn a forgotten
  validator into a production 500. Instead `ValidatorCoverageTests` fails the **build** — the mistake is
  caught where it costs nothing. Both halves are needed; either alone is insufficient.
- Constructing a failed `TResponse` needs reflection, since `TResponse` may be `Result` or `Result<T>`.
  It is resolved once per closed generic type in a static field, so the cost is paid once per request
  type per process.

## Consequences

**Good.** An unvalidated request is not expressible. Validators are unit-testable with no HTTP. The
error shape is identical for every endpoint because one behaviour produces it.

**Bad.** A deviation from the root instructions, which must be tracked until ratified. Validation does
not apply to anything that bypasses `ISender` — but nothing should, and endpoints do not. The reflection
is a small piece of cleverness that needs its comment.

## Alternatives considered

- **An endpoint filter**, as §6 says. Rejected: opt-in, therefore forgettable.
- **Both.** Rejected: two mechanisms producing the same error shape, twice the maintenance, and ambiguity
  about which one actually ran.
- **Validating inside handlers.** Rejected: duplicated in every handler, and impossible to guarantee.
