using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// Handles <see cref="GetRegNumberPreviewQuery"/>.
/// </summary>
/// <remarks>
/// Approved delta amendment 1, the preview half: the counter partition read here is resolved from
/// the CURRENTLY SAVED <see cref="SchoolProfile.SerialReset"/> (never from a query parameter — the
/// contract has none for it), so a school running <c>continuous</c> previews against the running
/// roll, and a school running <c>per_year</c> previews against this calendar year's own row — not
/// always the per-year rows regardless of what is actually configured.
/// </remarks>
internal sealed class GetRegNumberPreviewQueryHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IRegistrationCounterRepository registrationCounterRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetRegNumberPreviewQuery, Result<RegNumberPreviewDto>>
{
    /// <inheritdoc />
    public async Task<Result<RegNumberPreviewDto>> HandleAsync(
        GetRegNumberPreviewQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetReadOnlySingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var partitionKey = RegistrationCounterPartition.Resolve(profile.SerialReset, now.Year);
        var lastSerial = await registrationCounterRepository
            .GetLastSerialAsync(partitionKey, cancellationToken)
            .ConfigureAwait(false);

        var preview = RegNumberFormat.Compose(
            profile.Abbreviation,
            request.Separator,
            now.Year,
            request.SerialWidth,
            lastSerial + 1);

        return Result.Success(new RegNumberPreviewDto(preview));
    }
}
