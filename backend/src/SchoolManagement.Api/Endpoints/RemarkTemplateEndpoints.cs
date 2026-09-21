using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The school's saved remark phrases (TASK-0086 stage B; spec §6.7.7 delta item 4), two lists by
/// <see cref="RemarkKind"/> — ruling T. The class-teacher and head-teacher entry screens read from
/// here to offer a picker before the teacher edits the inserted text.
/// </summary>
/// <remarks>
/// EVERY ROUTE MAPS WITH <c>RequireAuthenticatedCaller()</c>, NOT <c>RequirePrivilege(...)</c> —
/// which of the two remark-template privileges applies is DATA-DEPENDENT on a request's
/// <c>kind</c> (the query string on GET, the body on POST, or the stored row on DELETE), never a
/// route parameter the declarative mechanism can read. <see cref="RemarkTemplateAccessGuard"/>'s
/// remarks give the full reasoning; <c>PupilEndpoints</c> already established this same shape for
/// the same reason.
/// </remarks>
public sealed class RemarkTemplateEndpoints : IEndpointModule
{
    private const string Tag = "Remark templates";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/remark-templates").WithTags(Tag);

        MapList(group);
        MapCreate(group);
        MapDelete(group);
    }

    private static void MapList(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                [FromQuery] RemarkKind kind,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetRemarkTemplatesQuery(kind), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .WithName("ListRemarkTemplates")
            .WithSummary("List one kind's remark templates")
            .WithDescription(
                "Spec §6.7.7 delta item 4. `kind` is required. In creation order; ships empty. The " +
                "class-teacher list needs `result.remark.classteacher` under ANY grant — school-wide " +
                "or arm-scoped (ruling T) — and the head-teacher list needs " +
                "`result.remark.headteacher`, enforced server-side against the requested `kind`.")
            .Produces<RemarkTemplateListDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapCreate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                CreateRemarkTemplateCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(response => TypedResults.Created($"/api/v1/remark-templates/{response.Id}", response));
            })
            .RequireAuthenticatedCaller()
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateRemarkTemplate")
            .WithSummary("Add a remark template")
            .WithDescription(
                "Spec §6.7.7 delta item 4: text trimmed, 1-300 characters, else 422. A duplicate " +
                "within the same `kind` (trimmed, case-insensitive) is 409 " +
                "`remark_template.duplicate`. `Idempotency-Key` is REQUIRED — there is no version " +
                "to make a retry self-correcting the way a sheet save has. Privilege is checked " +
                "against the body's `kind`, same rule as `ListRemarkTemplates`.")
            .Produces<RemarkTemplateDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapDelete(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new DeleteRemarkTemplateCommand(id), cancellationToken);
                return result.Match(TypedResults.NoContent);
            })
            .RequireAuthenticatedCaller()
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("DeleteRemarkTemplate")
            .WithSummary("Delete a remark template")
            .WithDescription(
                "Spec §6.7.7 delta item 4: a HARD delete — inserted text is copied, never " +
                "referenced. An unknown id is 404 only for a caller holding either remark-template " +
                "privilege; anyone holding neither gets 403 first, before the id is even looked up. " +
                "A caller holding only the OTHER kind's privilege gets 403 on a row of this kind, " +
                "never 404, and the row is never touched.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
