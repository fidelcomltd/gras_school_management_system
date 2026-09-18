using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// <c>PATCH /api/v1/terms/{id}</c> (spec 6.3.10): "Dates, label, times school opened, next
/// resumption date." Every field is independently optional; an absent field is left unchanged.
/// <see cref="TimesSchoolOpened"/> cannot be CLEARED back to blank through this command — spec never
/// asks for that operation, only for filling it in once and (implicitly, spec 6.3.6) never touching
/// it again after close.
/// </summary>
/// <param name="Id">The term being edited.</param>
/// <param name="Name"><see langword="null"/> to leave unchanged.</param>
/// <param name="StartDate"><see langword="null"/> to leave unchanged.</param>
/// <param name="EndDate"><see langword="null"/> to leave unchanged.</param>
/// <param name="TimesSchoolOpened"><see langword="null"/> to leave unchanged; rejected once the term is closed.</param>
/// <param name="NextResumptionDate"><see langword="null"/> to leave unchanged.</param>
public sealed record UpdateTermCommand(
    Guid Id,
    string? Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? TimesSchoolOpened,
    DateOnly? NextResumptionDate)
    : ICommand<Result<TermDto>>;

/// <summary>Validates <see cref="UpdateTermCommand"/>.</summary>
internal sealed class UpdateTermCommandValidator : AbstractValidator<UpdateTermCommand>
{
    public UpdateTermCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(Term.NameMaxLength)
            .When(command => command.Name is not null);

        RuleFor(command => command.TimesSchoolOpened)
            .InclusiveBetween(Term.MinTimesSchoolOpened, Term.MaxTimesSchoolOpened)
            .When(command => command.TimesSchoolOpened is not null);
    }
}
