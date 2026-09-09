using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>PATCH /api/v1/settings/abbreviation</c> (spec 6.2.4). Requires the literal confirmation token
/// <see cref="UpdateAbbreviationCommandValidator.RequiredConfirmationToken"/> and a reason (spec: "the
/// save writes an audit event with a mandatory reason") — deliberately NO 10-character floor, unlike
/// 6.2.9's grading/assessment/traits/trait-scale/result-rules family: 6.2.10 confirms the abbreviation
/// is never locked and never triggers that warning (approved delta, confirmed as proposed).
/// </summary>
/// <param name="Abbreviation">2 to 8 characters. A value already used historically is allowed (spec 6.2.11).</param>
/// <param name="ConfirmationToken">Must equal the literal string <c>CHANGE</c>, typed by the administrator.</param>
/// <param name="Reason">
/// Non-empty once trimmed. Capped but not floored — see <c>backend/docs/ASSUMPTIONS.md</c> for both
/// authored choices.
/// </param>
/// <param name="ExpectedVersion">
/// The abbreviation group's current <c>versionNumber</c>, as last read from <c>GET /settings</c>. A
/// stale value is rejected <c>409 settings.abbreviation.stale_version</c> before anything is written.
/// </param>
public sealed record UpdateAbbreviationCommand(
    string Abbreviation,
    string ConfirmationToken,
    string Reason,
    int ExpectedVersion)
    : ICommand<Result<SettingsAbbreviationGroupDto>>;

/// <summary>Validates <see cref="UpdateAbbreviationCommand"/> against spec 6.2.4's field rules.</summary>
internal sealed class UpdateAbbreviationCommandValidator : AbstractValidator<UpdateAbbreviationCommand>
{
    /// <summary>The literal token spec 6.2.4 requires the administrator to type, verbatim, case-sensitive.</summary>
    public const string RequiredConfirmationToken = "CHANGE";

    /// <summary>Authored cap — spec sets no ceiling for this reason. See <c>ASSUMPTIONS.md</c>.</summary>
    public const int ReasonMaxLength = 500;

    public UpdateAbbreviationCommandValidator()
    {
        RuleFor(command => command.Abbreviation)
            .NotEmpty()
            .MinimumLength(SchoolProfile.AbbreviationMinLength)
            .MaximumLength(SchoolProfile.AbbreviationMaxLength);

        RuleFor(command => command.ConfirmationToken)
            .Equal(RequiredConfirmationToken)
            .WithMessage($"Type {RequiredConfirmationToken} to confirm.");

        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithMessage("A reason is required.")
            .MaximumLength(ReasonMaxLength);

        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}
