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

    /// <summary>Maps <paramref name="scales"/> (already carrying their own points) to the wire DTO.</summary>
    public static SettingsRatingScaleGroupDto ToRatingScalesDto(IReadOnlyList<RatingScale> scales, int versionNumber)
    {
        ArgumentNullException.ThrowIfNull(scales);

        var dtos = scales
            .OrderBy(scale => scale.Name, StringComparer.OrdinalIgnoreCase)
            .Select(scale => new RatingScaleDto(
                scale.Id.ToString("D", CultureInfo.InvariantCulture),
                scale.Name,
                scale.Points
                    .OrderBy(point => point.PointOrder)
                    .Select(point => new RatingScalePointDto(
                        point.Id.ToString("D", CultureInfo.InvariantCulture),
                        point.PointCode,
                        point.PointLabel,
                        point.PointOrder))
                    .ToList()))
            .ToList();

        return new SettingsRatingScaleGroupDto(dtos, versionNumber);
    }

    /// <summary>
    /// Maps <paramref name="domains"/> (already carrying their own indicators) to the wire DTO.
    /// <paramref name="sectionNamesById"/> resolves each domain's <c>section</c> display name — the
    /// mapper takes it rather than a repository so a caller that has already loaded the section list
    /// for its own id-validation need not load it twice.
    /// </summary>
    public static SettingsDevelopmentDomainGroupDto ToDevelopmentDomainsDto(
        IReadOnlyList<DevelopmentDomain> domains,
        IReadOnlyDictionary<Guid, string> sectionNamesById,
        int versionNumber)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(sectionNamesById);

        var dtos = domains
            .OrderBy(domain => domain.SectionId)
            .ThenBy(domain => domain.DisplayOrder)
            .Select(domain => new DevelopmentDomainDto(
                domain.Id.ToString("D", CultureInfo.InvariantCulture),
                domain.SectionId.ToString("D", CultureInfo.InvariantCulture),
                sectionNamesById.TryGetValue(domain.SectionId, out var sectionName) ? sectionName : string.Empty,
                domain.Name,
                domain.DisplayOrder,
                domain.RatingScaleId.ToString("D", CultureInfo.InvariantCulture),
                domain.AllowsIndicatorComment,
                domain.Status,
                domain.Status == DevelopmentDomainStatus.Active
                    ? domain.Indicators.Count(indicator => indicator.Status == DevelopmentIndicatorStatus.Active)
                    : 0,
                domain.Indicators
                    .OrderBy(indicator => indicator.DisplayOrder)
                    .Select(indicator => new DevelopmentIndicatorDto(
                        indicator.Id.ToString("D", CultureInfo.InvariantCulture),
                        indicator.Name,
                        indicator.DisplayOrder,
                        indicator.Status))
                    .ToList()))
            .ToList();

        return new SettingsDevelopmentDomainGroupDto(dtos, versionNumber);
    }

    /// <summary>Maps <paramref name="resultRules"/> to the wire DTO. <paramref name="versionNumber"/> comes from the caller's <see cref="SchoolProfile.ResultRulesVersionNumber"/> read, matching <see cref="ToGradingDto"/>'s and <see cref="ToAssessmentDto"/>'s own pattern.</summary>
    public static ResultRulesDto ToResultRulesDto(ResultRules resultRules, int versionNumber)
    {
        ArgumentNullException.ThrowIfNull(resultRules);

        return new ResultRulesDto(
            resultRules.AnnualMethod,
            resultRules.WeightFirst,
            resultRules.WeightSecond,
            resultRules.WeightThird,
            resultRules.PrimaryPositionScope,
            resultRules.ShowLevelPosition,
            resultRules.TieBreakRule,
            resultRules.PassMark,
            resultRules.PromotionThreshold,
            resultRules.RequireCorePass,
            resultRules.CoreSubjectIds,
            resultRules.MinSubjectsForPosition,
            versionNumber);
    }
}
