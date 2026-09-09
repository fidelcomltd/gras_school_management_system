using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The admissions queue read surface (TASK-0050; spec 6.5.14, 6.5.17). The nine-step admission flow
/// and its <c>POST/PATCH /admissions</c> write surface are a later card — this ships the queue READ
/// only.
/// </summary>
public sealed class AdmissionEndpoints : IEndpointModule
{
    private const string Tag = "Admissions";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGroup("/admissions")
            .WithTags(Tag)
            .MapGet(string.Empty, async (
                    ISender sender,
                    CancellationToken cancellationToken,
                    [FromQuery] string? cursor = null,
                    [FromQuery] int? pageSize = null) =>
                {
                    var result = await sender.SendAsync(new ListAdmissionsQueueQuery(cursor, pageSize), cancellationToken);
                    return result.Match(TypedResults.Ok);
                })
            .RequirePrivilege(Privileges.Pupil.View, ScopeParameterKind.None)
            .WithName("ListAdmissionsQueue")
            .WithSummary("List the admissions queue")
            .WithDescription(
                "Spec 6.5.14: \"A pending pupil is excluded from every arm roster... It exists in the " +
                "admissions queue and nowhere else.\" This IS that queue — every pending pupil, " +
                "unconditionally. SCHOOL-WIDE `pupil.view` only: a pending record has no arm yet, so " +
                "an arm-scoped grant has no meaningful reach here (unlike `GET /pupils`, which still " +
                "admits an arm-scoped caller to an honest empty page). Cursor-paginated per spec 9.5.")
            .Produces<CursorPage<PupilDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
