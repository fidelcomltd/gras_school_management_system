using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Shared entity-to-DTO mapping for the settings slice, so <c>GetSettingsQueryHandler</c> and
/// <c>UpdateSchoolIdentityCommandHandler</c> cannot describe the same shape two different ways.</summary>
internal static class SettingsMapper
{
    /// <summary>Maps <paramref name="profile"/>'s identity fields to the wire DTO.</summary>
    public static SettingsIdentityGroupDto ToIdentityDto(SchoolProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new SettingsIdentityGroupDto(
            profile.SchoolName,
            profile.ShortName,
            profile.Address,
            profile.Phone,
            profile.Email,
            profile.Motto,
            profile.HeadTeacherName,
            profile.Timezone,
            profile.IdentityVersionNumber);
    }

    /// <summary>
    /// Maps <paramref name="profile"/>'s abbreviation fields to the wire DTO.
    /// <paramref name="issuedCount"/> is supplied by the caller rather than read here — amendment 2:
    /// no pupil register exists yet, so this card can only ever pass <see langword="null"/>
    /// (TASK-0051 wires the real count).
    /// </summary>
    public static SettingsAbbreviationGroupDto ToAbbreviationDto(SchoolProfile profile, int? issuedCount)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new SettingsAbbreviationGroupDto(profile.Abbreviation, issuedCount, profile.AbbreviationVersionNumber);
    }

    /// <summary>Maps <paramref name="profile"/>'s registration-number pattern fields to the wire DTO.</summary>
    public static SettingsRegNumberGroupDto ToRegNumberDto(SchoolProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new SettingsRegNumberGroupDto(
            profile.Separator,
            profile.SerialWidth,
            profile.SerialReset,
            SchoolProfile.YearSource,
            profile.RegNumberVersionNumber);
    }
}
