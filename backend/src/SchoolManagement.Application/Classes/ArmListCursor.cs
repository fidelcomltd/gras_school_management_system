using System.Globalization;
using System.Text;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// Encodes and decodes the opaque cursor for <c>GET /api/v1/arms</c> (spec 9.5, 6.4.5), sorted by the
/// owning level's <c>progressionOrder</c> ascending, then <c>label</c> collated naturally
/// (<see cref="ArmLabelComparer"/>), then <c>id</c> as a final tie-break.
/// </summary>
internal static class ArmListCursor
{
    // ASCII unit separator (0x1F), same technique as LevelListCursor: never producible by any field.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>Encodes the last row's sort key.</summary>
    public static string Encode(int progressionOrder, string label, Guid id)
    {
        var raw = string.Join(
            FieldSeparator,
            progressionOrder.ToString(CultureInfo.InvariantCulture),
            label,
            id.ToString("D", CultureInfo.InvariantCulture));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything that
    /// is not a well-formed cursor produced by <see cref="Encode"/>.
    /// </summary>
    public static bool TryDecode(string? cursor, out int progressionOrder, out string label, out Guid id)
    {
        progressionOrder = 0;
        label = string.Empty;
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

        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out progressionOrder) ||
            !Guid.TryParse(parts[2], out id))
        {
            return false;
        }

        label = parts[1];
        return true;
    }
}
