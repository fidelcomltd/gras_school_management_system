using System.Globalization;
using System.Text;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// Encodes and decodes the opaque cursor for <c>GET /api/v1/levels</c> (spec 9.5, 6.4.9), sorted by
/// <c>progressionOrder</c> ascending with <c>id</c> as a tie-break (order is unique only across ACTIVE
/// levels — an inactive one included via <c>?status=all</c> could collide, so the tie-break is real).
/// </summary>
public static class LevelListCursor
{
    // ASCII unit separator (0x1F), same technique as RoleListCursor: never producible by either field.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>Encodes the last row's <c>progressionOrder</c> and <c>id</c>.</summary>
    public static string Encode(int progressionOrder, Guid id)
    {
        var raw = string.Join(
            FieldSeparator,
            progressionOrder.ToString(CultureInfo.InvariantCulture),
            id.ToString("D", CultureInfo.InvariantCulture));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything that
    /// is not a well-formed cursor produced by <see cref="Encode"/>.
    /// </summary>
    public static bool TryDecode(string? cursor, out int progressionOrder, out Guid id)
    {
        progressionOrder = 0;
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

        return parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out progressionOrder) &&
            Guid.TryParse(parts[1], out id);
    }
}
