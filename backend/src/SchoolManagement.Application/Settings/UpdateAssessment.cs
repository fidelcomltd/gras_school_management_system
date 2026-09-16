using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>PUT /api/v1/settings/assessment</c> (spec 6.2.6, 6.2.12: "Whole structure as one array. Atomic.
/// Returns 409 when locked for the session."). A REPLACE with per-row IDENTITY preserved by
/// <see cref="AssessmentComponentSaveRequest.Id"/> — see that type's remarks for why this differs
/// from <see cref="UpdateGradingCommand"/>'s blind replace.
/// </summary>
/// <param name="Components">The whole structure, in the order it should print (examination forced last regardless of position — spec 6.2.6).</param>
/// <param name="ExpectedVersion">
/// The assessment group's current <c>versionNumber</c>, as last read from <c>GET /settings</c>. A
/// stale value is rejected <c>409 settings.assessment.stale_version</c> before anything is written.
/// </param>
/// <param name="Reason">
/// Required, at least ten characters, ONLY when a result set is Published in the active session
/// (spec 6.2.9); ignored otherwise. See <c>UpdateGradingCommandHandler</c>'s remarks.
/// </param>
public sealed record UpdateAssessmentCommand(
    IReadOnlyList<AssessmentComponentSaveRequest> Components,
    int ExpectedVersion,
    string? Reason)
    : ICommand<Result<SettingsAssessmentGroupDto>>;

/// <summary>
/// Structural checks only — the six numbered business rules (exactly one examination, total 100, and
/// so on) live in <see cref="AssessmentStructureRules"/>, and the session-lock/id-existence checks
/// live in the handler because both need a repository read this validator cannot perform.
/// </summary>
internal sealed class UpdateAssessmentCommandValidator : AbstractValidator<UpdateAssessmentCommand>
{
    /// <summary>6.2.9: "a reason of at least ten characters." Matches <c>UpdateGradingCommandValidator</c>.</summary>
    private const int ReasonMinLength = 10;

    /// <summary>Matches <c>ConfigVersionConfiguration.ReasonMaxLength</c>.</summary>
    private const int ReasonMaxLength = 1000;

    public UpdateAssessmentCommandValidator()
    {
        RuleFor(command => command.Components).NotNull();

        RuleForEach(command => command.Components).ChildRules(component =>
        {
            component.RuleFor(input => input.Id)
                .Must(id => id is null || Guid.TryParse(id, out _))
                .WithMessage("Id must be a valid identifier.");

            component.RuleFor(input => input.Name)
                .NotEmpty()
                .MaximumLength(AssessmentComponent.NameMaxLength);

            component.RuleFor(input => input.ShortLabel)
                .NotEmpty()
                .MaximumLength(AssessmentComponent.ShortLabelMaxLength);
        });

        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);

        RuleFor(command => command.Reason)
            .MinimumLength(ReasonMinLength)
            .WithMessage($"A reason must be at least {ReasonMinLength} characters.")
            .When(command => command.Reason is not null);

        RuleFor(command => command.Reason)
            .MaximumLength(ReasonMaxLength)
            .When(command => command.Reason is not null);
    }
}
