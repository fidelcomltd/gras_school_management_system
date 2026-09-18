using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>POST /api/v1/settings/grading/reset</c> (spec 6.2.5: "a Reset to defaults action requiring
/// `settings.reset.defaults`... restores the six [now nine, 6.2.13] seeded bands. The reset confirms
/// first, naming what will be lost, and writes an audit event."). The confirmation dialogue itself is
/// frontend scope; this endpoint is what it calls once the administrator confirms.
/// </summary>
/// <param name="ExpectedVersion">
/// The grading group's current <c>versionNumber</c>, as last read from <c>GET /settings</c>. A stale
/// value is rejected <c>409 settings.grading.stale_version</c> before anything is written.
/// </param>
/// <param name="Reason">
/// Required, at least ten characters, ONLY when a result set is Published in the active session
/// (spec 6.2.9 — 6.2.11: "Reset to defaults while results are published: Allowed, treated as an
/// ordinary edit under 6.2.9"); ignored otherwise. See <c>UpdateGradingCommandHandler</c>'s remarks.
/// </param>
public sealed record ResetGradingCommand(int ExpectedVersion, string? Reason)
    : ICommand<Result<SettingsGradingGroupDto>>;

/// <summary>Validates <see cref="ResetGradingCommand"/>.</summary>
internal sealed class ResetGradingCommandValidator : AbstractValidator<ResetGradingCommand>
{
    /// <summary>6.2.9: "a reason of at least ten characters." Matches <c>UpdateGradingCommandValidator</c>.</summary>
    private const int ReasonMinLength = 10;

    /// <summary>Matches <c>ConfigVersionConfiguration.ReasonMaxLength</c>.</summary>
    private const int ReasonMaxLength = 1000;

    public ResetGradingCommandValidator()
    {
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
