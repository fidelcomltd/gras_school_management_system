using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>PUT /api/v1/settings/rating-scales</c> (spec 6.2.13: "Rating scales become records"). Every
/// scale in the new set — this is a REPLACE, not a patch; omitting a currently-seeded scale removes
/// it, refused <c>409</c> when that scale is still referenced by a rating block.
/// </summary>
/// <param name="Scales">The whole set, in the order it should list. See <see cref="RatingScaleRules"/> for the save-time rules.</param>
/// <param name="ExpectedVersion">
/// The rating-scales group's current <c>versionNumber</c>, as last read from <c>GET /settings</c>. A
/// stale value is rejected <c>409 settings.ratingscales.stale_version</c> before anything is written.
/// </param>
/// <param name="Reason">
/// Required, at least ten characters, ONLY when a result set is Published in the active session
/// (spec 6.2.9); ignored otherwise. See <see cref="UpdateGradingCommandHandler"/>'s remarks — the
/// same conditional gate, applied to this group.
/// </param>
public sealed record UpdateRatingScalesCommand(IReadOnlyList<RatingScaleInput> Scales, int ExpectedVersion, string? Reason)
    : ICommand<Result<SettingsRatingScaleGroupDto>>;

/// <summary>
/// Structural checks only — the cross-row rules (point count, duplicate codes/orders/names) live in
/// <see cref="RatingScaleRules"/> so their rejection messages and order match <see cref="UpdateGradingCommandValidator"/>'s
/// own pattern: per-field FluentValidation rules cannot guarantee order across a whole array.
/// </summary>
internal sealed class UpdateRatingScalesCommandValidator : AbstractValidator<UpdateRatingScalesCommand>
{
    /// <summary>6.2.9: "a reason of at least ten characters."</summary>
    private const int ReasonMinLength = 10;

    /// <summary>Matches <c>ConfigVersionConfiguration.ReasonMaxLength</c>.</summary>
    private const int ReasonMaxLength = 1000;

    public UpdateRatingScalesCommandValidator()
    {
        RuleFor(command => command.Scales).NotNull();

        RuleForEach(command => command.Scales).ChildRules(scale =>
        {
            scale.RuleFor(input => input.Name)
                .NotEmpty()
                .MaximumLength(RatingScale.NameMaxLength);

            scale.RuleFor(input => input.Points).NotNull();

            scale.RuleForEach(input => input.Points).ChildRules(point =>
            {
                point.RuleFor(p => p.PointCode)
                    .NotEmpty()
                    .MaximumLength(RatingScalePoint.PointCodeMaxLength);

                point.RuleFor(p => p.PointLabel)
                    .NotEmpty()
                    .MaximumLength(RatingScalePoint.PointLabelMaxLength);
            });
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
