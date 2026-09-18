using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// <c>GET /api/v1/arms/{id}/subjects?term_id=</c> (spec 6.6.1, 6.6.9): "the resolved set in effect for
/// one arm, with each row flagged as level-inherited or arm exception. This is the endpoint the score
/// entry screen and the result renderer both call." Unwrapped array — same convention as
/// <c>ReorderLevelsCommand</c>'s response: a bounded list, no pagination.
/// </summary>
/// <param name="ArmId">The arm to resolve.</param>
/// <param name="TermId">The term to resolve against.</param>
public sealed record GetArmSubjectsQuery(string ArmId, string TermId) : IQuery<Result<IReadOnlyList<ArmSubjectDto>>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetArmSubjectsQueryValidator : AbstractValidator<GetArmSubjectsQuery>
{
    public GetArmSubjectsQueryValidator()
    {
        RuleFor(query => query.ArmId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");

        RuleFor(query => query.TermId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
    }
}
