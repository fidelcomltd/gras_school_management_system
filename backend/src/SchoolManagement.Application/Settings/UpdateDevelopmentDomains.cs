using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>PUT /api/v1/settings/development-domains</c> (spec 6.2.13: "Development domains and
/// indicators, nursery only"). Every domain in the new set — this is a REPLACE, not a patch;
/// omitting a currently-saved domain removes it, refused <c>409</c> when any of its indicators has
/// ever been rated. Same convention as <see cref="UpdateRatingScalesCommand"/>.
/// </summary>
/// <param name="Domains">The whole set, in the order it should list. See <see cref="DevelopmentDomainRules"/> for the structural save-time rules.</param>
/// <param name="ExpectedVersion">
/// The development-domains group's current <c>versionNumber</c>, as last read from
/// <c>GET /settings</c>. A stale value is rejected
/// <c>409 settings.developmentdomains.stale_version</c> before anything is written.
/// </param>
/// <param name="Reason">
/// Required, at least ten characters, ONLY when a result set is Published in the active session
/// (spec 6.2.9); ignored otherwise — the same conditional gate every other settings group uses.
/// </param>
public sealed record UpdateDevelopmentDomainsCommand(
    IReadOnlyList<DevelopmentDomainInput> Domains,
    int ExpectedVersion,
    string? Reason) : ICommand<Result<SettingsDevelopmentDomainGroupDto>>;

/// <summary>
/// Structural checks only — the cross-row rules (duplicate domain name within a section, duplicate
/// indicator name within a domain) live in <see cref="DevelopmentDomainRules"/>, and every
/// database-dependent check (unknown section/scale/domain/indicator id, the rated-indicator removal
/// gate) lives in <see cref="UpdateDevelopmentDomainsCommandHandler"/> — the same three-way split
/// <see cref="UpdateRatingScalesCommandValidator"/> established.
/// </summary>
internal sealed class UpdateDevelopmentDomainsCommandValidator : AbstractValidator<UpdateDevelopmentDomainsCommand>
{
    /// <summary>6.2.9: "a reason of at least ten characters."</summary>
    private const int ReasonMinLength = 10;

    /// <summary>Matches <c>ConfigVersionConfiguration.ReasonMaxLength</c>.</summary>
    private const int ReasonMaxLength = 1000;

    public UpdateDevelopmentDomainsCommandValidator()
    {
        RuleFor(command => command.Domains).NotNull();

        RuleForEach(command => command.Domains).ChildRules(domain =>
        {
            domain.RuleFor(input => input.SectionId).NotEmpty();

            domain.RuleFor(input => input.Name)
                .NotEmpty()
                .MaximumLength(DevelopmentDomain.NameMaxLength);

            domain.RuleFor(input => input.RatingScaleId).NotEmpty();

            domain.RuleFor(input => input.Indicators).NotNull();

            domain.RuleForEach(input => input.Indicators).ChildRules(indicator =>
            {
                indicator.RuleFor(i => i.Name)
                    .NotEmpty()
                    .MaximumLength(DevelopmentIndicator.NameMaxLength);
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
