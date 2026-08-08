# AGENTS.md — instructions for AI contributors

Also applies to humans. This file assumes **no memory of any previous conversation**.

Read this before writing code. If it conflicts with what you were told elsewhere, say so rather than
picking one silently.

---

## 1. What this repository is

A .NET 10 REST API scaffold at `backend/`. Layered, dependency-inverted, mediator-based application
layer. **No business logic exists yet, on purpose.**

The domain vocabulary has **not been decided**. Do not invent `Student`, `Enrolment`, `Course` or any
other entity because it seems obvious. If a task requires one and none is specified, **stop and ask**.
Product decisions are not yours to make; see `docs/ASSUMPTIONS.md`.

---

## 2. Non-negotiables

| Rule | Consequence of breaking it |
|---|---|
| Never edit `../contracts/openapi.json` by hand | It is build output. Regenerate: `./scripts/generate-openapi.ps1 -Promote` |
| Never commit a secret — not even a realistic placeholder | Secret-scan gate fails; a leak cannot be undone, only rotated |
| Never edit a file under `Persistence/Migrations/` | Generated. Add a new migration instead |
| Never reference EF Core from `Application` | `DependencyDirectionTests` fails |
| Never add a package version to a `.csproj` | Versions live only in `Directory.Packages.props` |
| Never use `DateTime.UtcNow` / `DateTimeOffset.UtcNow` | Inject `TimeProvider`. `SourceConventionTests` fails |
| Never call `.Result` or `.Wait()` on a task | `SourceConventionTests` fails. Await it |
| Never write `logger.LogInformation($"...")` | `CA1848` is an error. Add a `[LoggerMessage]` method |
| Never write a `TODO` without `(TASK-####)` | `SourceConventionTests` fails |
| Never catch an exception in a handler | The global handler owns that. A `catch` hides bugs |
| Never suppress an analyser without an inline justification | Rejected in review |

**Warnings are errors.** The build fails on any warning. This is not negotiable and not configurable
per project.

---

## 3. Where each kind of file goes

| You are writing | Put it in |
|---|---|
| An entity, value object, domain rule | `src/SchoolManagement.Domain/<Feature>/` |
| A request + its response DTO + its validator | `src/SchoolManagement.Application/<Feature>/<UseCase>.cs` |
| A handler | `src/SchoolManagement.Application/<Feature>/<UseCase>Handler.cs` |
| A persistence port (interface) | `src/SchoolManagement.Application/<Feature>/I<Thing>Repository.cs` |
| Its EF Core implementation | `src/SchoolManagement.Infrastructure/Persistence/Repositories/` |
| An entity's table mapping | `src/SchoolManagement.Infrastructure/Persistence/Configurations/` |
| HTTP endpoints | `src/SchoolManagement.Api/Endpoints/<Feature>Endpoints.cs` |
| A cross-cutting pipeline stage | `src/SchoolManagement.Application/Behaviors/` |
| A unit test | `tests/SchoolManagement.UnitTests/<Layer>/` |
| An integration test | `tests/SchoolManagement.IntegrationTests/` |

**Keep a slice's files together.** The unit of work is the feature, not the layer.

---

## 4. THE RECIPE: adding an endpoint

Copy the reference slice. It exists for exactly this purpose:

- `src/SchoolManagement.Application/Reference/Ping/` — the simplest query, no database
- `src/SchoolManagement.Application/Reference/SampleRecords/` — a command and a paginated query
- `src/SchoolManagement.Api/Endpoints/ReferenceEndpoints.cs` — how endpoints are declared

### Step 1 — the request, response and validator

One file, in `Application/<Feature>/`:

```csharp
/// <summary>What this does, in one line. THIS BECOMES THE OPENAPI DESCRIPTION.</summary>
/// <param name="Label">What this field means.</param>
public sealed record CreateThingCommand(string Label) : ICommand<Result<CreateThingResponse>>;

/// <summary>The response.</summary>
/// <param name="Id">The new thing's opaque identifier.</param>
public sealed record CreateThingResponse(string Id);

internal sealed class CreateThingCommandValidator : AbstractValidator<CreateThingCommand>
{
    public CreateThingCommandValidator() =>
        RuleFor(command => command.Label).NotEmpty().MaximumLength(Thing.LabelMaxLength);
}
```

- `ICommand<T>` if it CHANGES state (it gets a transaction). `IQuery<T>` if it only READS.
- `TResponse` must be `Result` or `Result<T>`. The compiler enforces this.
- Records, always: immutable and value-equal.
- **A validator is mandatory**, even an empty one. `ValidatorCoverageTests` fails otherwise. An empty
  validator records that somebody considered the question.
