using System.Text.Json;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// Builds the WHOLE serialised configuration (spec 6.2.9) that every <see cref="ConfigVersion"/> row
/// stores in <see cref="ConfigVersion.SnapshotJson"/>.
/// </summary>
/// <remarks>
/// Only <see cref="SchoolProfile"/> exists as of TASK-0005a, so the snapshot has exactly one section.
/// TASK-0005b/0005c extend this same shape additively (a new section per group they add) rather than
/// replacing it — every future save's snapshot must still be able to reconstruct the FULL
/// configuration as of that moment, not just the group that changed.
/// </remarks>
internal static class SettingsSnapshotBuilder
{
    // camelCase, matching the wire format every other DTO in this API serialises with.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Serialises the current, in-memory state of <paramref name="profile"/> into a snapshot document.</summary>
    public static string Build(SchoolProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var snapshot = new SettingsSnapshot(new SchoolProfileSnapshot(
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
            profile.RegNumberVersionNumber));

        return JsonSerializer.Serialize(snapshot, Options);
    }

    private sealed record SettingsSnapshot(SchoolProfileSnapshot SchoolProfile);

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
}
