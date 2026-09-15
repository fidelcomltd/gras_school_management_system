using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="GetSettingsQuery"/>.</summary>
internal sealed class GetSettingsQueryHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IPupilRepository pupils)
    : IRequestHandler<GetSettingsQuery, Result<SettingsDto>>
{
    /// <inheritdoc />
    public async Task<Result<SettingsDto>> HandleAsync(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetReadOnlySingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        // TASK-0051: the register is real now — a live count, never null (amendment 2's "no register
        // exists yet" reason no longer holds).
        var issuedCount = await pupils
            .CountByRegistrationNumberPrefixAsync(profile.Abbreviation, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new SettingsDto(
            SettingsMapper.ToIdentityDto(profile),
            SettingsMapper.ToAbbreviationDto(profile, issuedCount),
            SettingsMapper.ToRegNumberDto(profile)));
    }
}
