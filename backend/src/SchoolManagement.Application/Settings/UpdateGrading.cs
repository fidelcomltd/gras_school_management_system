using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>PUT /api/v1/settings/grading</c> (spec 6.2.5, 6.2.12: "Whole scale as one array. Atomic.").
/// Every band in the new scale — this is a REPLACE, not a patch; omitting a currently-seeded band
/// deletes it.
/// </summary>
/// <param name="Bands">The whole scale, in the order it should print. See <see cref="GradingScaleRules"/> for the ten save-time rules.</param>
/// <param name="ExpectedVersion">
/// The grading group's current <c>versionNumber</c>, as last read from <c>GET /settings</c>. A stale
/// value is rejected <c>409 settings.grading.stale_version</c> before anything is written.
/// </param>
/// <param name="Reason">
/// Required, at least ten characters, ONLY when a result set is Published in the active session
/// (spec 6.2.9); ignored otherwise. See <see cref="UpdateGradingCommandHandler"/>'s remarks — the
/// check that decides which applies is honestly always "no publications" today, no results module
/// existing yet.
/// </param>
public sealed record UpdateGradingCommand(IReadOnlyList<GradingBandInput> Bands, int ExpectedVersion, string? Reason)
    : ICommand<Result<SettingsGradingGroupDto>>;

/// <summary>
/// Structural checks only — the ten numbered business rules (bound range, coverage, uniqueness, and
/// so on) live in <see cref="GradingScaleRules"/> so their rejection messages and ORDER match spec
/// 6.2.5 exactly, which per-field FluentValidation rules cannot guarantee across a whole array.
/// </summary>
internal sealed class UpdateGradingCommandValidator : AbstractValidator<UpdateGradingCommand>
{
    /// <summary>6.2.5: "Letters and digits." Widened to also allow <c>+</c>/<c>-</c> for 6.2.13's <c>A+</c>/<c>B-</c> seed — see <c>backend/docs/ASSUMPTIONS.md</c>.</summary>
    private const string GradeLetterPattern = "^[A-Za-z0-9+-]+$";

    /// <summary>6.2.9: "a reason of at least ten characters."</summary>
    private const int ReasonMinLength = 10;

    /// <summary>Matches <c>ConfigVersionConfiguration.ReasonMaxLength</c>.</summary>
    private const int ReasonMaxLength = 1000;

    public UpdateGradingCommandValidator()
    {
        RuleFor(command => command.Bands).NotNull();

        RuleForEach(command => command.Bands).ChildRules(band =>
        {
            band.RuleFor(input => input.GradeLetter)
                .NotEmpty()
                .MaximumLength(GradingBand.GradeLetterMaxLength)
                .Matches(GradeLetterPattern)
                .WithMessage("Grade letter may contain only letters, digits, + and -.");

            band.RuleFor(input => input.Remark).NotNull();
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