- Reference domain constants for lengths (`Thing.LabelMaxLength`), never a literal — the validator,
  the entity invariant and the column length must not drift apart.

### Step 2 — the handler

```csharp
internal sealed class CreateThingHandler(IThingRepository repository)
    : IRequestHandler<CreateThingCommand, Result<CreateThingResponse>>
{
    public async Task<Result<CreateThingResponse>> HandleAsync(
        CreateThingCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await repository.LabelExistsAsync(request.Label, cancellationToken))
        {
            return Result.Failure<CreateThingResponse>(
                Error.Conflict("thing.label_taken", "A thing with that label already exists."));
        }

        var creation = Thing.Create(Guid.CreateVersion7(), request.Label);
        if (creation.IsFailure)
        {
            return Result.Failure<CreateThingResponse>(creation.Error);
        }

        await repository.AddAsync(creation.Value, cancellationToken);
        return Result.Success(new CreateThingResponse(creation.Value.Id.ToString("D", CultureInfo.InvariantCulture)));
    }
}
```

- `internal sealed`. Enforced.
- **No `SaveChangesAsync`.** The unit-of-work behaviour commits on success and rolls back on failure.
- **No input validation.** The pipeline already guaranteed the request is valid.
- **No try/catch.** The global handler turns exceptions into a 500 with a `traceId`.
- **No status codes.** Return a `Result`; `ErrorType` decides the status centrally.
- Forward the `CancellationToken` to everything.
- Use `TimeProvider` if you need the time. Never `DateTimeOffset.UtcNow`.

### Step 3 — the endpoint

In `Api/Endpoints/<Feature>Endpoints.cs`, implementing `IEndpointModule`. It is discovered
automatically — there is no registration list to update.

```csharp
group.MapPost("/things", async (
        CreateThingCommand command,
        ISender sender,
        CancellationToken cancellationToken) =>
    {
        var result = await sender.SendAsync(command, cancellationToken);
        return result.Match(response => TypedResults.Created($"/api/v1/things/{response.Id}", response));
    })
    .WithName("CreateThing")
    .WithSummary("Create a thing")
    .WithDescription("Longer prose. Explain the rules a client needs to know.")
    .Produces<CreateThingResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status500InternalServerError);
```

- The body only dispatches and matches. No logic.
- **Declare every response type.** The committed contract is generated from this metadata; an
  undocumented response becomes an untyped client.
- Authorisation: endpoints are protected **by default**. Add `.AllowAnonymous()` only when public
  access is intended, and mean it.
- Route templates here are relative; the group supplies `/api/v{version}`.

### Step 4 — register the repository

If you added a port, wire it in `InfrastructureDependencyInjection.AddInfrastructure`.

### Step 5 — add the OpenAPI example

Add an entry for every new DTO in `Api/OpenApi/OpenApiExamples.cs`. `OpenApiContractTests` fails
without one. Property examples are derived from the object example automatically — declare it once.

### Step 6 — tests. NOT OPTIONAL

A task is **not done** until its tests exist and pass.

- Unit test the handler (construct it directly — see `PingQueryHandlerTests`).
- Unit test the validator at its **boundaries**, not just the obvious rejection.
- Integration test the endpoint: happy path, validation failure, unauthorized, not-found.

### Step 7 — regenerate the contract

```bash
./scripts/generate-openapi.ps1 -Promote
```

### Step 8 — run the gates

```bash
./scripts/ci.ps1
```

---

## 5. Definition of Done

A task is done only when **all** of these hold. Do not report completion otherwise.

- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test` — all pass. Integration tests SKIPPED is **not** the same as passed; say so
- [ ] `dotnet format --verify-no-changes` — clean
- [ ] Every new request has a validator; every new DTO has an OpenAPI example
- [ ] Tests exist for the new behaviour, asserting outcomes rather than implementation details
- [ ] Contract regenerated if any endpoint or DTO changed
- [ ] No new `TODO`/`FIXME` without a `TASK-####` reference
- [ ] No secret, credential, or real connection string anywhere in the tree
- [ ] **Gate output pasted into your report.** "Should pass" is not a result

If you cannot finish something, say exactly what and why. A partial change reported as complete costs
far more than an honest blocker.

---

## 6. Reporting

State: files changed (grouped by project), contract impact (`unchanged` / `additive` / `BREAKING`),
real gate output, anything deliberately left undone, and any assumption you made.

If a requirement was ambiguous, **name the ambiguity and the assumption you proceeded under**. Do not
resolve a product question by guessing quietly.
