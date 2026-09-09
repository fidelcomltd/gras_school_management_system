using System.Globalization;
using System.Text;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// Encodes and decodes the opaque cursor for <c>GET /api/v1/pupils</c> and
/// <c>GET /api/v1/admissions</c>: surname ascending, then id (spec 9.5's keyset convention, same
/// codec shape as <c>AdminAccountListCursor</c>).
/// </summary>
/// <remarks>
/// Spec 6.5.15's stated default is "class in progression order then surname ascending" — this card
/// has no arm/enrolment reference on <c>Pupil</c> at all (see the entity's own remarks), so there is
/// no class to order by yet. Sort is surname ascending, then id, and this is disclosed rather than
/// silently substituted — see <c>backend/docs/ASSUMPTIONS.md</c> §2.27. A later card that gives a
/// pupil a resolvable class widens this cursor's key rather than replacing it.
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
