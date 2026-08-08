# 0004 — `Result` for expected failures, RFC 9457 on the wire

**Status:** Accepted · **Date:** 2026-08-03

## Context

Two questions. How does a handler report an *expected* failure — not found, conflict, forbidden, invalid
input? And what does the client see?

The default .NET answer to the first is an exception, often a custom one per case, mapped to a status
code by a global handler. It works, and it has three costs: the failure is invisible in the method
signature, a routine outcome pays for a stack unwind, and callers learn to write `catch` blocks that
also swallow genuine bugs.

## Decision

**Internally: a `Result` type.**

- `Result` / `Result<T>`, carrying an `Error(Code, Description, ErrorType)`.
- `IRequest<TResponse>` **constrains `TResponse` to `Result`**, so a request cannot be declared that
  returns a bare DTO. This is a compile-time guarantee, not a convention.
- Exceptions are reserved for genuine defects and infrastructure faults.
- Reading `.Value` on a failed result **throws**. Returning `default` would let a null or zero flow into
  business logic as if it were data — precisely the bug the type exists to prevent. `TryGetValue` is the
  non-throwing accessor.

**On the wire: RFC 9457 `application/problem+json`.**

- Status code derived from `ErrorType` in **exactly one place** (`ApiProblem.ToStatusCode`). No handler
  or endpoint names a status code, so two endpoints cannot disagree about whether a missing row is a 404.
- A stable machine-readable `errorCode`. Clients branch on it, never on the human-readable `detail`.
- `type` is a URN: `urn:schoolmanagement:error:<code>`. RFC 9457 says `type` *should* dereference to
  documentation; we do not own a docs host, and `https://example.com/errors/...` would be a URI that
  looks resolvable and is not. A URN makes no such promise and is still stable — the property clients
  actually rely on.
- `traceId` on **every** problem response, attached centrally via `CustomizeProblemDetails` so it also
  appears on framework-generated errors (a 400 from model binding, a 401 from auth middleware) that never
  pass through our result mapping.
- Validation failures return **422** with an `errors` object keyed by property name. **400** means the
  request itself was malformed.
- **No stack traces, exception types, or messages outside Development.** They reveal library versions and
  file paths, and exception messages routinely contain user data.

**Database constraint violations are translated.** `PersistenceErrors.TryTranslate` maps a unique-key
violation to the same 409 a handler would have returned. A read-then-write conflict check is racy, so the
database constraint is what actually holds the line; this makes the response identical either way. It
lives in Infrastructure so the Api layer never names an Npgsql type.

## Consequences

**Good.** Failure modes are visible in signatures. Status-code consistency is structural. Adding an
`ErrorType` member cannot silently fall through to 500 — `ApiProblemTests` enumerates the enum and fails.

**Bad.** More ceremony than throwing: a handler that calls three fallible things checks three results.
`Result<T>` is a class, so there is an allocation per outcome (irrelevant at HTTP scale). `Not-null`
constraint violations deliberately return `null` from the translator and become a 500 — they mean *our*
validation let something through, which is our bug, not the caller's.

## Alternatives considered

- **Exceptions for expected failures.** Rejected above.
- **A discriminated-union library** (OneOf, ErrorOr). More expressive, but another dependency for
  something ~120 lines covers, and C# lacks exhaustive matching, so much of the benefit is unrealised.
- **`https://` problem type URIs.** Rejected until a documentation host actually exists.
