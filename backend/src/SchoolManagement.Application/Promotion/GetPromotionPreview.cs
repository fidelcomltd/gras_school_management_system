using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Promotion;

/// <summary>
/// <c>GET /api/v1/sessions/{sessionId}/promotion/preview</c> (spec 6.3.7): the review screen's rows, destination arms and
/// blockers. Needs <c>promotion.run</c>.
/// </summary>
/// <param name="SessionId">The session promoted from.</param>
/// <param name="TargetSessionId">The session promoted into; the next session by start date when absent.</param>
public sealed record GetPromotionPreviewQuery(Guid SessionId, Guid? TargetSessionId) : IQuery<Result<PromotionPreviewDto>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class GetPromotionPreviewQueryValidator : AbstractValidator<GetPromotionPreviewQuery>;

/// <summary>Handles <see cref="GetPromotionPreviewQuery"/>.</summary>
internal sealed class GetPromotionPreviewHandler(PromotionPlanner planner) : IRequestHandler<GetPromotionPreviewQuery, Result<PromotionPreviewDto>>
{
    /// <inheritdoc />
    public async Task<Result<PromotionPreviewDto>> HandleAsync(GetPromotionPreviewQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var planned = await planner.PlanAsync(request.SessionId, request.TargetSessionId, cancellationToken).ConfigureAwait(false);
        if (planned.IsFailure)
        {
            return Result.Failure<PromotionPreviewDto>(planned.Error);
        }

        var plan = planned.Value;
        return Result.Success(new PromotionPreviewDto(
            Session(plan.Source),
            plan.Target is null ? null : Session(plan.Target),
            plan.Blockers,
            plan.Rows,
            plan.TargetArms,
            plan.CoreSubjects,
            [.. plan.Excluded.Select(pupil => new PromotionExcludedPupilDto(pupil.Id, PromotionPlanner.DisplayName(pupil), pupil.RegistrationNumber, pupil.Status))],
            plan.CommittedBatch is null ? null : PromotionBatchDto.From(plan.CommittedBatch, plan.Target?.Name ?? string.Empty),
            plan.CanDecide));
    }

    private static PromotionSessionDto Session(Domain.Sessions.AcademicSession session) =>
        new(session.Id, session.Name, session.StartDate, session.EndDate);
}

