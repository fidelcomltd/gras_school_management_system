using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// <c>GET /api/v1/pupils/duplicates</c> (spec 6.5.11 step 1): "Duplicate detection runs... matching
/// on surname plus first name plus date of birth." Gated <c>pupil.create</c>, not <c>pupil.view</c> —
/// this is an admission-flow lookup, not a register read, and <c>pupil.create</c> is not scopable
/// (spec 4.4.4), so this route needs no arm-scope handling at all.
/// </summary>
/// <param name="Surname">Exact match, case-insensitive.</param>
/// <param name="FirstName">Exact match, case-insensitive.</param>
/// <param name="DateOfBirth">Exact match.</param>
/// <remarks>
/// Contact-phone matching (spec 6.5.11's other half of duplicate detection) is the NEXT card's —
/// <c>pupil_contact</c> does not exist yet. Only the surname+first-name+date-of-birth half is built
/// here, disclosed rather than silently narrowed.
/// </remarks>
public sealed record FindPupilDuplicatesQuery(string Surname, string FirstName, DateOnly DateOfBirth)
    : IQuery<Result<IReadOnlyList<PupilDto>>>;

/// <summary>Validates <see cref="FindPupilDuplicatesQuery"/>.</summary>
internal sealed class FindPupilDuplicatesQueryValidator : AbstractValidator<FindPupilDuplicatesQuery>
{
    public FindPupilDuplicatesQueryValidator()
    {
        RuleFor(query => query.Surname).NotEmpty().MaximumLength(Pupil.NameMaxLength);
        RuleFor(query => query.FirstName).NotEmpty().MaximumLength(Pupil.NameMaxLength);
    }
}
