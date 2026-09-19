using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>GET /api/v1/arms/{armId}/development-ratings?termId=</c> (TASK-0083 stage 2; spec §6.7.7,
/// §6.7.12 amendment, Appendix E.3) — one arm's development-rating grid for one term: every active
/// pupil as a row, across the arm's section's active development domains.
/// </summary>
/// <param name="ArmId">The arm to read, from the route.</param>
/// <param name="TermId">The term this grid is for.</param>
public sealed record GetDevelopmentRatingsQuery(string ArmId, string TermId) : IQuery<Result<DevelopmentRatingSheetDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetDevelopmentRatingsQueryValidator : AbstractValidator<GetDevelopmentRatingsQuery>
{
    public GetDevelopmentRatingsQueryValidator()
    {
        RuleFor(query => query.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(query => query.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
    }
}
