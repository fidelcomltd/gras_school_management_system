using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Results.Sheets;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Staff printing of published result sheets (`result.print`, arm-scoped). Under the same <c>/arms</c> prefix
/// <see cref="ReadinessEndpoints"/> owns, a domain split, not a URL-prefix one.
/// </summary>
public sealed class ResultSheetEndpoints : IEndpointModule
{
    private const string Tag = "Result sheets";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/arms").WithTags(Tag);

        MapPrint(group);
    }

    private static void MapPrint(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/result-sheets/pdf", async (
                Guid armId,
                [FromQuery(Name = "termId")] string termId,
                [FromQuery(Name = "pupilId")] string? pupilId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new PrintResultSheetsQuery(armId, termId, pupilId), cancellationToken);
                return result.Match(file => TypedResults.File(file.Content.ToArray(), "application/pdf", file.FileName));
            })
            .RequirePrivilege(Privileges.Results.Print, ScopeParameterKind.Arm, "armId")
            .WithName("PrintResultSheets")
            .WithSummary("Print an arm's published result sheets")
            .WithDescription(
                "The A4 result sheets for a published arm-term, as one PDF: every pupil with a result, in name order, each " +
                "starting on a fresh page and numbered within itself; or one pupil's sheet with `pupilId`. The same sheet the " +
                "parent downloads, except that the outstanding-fee line always shows. 409 `result_set.not_published` until the " +
                "arm's results for the term are published; 404 `result_sheet.not_found` when the pupil has no result in this " +
                "arm, `result_sheet.none` when no pupil does. Fetch it and save the blob.")
            .Produces<Stream>(StatusCodes.Status200OK, "application/pdf")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
