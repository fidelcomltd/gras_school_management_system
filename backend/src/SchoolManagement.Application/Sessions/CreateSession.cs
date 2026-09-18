using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// One term's dates as supplied at session creation (spec 6.3.5: "The form takes the session name,
/// then three rows of start date, end date and next resumption date"). The term's <c>name</c> is
/// never taken from the client here — it defaults to "First/Second/Third Term" by ordinal and is
/// editable afterwards via <c>PATCH /api/v1/terms/{id}</c> (spec 6.3.4).
/// </summary>
/// <param name="StartDate">Inside the session's range; later than the previous term's end date.</param>
/// <param name="EndDate">Later than <paramref name="StartDate"/>; earlier than the next term's start date.</param>
/// <param name="NextResumptionDate">May be left blank (spec 6.3.4: required only to publish, which does not exist yet).</param>
public sealed record CreateSessionTermInput(DateOnly StartDate, DateOnly EndDate, DateOnly? NextResumptionDate);

/// <summary>
/// <c>POST /api/v1/sessions</c> (spec 6.3.5). Creates the session AND its three terms in one
/// transaction — spec 6.3.5: "There is no route that creates a session without terms, because a
/// session with two terms is not a state the school ever wants." <see cref="CreateSessionHandler"/>
/// is the only place in this codebase that adds a row to the sessions table.
/// </summary>
/// <param name="Name">Format <c>YYYY/YYYY</c>; the second year must be exactly the first plus one. Unique.</param>
/// <param name="StartDate">Must fall inside the first named year.</param>
/// <param name="EndDate">Must fall inside the second named year.</param>
/// <param name="Term1">First Term's dates.</param>
/// <param name="Term2">Second Term's dates.</param>
/// <param name="Term3">Third Term's dates.</param>
public sealed record CreateSessionCommand(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    CreateSessionTermInput Term1,
    CreateSessionTermInput Term2,
    CreateSessionTermInput Term3)
    : ICommand<Result<SessionDetailDto>>;

/// <summary>
/// Structural checks only (spec's actual date-consecutiveness and format rules live in
/// <see cref="AcademicSession.Create"/> and <see cref="TermChronologyGuard"/>, which have a
/// repository/entity to consult and can name the real offending dates).
/// </summary>
internal sealed class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .Length(AcademicSession.NameLength);

        RuleFor(command => command.Term1).NotNull();
        RuleFor(command => command.Term2).NotNull();
        RuleFor(command => command.Term3).NotNull();
    }
}
