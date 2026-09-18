using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Admissions;

/// <summary>
/// <c>POST /api/v1/admissions/{id}/decline</c> (spec 6.5.14: pending -&gt; withdrawn). <paramref
/// name="Id"/> is the PUPIL id. Issues no registration number and leaves the counter's
/// <c>last_serial</c> untouched — this command never calls the counter at all.
/// </summary>
/// <param name="Id">The pupil whose pending admission is being declined. Supplied from the route, not the body.</param>
/// <param name="Reason">Required (spec 6.5.14: "Requires a reason").</param>
public sealed record DeclineAdmissionCommand(Guid Id, string Reason) : ICommand<Result<PupilDto>>;

/// <summary>Structural check only — whether the pupil is actually pending lives in the handler and <c>Pupil.DeclineAdmission</c>.</summary>
internal sealed class DeclineAdmissionCommandValidator : AbstractValidator<DeclineAdmissionCommand>
{
    /// <summary>No length rule is spec-mandated for this reason (unlike, for example, the reg-number correction's 10-character floor); this ceiling matches the general convention (<c>ChangeAdminAccountStatusCommand</c>, <c>UpdateAbbreviationCommand</c>).</summary>
    public const int ReasonMaxLength = 500;

    public DeclineAdmissionCommandValidator() =>
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(ReasonMaxLength);
}
