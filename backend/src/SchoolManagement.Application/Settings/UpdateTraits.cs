using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>PUT /api/v1/settings/traits</c> (spec 6.2.7's trait rules, carried into 6.2.13's "Trait lists
/// replaced" and per-block scales). Every trait in the new set — this is a REPLACE, not a patch;
/// omitting a currently-saved trait removes it, refused <c>409</c> when that trait has ever been
/// rated. Same convention as <see cref="UpdateDevelopmentDomainsCommand"/>.
/// </summary>
/// <param name="AffectiveRatingScaleId">The scale the affective block's traits are rated against. Must match an existing <see cref="RatingScale"/>.</param>
/// <param name="PsychomotorRatingScaleId">The scale the psychomotor block's traits are rated against. Must match an existing <see cref="RatingScale"/>.</param>
/// <param name="Traits">The whole set, in the order it should list. See <see cref="TraitRules"/> for the structural save-time rules.</param>
/// <param name="ExpectedVersion">
/// The traits group's current <c>versionNumber</c>, as last read from <c>GET /settings</c>. A stale
/// value is rejected <c>409 settings.traits.stale_version</c> before anything is written.
/// </param>
/// <param name="Reason">
/// Required, at least ten characters, ONLY when a result set is Published in the active session
/// (spec 6.2.9); ignored otherwise — the same conditional gate every other settings group uses.
/// </param>
public sealed record UpdateTraitsCommand(
    Guid AffectiveRatingScaleId,
    Guid PsychomotorRatingScaleId,
    IReadOnlyList<TraitInput> Traits,
    int ExpectedVersion,
    string? Reason) : ICommand<Result<SettingsTraitsGroupDto>>;

/// <summary>
/// Structural checks only — the cross-row rule (duplicate trait name within a domain) lives in
/// <see cref="TraitRules"/>, and every database-dependent check (unknown scale/trait id, the
/// rated-trait removal gate) lives in <see cref="UpdateTraitsCommandHandler"/> — the same three-way
/// split <see cref="UpdateDevelopmentDomainsCommandValidator"/> established.
/// </summary>
internal sealed class UpdateTraitsCommandValidator : AbstractValidator<UpdateTraitsCommand>
{
    /// <summary>6.2.9: "a reason of at least ten characters."</summary>
    private const int ReasonMinLength = 10;

    /// <summary>Matches <c>ConfigVersionConfiguration.ReasonMaxLength</c>.</summary>
    private const int ReasonMaxLength = 1000;

    public UpdateTraitsCommandValidator()
    {
        RuleFor(command => command.AffectiveRatingScaleId).NotEmpty();
        RuleFor(command => command.PsychomotorRatingScaleId).NotEmpty();

        RuleFor(command => command.Traits).NotNull();

        RuleForEach(command => command.Traits).ChildRules(trait =>
        {
            trait.RuleFor(input => input.Domain).IsInEnum();

            trait.RuleFor(input => input.Name)
                .NotEmpty()
                .MaximumLength(Trait.NameMaxLength);
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
