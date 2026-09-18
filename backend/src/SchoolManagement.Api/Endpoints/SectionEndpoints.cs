using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Classes;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The admin-editable section list (TASK-0038; spec 6.4.2, 6.4.9). Gated under <c>level.*</c> —
/// no <c>section.*</c> code exists in the fixed 93-row privilege register (inventing one would be
/// the larger deviation; a section is a property of a level).
/// </summary>
public sealed class SectionEndpoints : IEndpointModule
{
    private const string Tag = "Sections";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/sections")
            .WithTags(Tag);

        MapList(group);
        MapCreate(group);
        MapUpdate(group);
    }

    private static void MapList(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new ListSectionsQuery(), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Level.View)
            .WithName("ListSections")
            .WithSummary("List sections")
            .WithDescription("Spec 6.4.9: \"a two-row seeded list a school extends rarely.\" Not paged.")
            .Produces<SectionListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapCreate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                CreateSectionCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(response => TypedResults.Created($"/api/v1/sections/{response.Id}", response));
            })
            .RequirePrivilege(Privileges.Level.Create)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateSection")
            .WithSummary("Create a section")
            .WithDescription("Spec 6.4.2: 2..40 characters, unique, case-insensitive. `Idempotency-Key` is REQUIRED.")
            .Produces<SectionDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdate(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateSectionCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Level.Update)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("UpdateSection")
            .WithSummary("Rename a section")
            .WithDescription("Spec 6.4.9: name only.")
            .Produces<SectionDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
