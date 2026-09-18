using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>PUT /api/v1/settings/result-rules</c> (spec 6.2.8). A REPLACE of the whole singleton row — there
/// is no partial edit, matching <see cref="UpdateGradingCommand"/>'s blind-replace shape rather than
/// <see cref="UpdateAssessmentCommand"/>'s per-row identity (this row has no children to preserve
/// identity for).
/// </summary>
/// <param name="AnnualMethod">Simple average or weighted. Locked once Third Term is published for any arm (spec 6.2.10).</param>
/// <param name="WeightFirst">Required when <paramref name="AnnualMethod"/> is <see cref="AnnualMethod.Weighted"/>. 0 to 100. Locked with <paramref name="AnnualMethod"/>.</param>
/// <param name="WeightSecond">See <paramref name="WeightFirst"/>.</param>
/// <param name="WeightThird">See <paramref name="WeightFirst"/>.</param>
/// <param name="PrimaryPositionScope">Arm or level. Locked once anything is published in the active session (spec 6.2.10).</param>
/// <param name="ShowLevelPosition">Whether a second, level-wide position line prints alongside the arm position.</param>
/// <param name="TieBreakRule">How a tied position is broken. Locked once anything is published in the active session (spec 6.2.10).</param>
/// <param name="PassMark">0 to 100. A subject total at or above this is a pass.</param>
/// <param name="PromotionThreshold">0 to 100. Annual average at or above this proposes promotion.</param>
/// <param name="RequireCorePass">When true, promotion also requires a pass in every core subject.</param>
/// <param name="CoreSubjectIds">Required non-empty when <paramref name="RequireCorePass"/> is true. Every id must be an existing active subject.</param>
/// <param name="MinSubjectsForPosition">At least 1. A pupil with fewer scored subjects than this is excluded from position ranking.</param>
/// <param name="ExpectedVersion">
/// The result-rules group's current <c>versionNumber</c>, as last read from
/// <c>GET /settings/result-rules</c>. A stale value is rejected
/// <c>409 settings.resultrules.stale_version</c> before anything is written, checked BEFORE the 6.2.9
/// reason gate — matches <c>UpdateAssessmentCommandHandler</c>'s documented ORDER OF CHECKS.
/// </param>
/// <param name="Reason">
/// Required, at least ten characters, ONLY when a result set is Published in the active session
/// (spec 6.2.9); ignored otherwise. See <c>UpdateGradingCommandHandler</c>'s remarks.
/// </param>
public sealed record UpdateResultRulesCommand(
    AnnualMethod AnnualMethod,
    int? WeightFirst,
    int? WeightSecond,
    int? WeightThird,
    PrimaryPositionScope PrimaryPositionScope,
    bool ShowLevelPosition,
    TieBreakRule TieBreakRule,
    int PassMark,
    int PromotionThreshold,
    bool RequireCorePass,
    IReadOnlyList<Guid> CoreSubjectIds,
    int MinSubjectsForPosition,
    int ExpectedVersion,
    string? Reason)
    : ICommand<Result<ResultRulesDto>>;

/// <summary>
/// Spec 6.2.8's field-shape rules — everything checkable without a repository read. The two hard
/// locks (spec 6.2.10) and the core-subject existence/active check need live state this validator
/// cannot see, so both live in <c>UpdateResultRulesCommandHandler</c> instead (matching
/// <c>UpdateAssessmentCommandValidator</c>'s own division of labour).
/// </summary>
internal sealed class UpdateResultRulesCommandValidator : AbstractValidator<UpdateResultRulesCommand>
{
    /// <summary>6.2.9: "a reason of at least ten characters." Matches every other settings validator.</summary>
    private const int ReasonMinLength = 10;

    /// <summary>Matches <c>ConfigVersionConfiguration.ReasonMaxLength</c>.</summary>
    private const int ReasonMaxLength = 1000;

    public UpdateResultRulesCommandValidator()
    {
        RuleFor(command => command.AnnualMethod).IsInEnum();
        RuleFor(command => command.PrimaryPositionScope).IsInEnum();
        RuleFor(command => command.TieBreakRule).IsInEnum();

        RuleFor(command => command.WeightFirst)
            .NotNull()
            .WithMessage("The three term weights are required when the annual method is weighted.")
            .When(command => command.AnnualMethod == AnnualMethod.Weighted);

        RuleFor(command => command.WeightSecond)
            .NotNull()
            .WithMessage("The three term weights are required when the annual method is weighted.")
            .When(command => command.AnnualMethod == AnnualMethod.Weighted);

        RuleFor(command => command.WeightThird)
            .NotNull()
            .WithMessage("The three term weights are required when the annual method is weighted.")
            .When(command => command.AnnualMethod == AnnualMethod.Weighted);

        RuleFor(command => command.WeightFirst)
            .InclusiveBetween(ResultRules.MinPercent, ResultRules.MaxPercent)
            .When(command => command.WeightFirst is not null);

        RuleFor(command => command.WeightSecond)
            .InclusiveBetween(ResultRules.MinPercent, ResultRules.MaxPercent)
            .When(command => command.WeightSecond is not null);

        RuleFor(command => command.WeightThird)
            .InclusiveBetween(ResultRules.MinPercent, ResultRules.MaxPercent)
            .When(command => command.WeightThird is not null);

        // Rule 6.2.8: "Must total exactly 100" — only meaningfully checkable once all three are
        // present (the NotNull rules above already reject a Weighted submission missing any of them).
        RuleFor(command => command)
            .Must(command => (command.WeightFirst ?? 0) + (command.WeightSecond ?? 0) + (command.WeightThird ?? 0) == 100)
            .WithMessage(command =>
                $"The three term weights total {(command.WeightFirst ?? 0) + (command.WeightSecond ?? 0) + (command.WeightThird ?? 0)}. They must total 100.")
            .When(command =>
                command.AnnualMethod == AnnualMethod.Weighted &&
                command.WeightFirst is not null && command.WeightSecond is not null && command.WeightThird is not null);

        RuleFor(command => command.PassMark).InclusiveBetween(ResultRules.MinPercent, ResultRules.MaxPercent);
        RuleFor(command => command.PromotionThreshold).InclusiveBetween(ResultRules.MinPercent, ResultRules.MaxPercent);

        RuleFor(command => command.CoreSubjectIds).NotNull();

        RuleFor(command => command.CoreSubjectIds)
            .NotEmpty()
            .WithMessage("At least one core subject is required when a core-subject pass is required for promotion.")
            .When(command => command.RequireCorePass);

        RuleFor(command => command.MinSubjectsForPosition).GreaterThanOrEqualTo(1);

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
