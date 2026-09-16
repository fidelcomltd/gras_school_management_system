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
/// </remarks>
internal static class SettingsSnapshotBuilder
{
    // camelCase, matching the wire format every other DTO in this API serialises with.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serialises the current, in-memory state of <paramref name="profile"/>, <paramref name="bands"/>
    /// and <paramref name="components"/> into a snapshot document — the WHOLE configuration as of this
    /// save, regardless of which group actually changed.
    /// </summary>
    public static string Build(
        SchoolProfile profile,
        IReadOnlyList<GradingBand> bands,
        IReadOnlyList<AssessmentComponent> components)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(bands);
        ArgumentNullException.ThrowIfNull(components);

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
            profile.AssessmentVersionNumber);

        return JsonSerializer.Serialize(snapshot, Options);
    }

    private sealed record SettingsSnapshot(
        SchoolProfileSnapshot SchoolProfile,
        IReadOnlyList<GradingBandSnapshot> GradingBands,
        int GradingVersionNumber,
        IReadOnlyList<AssessmentComponentSnapshot> AssessmentComponents,
        int AssessmentVersionNumber);

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
}
