using System.Globalization;
using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// Maps <see cref="Arm"/> to its wire shape, composing <c>displayName</c> via
/// <see cref="ArmDisplayName"/> — the SOLE call site outside its own tests, per spec 6.4.3's "one
/// function, no second implementation" rule.
/// </summary>
internal static class ArmMapper
{
    /// <summary>Projects one arm to its wire shape, composing <c>displayName</c> along the way.</summary>
    /// <param name="arm">The arm to project.</param>
    /// <param name="levelNamesById">Every level's current name, keyed by id.</param>
    public static ArmDto ToDto(Arm arm, IReadOnlyDictionary<Guid, string> levelNamesById)
    {
        ArgumentNullException.ThrowIfNull(arm);
        ArgumentNullException.ThrowIfNull(levelNamesById);

        // A FK RESTRICT on class_level_id (ArmConfiguration) makes an orphaned arm impossible in
        // practice; this fallback only guards ArmDisplayName.Compose against ever throwing on a blank
        // level name, which it does not otherwise accept.
        var levelName = levelNamesById.TryGetValue(arm.ClassLevelId, out var name) ? name : "Unknown level";

        return new ArmDto(
            arm.Id.ToString("D", CultureInfo.InvariantCulture),
            arm.ClassLevelId.ToString("D", CultureInfo.InvariantCulture),
            levelName,
            arm.SessionId.ToString("D", CultureInfo.InvariantCulture),
            arm.Label,
            ArmDisplayName.Compose(levelName, arm.Label),
            arm.Capacity,
            arm.FormTeacherAdminId?.ToString("D", CultureInfo.InvariantCulture),
            arm.Status);
    }
}
