using System.Globalization;
using System.Text;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// Encodes and decodes the opaque cursor for <c>GET /api/v1/admissions</c> (the queue of
/// <c>pending</c> pupils, unconditionally): surname ascending, then id (spec 9.5's keyset convention,
/// same codec shape as <c>AdminAccountListCursor</c>).
/// </summary>
/// <remarks>
/// <para>
/// NOT used by <c>GET /api/v1/pupils</c> as of TASK-0061 — that endpoint's default sort widened to
/// spec 6.5.15's full "class in progression order, then arm, then surname ascending" and moved to
/// <see cref="PupilRegisterCursor"/>, a SEPARATE type rather than this one widened in place: this
/// type is shared with <c>PupilRepository.ListAdmissionsQueueAsync</c>, whose <c>pending</c>-only
/// queue never carries a class (spec 6.5.14: a pending pupil holds no enrolment) and whose cursor
/// format TASK-0064's <c>/admissions</c> screen already depends on unchanged.
/// </para>
/// <para>
/// Before TASK-0061, this type ALSO backed <c>GET /api/v1/pupils</c>, disclosed as a temporary
/// shortfall against spec 6.5.15 rather than silently substituted — see
/// <c>backend/docs/ASSUMPTIONS.md</c> §2.27, now closed for the register (the queue's own sort was
/// never part of that gap: spec 6.5.15 never asks the admissions queue to sort by class).
/// </para>
/// </remarks>
public static class PupilListCursor
{
    // ASCII unit separator (0x1F): never producible by the surname validator (letters, spaces,
    // hyphens, apostrophes only), so splitting on it can never misparse a real surname.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>Encodes the composite ordering key of the last row on a page.</summary>
    public static string Encode(string surnameKey, Guid id)
    {
        ArgumentNullException.ThrowIfNull(surnameKey);

        var raw = string.Join(FieldSeparator, surnameKey, id.ToString("D", CultureInfo.InvariantCulture));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything
    /// that is not a well-formed cursor produced by <see cref="Encode"/>.
    /// </summary>
    public static bool TryDecode(string? cursor, out string surnameKey, out Guid id)
    {
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

        if (parts.Length != 2 || !Guid.TryParse(parts[1], out id))
        {
            return false;
        }

        surnameKey = parts[0];
        return true;
    }
}
