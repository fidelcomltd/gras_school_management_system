# 0002 — Hand-rolled mediator instead of MediatR

**Status:** Accepted · **Date:** 2026-08-03

## Context

The application layer needs a mediator: a way for an endpoint to dispatch a request through
cross-cutting behaviours to exactly one handler. MediatR is the default choice in .NET and the one most
contributors will expect.

Its licensing changed. Verified against the NuGet registration API rather than recalled:

| MediatR versions | Published `licenseExpression` |
|---|---|
| 11.0.0 – 12.5.0 | `Apache-2.0` |
| 13.0.0 – 14.2.0 | *(none)* — embedded licence file, `projectUrl` now `mediatr.io` |

So the current line carries a commercial licence whose terms are a legal and procurement question, not
an engineering one.

## Decision

Hand-roll the mediator. About 150 lines:

- `IRequest<TResponse>` (constrained so `TResponse` must be a `Result`), `ICommand<T>`, `IQuery<T>`,
  `IBaseCommand`
- `IRequestHandler<TRequest, TResponse>`
- `IPipelineBehavior<TRequest, TResponse>` + `PipelineContinuation<TResponse>`
- `ISender` and `Sender`, with handler discovery by assembly scanning

`Sender` receives a request as `IRequest<TResponse>`, so the concrete type is only known at runtime. It
closes a generic executor over that type **once per request type** and caches it, so reflection is paid
once per process rather than per dispatch.

## Consequences

**Good.**
- No licensing question at the core of a scaffold every contributor copies, and no pressure on a future
  maintainer to "just bump it" across the licence boundary.
- The pipeline order is explicit, debuggable, and steppable — no third-party frames.
- `TResponse` is constrained to `Result`, which MediatR cannot express. "Handlers return a Result rather
  than throwing" is therefore a **compile-time** guarantee rather than a convention.
- Nothing to keep up to date.

**Bad.**
- ~150 lines we own and must test. Mitigated by `PipelineTests`, which asserts handler resolution,
  behaviour order, and the commands-only transaction rule.
- Contributors who know MediatR must learn slightly different names.
- No `INotification`/publish. Not needed yet; add it if a genuine case appears rather than pre-emptively.
- `Activator.CreateInstance` is not trim-safe, so Native AOT would need a source generator. Irrelevant
  for a container deployment; noted in the class.

## Alternatives considered

- **Pin MediatR 12.5.0** (last Apache-2.0). Battle-tested, but a deliberately frozen dependency that
  receives no fixes, and every future contributor is one `dotnet outdated` away from bumping it straight
  across the licence boundary.
- **MediatR 14.2.0.** Current and maintained; requires accepting commercial terms. A decision for the
  business, not for a scaffold to make silently.
- **A community fork.** Trades a known licensing question for unknown maintenance risk.
