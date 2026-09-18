using System.Text.Json;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// Builds the WHOLE serialised configuration (spec 6.2.9) that every <see cref="ConfigVersion"/> row
/// stores in <see cref="ConfigVersion.SnapshotJson"/>.
/// </summary>
/// <remarks>
/// TASK-0069 adds the grading and assessment sections — <see cref="Build"/> now takes the current
/// bands and components explicitly, EVEN FROM a caller that did not itself change them (spec 6.2.9:
/// "the whole serialised configuration", every group, not only the one that changed). Every existing
/// caller (identity, abbreviation, reg-number saves) was updated to read the other group's CURRENT
/// state and pass it through, rather than continuing to snapshot only <see cref="SchoolProfile"/>.
/// TASK-0077 adds the result-rules section the same way — every caller now also reads the CURRENT
/// <see cref="ResultRules"/> row and passes it through.
/// </remarks>
/// <remarks>
/// TASK-0072 STAGE 3A: <see cref="Build"/> now takes ONE bundled <see cref="SettingsSnapshotState"/>
/// instead of one parameter per group — see that type's remarks for why. The serialised JSON SHAPE is
/// byte-identical to before this change; only how a caller ASSEMBLES the input moved, pinned by
/// <c>SettingsSnapshotBuilderTests.Build_ProducesTheSameShapeAsBeforeTheStage3ARefactor</c>.
/// </remarks>
internal static class SettingsSnapshotBuilder
{
    // camelCase, matching the wire format every other DTO in this API serialises with.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serialises the current, in-memory state of <paramref name="profile"/> and every group in
    /// <paramref name="state"/> into a snapshot document — the WHOLE configuration as of this save,
    /// regardless of which group actually changed.
    /// </summary>
    public static string Build(SchoolProfile profile, SettingsSnapshotState state)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);

        var bands = state.GradingBands;
        var components = state.AssessmentComponents;
        var resultRules = state.ResultRules;
        var ratingScales = state.RatingScales;
        var developmentDomains = state.DevelopmentDomains;

        ArgumentNullException.ThrowIfNull(bands);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(resultRules);
        ArgumentNullException.ThrowIfNull(ratingScales);
        ArgumentNullException.ThrowIfNull(developmentDomains);

        var snapshot = new SettingsSnapshot(
            new SchoolProfileSnapshot(
                profile.SchoolName,
                profile.ShortName,
                profile.Abbreviation,
                profile.Address,
                profile.Phone,
                profile.Email,
                profile.Motto,
                profile.HeadTeacherName,
                profile.Timezone,
                profile.IdentityVersionNumber,
                profile.AbbreviationVersionNumber,
                profile.Separator,
                profile.SerialWidth,
                profile.SerialReset,
                profile.RegNumberVersionNumber),
            bands
                .OrderBy(band => band.DisplayOrder)
                .Select(band => new GradingBandSnapshot(
                    band.LowerBound,
                    band.UpperBound,
                    band.GradeLetter,
                    band.Remark,
                    band.DisplayOrder))
                .ToList(),
            profile.GradingVersionNumber,
            components
                .OrderBy(component => component.DisplayOrder)
                .Select(component => new AssessmentComponentSnapshot(
                    component.Name,
                    component.ShortLabel,
                    component.MaxMark,
                    component.IsExamination,
                    component.DisplayOrder))
                .ToList(),
            profile.AssessmentVersionNumber,
            new ResultRulesSnapshot(
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
                resultRules.MinSubjectsForPosition),
            profile.ResultRulesVersionNumber,
            ratingScales
                .OrderBy(scale => scale.Name, StringComparer.OrdinalIgnoreCase)
                .Select(scale => new RatingScaleSnapshot(
                    scale.Name,
                    scale.Points
                        .OrderBy(point => point.PointOrder)
                        .Select(point => new RatingScalePointSnapshot(point.PointCode, point.PointLabel, point.PointOrder))
                        .ToList()))
                .ToList(),
            profile.RatingScalesVersionNumber,
            developmentDomains
                .OrderBy(domain => domain.SectionId)
                .ThenBy(domain => domain.DisplayOrder)
                .Select(domain => new DevelopmentDomainSnapshot(
                    domain.SectionId,
                    domain.Name,
                    domain.DisplayOrder,
                    domain.RatingScaleId,
                    domain.AllowsIndicatorComment,
                    domain.Status,
                    domain.Indicators
                        .OrderBy(indicator => indicator.DisplayOrder)
                        .Select(indicator => new DevelopmentIndicatorSnapshot(indicator.Name, indicator.DisplayOrder, indicator.Status))
                        .ToList()))
                .ToList(),
            profile.DevelopmentDomainsVersionNumber);

        return JsonSerializer.Serialize(snapshot, Options);
    }

    private sealed record SettingsSnapshot(
        SchoolProfileSnapshot SchoolProfile,
        IReadOnlyList<GradingBandSnapshot> GradingBands,
        int GradingVersionNumber,
        IReadOnlyList<AssessmentComponentSnapshot> AssessmentComponents,
        int AssessmentVersionNumber,
        ResultRulesSnapshot ResultRules,
        int ResultRulesVersionNumber,
        IReadOnlyList<RatingScaleSnapshot> RatingScales,
        int RatingScalesVersionNumber,
        IReadOnlyList<DevelopmentDomainSnapshot> DevelopmentDomains,
        int DevelopmentDomainsVersionNumber);

    private sealed record SchoolProfileSnapshot(
        string SchoolName,
        string ShortName,
        string Abbreviation,
        string Address,
        string Phone,
        string Email,
        string? Motto,
        string HeadTeacherName,
        string Timezone,
        int IdentityVersionNumber,
        int AbbreviationVersionNumber,
        string Separator,
        int SerialWidth,
        RegNumberSerialReset SerialReset,
        int RegNumberVersionNumber);

    private sealed record GradingBandSnapshot(
        int LowerBound,
        int UpperBound,
        string GradeLetter,
        string Remark,
        int DisplayOrder);

    private sealed record AssessmentComponentSnapshot(
        string Name,
        string ShortLabel,
        int MaxMark,
        bool IsExamination,
        int DisplayOrder);

    private sealed record ResultRulesSnapshot(
        AnnualMethod AnnualMethod,
        int? WeightFirst,
        int? WeightSecond,
        int? WeightThird,
        PrimaryPositionScope PrimaryPositionScope,
        bool ShowLevelPosition,
        TieBreakRule TieBreakRule,
        int PassMark,
        int PromotionThreshold,
        bool RequireCorePass,
        IReadOnlyList<Guid> CoreSubjectIds,
        int MinSubjectsForPosition);

    private sealed record RatingScaleSnapshot(string Name, IReadOnlyList<RatingScalePointSnapshot> Points);

    private sealed record RatingScalePointSnapshot(string PointCode, string PointLabel, int PointOrder);

    private sealed record DevelopmentDomainSnapshot(
        Guid SectionId,
        string Name,
        int DisplayOrder,
        Guid RatingScaleId,
        bool AllowsIndicatorComment,
        DevelopmentDomainStatus Status,
        IReadOnlyList<DevelopmentIndicatorSnapshot> Indicators);

    private sealed record DevelopmentIndicatorSnapshot(string Name, int DisplayOrder, DevelopmentIndicatorStatus Status);
}
