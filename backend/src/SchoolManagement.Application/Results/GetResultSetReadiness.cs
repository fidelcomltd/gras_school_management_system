using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>GET /api/v1/arms/{armId}/readiness?termId=</c> (TASK-0088 stage B; spec §6.7.5, §6.7.11) — the
/// arm's completeness grid for one term: the readiness grid, counters, blockers and whether it can be
/// submitted. Arm-scoped rather than result-set-scoped, mirroring every other sheet route in this
/// module (<c>ScoreSheetEndpoints</c>'s own remarks give the reason), so a "Not started" arm answers
/// too — <c>resultSet: null</c>, everything missing.
/// </summary>
/// <param name="ArmId">The arm to read, from the route.</param>
/// <param name="TermId">The term this grid is for.</param>
public sealed record GetResultSetReadinessQuery(string ArmId, string TermId) : IQuery<Result<ResultSetReadinessDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetResultSetReadinessQueryValidator : AbstractValidator<GetResultSetReadinessQuery>
{
    public GetResultSetReadinessQueryValidator()
    {
        RuleFor(query => query.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(query => query.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
    }
}
