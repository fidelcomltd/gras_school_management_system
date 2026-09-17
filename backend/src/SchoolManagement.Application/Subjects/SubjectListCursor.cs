using System.Text;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// Encodes and decodes the opaque cursor for <c>GET /api/v1/subjects</c> (spec 9.5, 6.6.7: "Default
/// sort by name ascending"), sorted by name (ordinal, case-insensitive key) then <c>id</c> as a final
/// tie-break — same technique as <see cref="Classes.ArmListCursor"/>.
/// </summary>
internal static class SubjectListCursor
{
    // ASCII unit separator (0x1F), same technique as ArmListCursor: never producible by any field.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>Encodes the last row's sort key.</summary>
    public static string Encode(string nameKey, Guid id)
    {
        var raw = string.Join(FieldSeparator, nameKey, id.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything that
    /// is not a well-formed cursor produced by <see cref="Encode"/>.
    /// </summary>
    public static bool TryDecode(string? cursor, out string nameKey, out Guid id)
    {
        nameKey = string.Empty;
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

        var parts = Encoding.UTF8.GetString(bytes).Split(FieldSeparator);

        if (parts.Length != 2 || !Guid.TryParse(parts[1], out id))
        {
            return false;
        }

        nameKey = parts[0];
        return true;
    }
}
