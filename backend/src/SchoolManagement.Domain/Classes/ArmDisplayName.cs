using System.Globalization;

namespace SchoolManagement.Domain.Classes;

/// <summary>
/// Spec 6.4.3's display-name composition rule — "stated once", used everywhere an arm is named: the
/// arm list, selectors, score sheets, rosters, pin slips, the parent portal, result PDFs and every
/// export. THE ONLY IMPLEMENTATION. Never stored (<see cref="Arm"/> carries no display-name column);
/// always composed at read time from the owning level's current name and the arm's own label.
/// </summary>
/// <remarks>
/// Spec 6.4.3 / Appendix A entry 12 explicitly rejects making this conditional on how many arms a
/// level has — a level with a single arm still renders level + label (Primary 4 with one arm labelled
/// A is "Primary 4A"), never bare "Primary 4".
/// </remarks>
public static class ArmDisplayName
{
    /// <summary>
    /// Composes the display name. If <paramref name="label"/> is a single alphanumeric character, the
    /// result is <paramref name="levelName"/> immediately followed by the label, no space (<c>Primary
    /// 1A</c>). Otherwise it is <paramref name="levelName"/>, one space, then the label
    /// (<c>Primary 1 Gold</c>).
    /// </summary>
    public static string Compose(string levelName, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(levelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var isSingleAlphanumericCharacter = label.Length == 1 && char.IsLetterOrDigit(label[0]);

        return isSingleAlphanumericCharacter
            ? string.Concat(levelName, label)
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{levelName} {label}");
    }
}
