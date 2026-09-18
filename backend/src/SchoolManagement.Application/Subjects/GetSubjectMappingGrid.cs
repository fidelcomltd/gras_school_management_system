using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// <c>GET /api/v1/subject-mappings?term_id=</c> (spec 6.6.5, 6.6.9): "the whole grid for a term:
/// levels, subjects, ticks, and the arm exception summary."
/// </summary>
/// <param name="TermId">The term to read the grid for.</param>
public sealed record GetSubjectMappingGridQuery(string TermId) : IQuery<Result<SubjectMappingGridDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetSubjectMappingGridQueryValidator : AbstractValidator<GetSubjectMappingGridQuery>
{
    public GetSubjectMappingGridQueryValidator() =>
        RuleFor(query => query.TermId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
}
