using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>GET /api/v1/arms/{armId}/score-sheets?subjectId=&amp;termId=</c> (spec 6.7.4; TASK-0076's
/// approved contract delta) — one arm's score sheet for one subject and term: every active pupil as a
/// row, including one with no marks entered at all.
/// </summary>
/// <param name="ArmId">The arm to read, from the route.</param>
/// <param name="SubjectId">The subject this sheet is for.</param>
/// <param name="TermId">The term this sheet is for.</param>
public sealed record GetScoreSheetQuery(string ArmId, string SubjectId, string TermId) : IQuery<Result<ScoreSheetDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetScoreSheetQueryValidator : AbstractValidator<GetScoreSheetQuery>
{
    public GetScoreSheetQueryValidator()
    {
        RuleFor(query => query.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(query => query.SubjectId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("SubjectId must be a valid identifier.");
        RuleFor(query => query.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
    }
}
