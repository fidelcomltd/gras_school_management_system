using System.Globalization;
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

    /// <summary>Maps <paramref name="bands"/> (already ordered by the caller) to the wire DTO.</summary>
    public static SettingsGradingGroupDto ToGradingDto(IReadOnlyList<GradingBand> bands, int versionNumber)
    {
        ArgumentNullException.ThrowIfNull(bands);

        var dtos = bands
            .OrderBy(band => band.DisplayOrder)
            .Select(band => new GradingBandDto(
                band.Id.ToString("D", CultureInfo.InvariantCulture),
                band.LowerBound,
                band.UpperBound,
                band.GradeLetter,
                band.Remark,
                band.DisplayOrder))
            .ToList();

        return new SettingsGradingGroupDto(dtos, versionNumber);
    }

    /// <summary>Maps <paramref name="components"/> (already ordered by the caller) to the wire DTO.</summary>
    public static SettingsAssessmentGroupDto ToAssessmentDto(IReadOnlyList<AssessmentComponent> components, int versionNumber)
    {
        ArgumentNullException.ThrowIfNull(components);

        var dtos = components
            .OrderBy(component => component.DisplayOrder)
            .Select(component => new AssessmentComponentDto(
                component.Id.ToString("D", CultureInfo.InvariantCulture),
                component.Name,
                component.ShortLabel,
                component.MaxMark,
                component.IsExamination,
                component.DisplayOrder))
            .ToList();

        return new SettingsAssessmentGroupDto(dtos, versionNumber);
    }
}
