using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="GetSettingsQuery"/>.</summary>
internal sealed class GetSettingsQueryHandler(ISchoolProfileRepository schoolProfileRepository)
    : IRequestHandler<GetSettingsQuery, Result<SettingsDto>>
{
    /// <inheritdoc />
    public async Task<Result<SettingsDto>> HandleAsync(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetReadOnlySingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new SettingsDto(
            SettingsMapper.ToIdentityDto(profile),
            // Amendment 2: no pupil register exists yet, so this can only ever be null — never 0.
            SettingsMapper.ToAbbreviationDto(profile, issuedCount: null),
            SettingsMapper.ToRegNumberDto(profile)));
    }
}
