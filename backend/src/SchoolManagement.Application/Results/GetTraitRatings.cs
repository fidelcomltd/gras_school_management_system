using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>GET /api/v1/arms/{armId}/trait-ratings?termId=</c> (TASK-0083 stage 1; spec §6.7.7, §6.7.12
/// amendment) — one arm's trait-rating grid for one term: every active pupil as a row, including one
/// with no ratings entered at all.
/// </summary>
/// <param name="ArmId">The arm to read, from the route.</param>
/// <param name="TermId">The term this grid is for.</param>
public sealed record GetTraitRatingsQuery(string ArmId, string TermId) : IQuery<Result<TraitRatingSheetDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetTraitRatingsQueryValidator : AbstractValidator<GetTraitRatingsQuery>
{
    public GetTraitRatingsQueryValidator()
    {
        RuleFor(query => query.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(query => query.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
    }
}
