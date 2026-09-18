using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>PATCH /api/v1/settings/reg-number</c> (spec 6.2.4). <c>yearSource</c> is deliberately absent
/// from this body — it is fixed (admission year) and never editable.
/// </summary>
/// <param name="Separator">One of <c>/</c>, <c>-</c>, <c>.</c>.</param>
/// <param name="SerialWidth">3 to 6. Serials are zero-padded to this width.</param>
/// <param name="SerialReset">
/// Whether the serial restarts each admission year (<see cref="RegNumberSerialReset.PerYear"/>) or
/// runs continuously (<see cref="RegNumberSerialReset.Continuous"/>) — approved delta amendment 1: the
/// counter itself carries a partition per mode, so this genuinely changes which numbers get issued
/// next, not merely a stored-but-inert flag.
/// </param>
/// <param name="ExpectedVersion">
/// The reg-number group's current <c>versionNumber</c>, as last read from <c>GET /settings</c>. A
/// stale value is rejected <c>409 settings.regnumber.stale_version</c> before anything is written.
/// </param>
public sealed record UpdateRegNumberCommand(
    string Separator,
    int SerialWidth,
    RegNumberSerialReset SerialReset,
    int ExpectedVersion)
    : ICommand<Result<SettingsRegNumberGroupDto>>;

/// <summary>Validates <see cref="UpdateRegNumberCommand"/> against spec 6.2.4's field table.</summary>
internal sealed class UpdateRegNumberCommandValidator : AbstractValidator<UpdateRegNumberCommand>
{
    public UpdateRegNumberCommandValidator()
    {
        RuleFor(command => command.Separator)
            .Must(RegNumberFormat.IsValidSeparator)
            .WithMessage("Separator must be one of / - .");

        RuleFor(command => command.SerialWidth)
            .InclusiveBetween(RegNumberFormat.MinSerialWidth, RegNumberFormat.MaxSerialWidth);

        RuleFor(command => command.SerialReset).IsInEnum();

        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}
