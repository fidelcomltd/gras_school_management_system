using System.Globalization;
using System.Text;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// Encodes and decodes the opaque cursor for the register's <c>GET /api/v1/pupils</c> default sort
/// (spec 6.5.15, TASK-0061): level in progression order, then arm, then surname ascending, then id.
/// </summary>
/// <remarks>
/// <para>
/// A pupil with no open enrolment — <c>transferred</c>, <c>withdrawn</c>, <c>graduated</c>, or the
/// <c>pending → withdrawn</c> lapsed application of spec 6.5.14, which never held one — sorts LAST,
/// as one flat trailing block ordered surname then id, per the human ruling
/// (<c>.agent/decisions/2026-Q3.md</c> 2026-09-15, TASK-0061). That block is represented with a
/// NON-NULL sentinel (<see cref="UnenrolledLevelOrdinal"/>, <see cref="UnenrolledArmKey"/>), never a
/// NULL, so the widened keyset stays a straight tuple comparison — the ruling's second binding part.
/// </para>
/// <para>
/// Deliberately a SEPARATE type from <see cref="PupilListCursor"/>, not that type widened in place:
/// <see cref="PupilListCursor"/> is shared with
/// <c>PupilRepository.ListAdmissionsQueueAsync</c> (the <c>pending</c>-only admissions queue, out of
/// this card's scope — every pending pupil is unenrolled by definition), whose cursor format
/// TASK-0064's <c>/admissions</c> screen already depends on. Widening the shared type would have
/// silently changed that screen's pagination too. See <c>.agent/tasks/TASK-0061.md</c>'s trap table.
/// </para>
/// </remarks>
public static class PupilRegisterCursor
{
    /// <summary>
    /// Sentinel level ordinal for a pupil with no open enrolment. <see cref="Domain.Classes.ClassLevel.ProgressionOrder"/>
    /// is "1 upward" (spec 6.4.2), so this sorts after every real level.
    /// </summary>
    public const int UnenrolledLevelOrdinal = int.MaxValue;

    /// <summary>Sentinel arm key for a pupil with no open enrolment.</summary>
    public const string UnenrolledArmKey = "";

    // ASCII unit separator (0x1F): never producible by the surname validator (letters, spaces,
    // hyphens, apostrophes only) or by an arm label (letters, digits, single internal spaces — see
    // Arm.LabelPattern), so splitting on it can never misparse a real key.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>Encodes the composite ordering key of the last row on a page.</summary>
    public static string Encode(int levelOrdinal, string armKey, string surnameKey, Guid id)
    {
        ArgumentNullException.ThrowIfNull(armKey);
        ArgumentNullException.ThrowIfNull(surnameKey);

        var raw = string.Join(
            FieldSeparator,
            levelOrdinal.ToString(CultureInfo.InvariantCulture),
            armKey,
            surnameKey,
            id.ToString("D", CultureInfo.InvariantCulture));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything
    /// that is not a well-formed cursor produced by <see cref="Encode"/>.
    /// </summary>
    public static bool TryDecode(
        string? cursor, out int levelOrdinal, out string armKey, out string surnameKey, out Guid id)
    {
        levelOrdinal = 0;
        armKey = string.Empty;
        surnameKey = string.Empty;
        id = Guid.Empty;

        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(cursor);
        }
        catch (FormatException)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(bytes);
        var parts = text.Split(FieldSeparator);

        if (parts.Length != 4 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out levelOrdinal) ||
            !Guid.TryParse(parts[3], out id))
        {
            return false;
        }

        armKey = parts[1];
        surnameKey = parts[2];
        return true;
    }
}
