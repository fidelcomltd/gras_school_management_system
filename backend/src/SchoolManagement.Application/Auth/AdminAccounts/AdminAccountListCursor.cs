using System.Globalization;
using System.Text;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>
/// Encodes and decodes the opaque cursor for <c>GET /api/v1/admins</c> (spec 6.1.8, 9.5): "Default
/// sort is status ascending with active first, then staff name ascending." That is a COMPOSITE sort
/// key, unlike <see cref="SchoolManagement.Application.Common.Pagination.OpaqueCursor"/>'s single
/// monotonic <c>long</c> (which config-versions' newest-first-by-id list is well served by, but this
/// list is not) — so this is a dedicated codec rather than a reuse of that one.
/// </summary>
/// <remarks>
/// PUBLIC rather than internal, matching <c>OpaqueCursor</c>'s own reasoning: the value is decoded in
/// the Application-layer query handler (a malformed cursor is a 422, not a 500) but encoded in the
/// Infrastructure-layer repository, which is the only place that knows the last row's raw ordering key
/// without a second round trip.
/// </remarks>
public static class AdminAccountListCursor
{
    // ASCII unit separator (0x1F): never producible by the staff-name validator (letters,
    // spaces, hyphens, apostrophes only), so splitting on it can never misparse a real staff name.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>
    /// Encodes the composite ordering key of the last row on a page: status ordinal, lower-invariant
    /// staff name (the tie-break), and id (the final tie-break, guaranteeing a total order even when
    /// two accounts share both status and staff name).
    /// </summary>
    public static string Encode(int statusOrder, string staffNameKey, Guid id)
    {
        ArgumentNullException.ThrowIfNull(staffNameKey);

        var raw = string.Join(
            FieldSeparator,
            statusOrder.ToString(CultureInfo.InvariantCulture),
            staffNameKey,
            id.ToString("D", CultureInfo.InvariantCulture));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything that
    /// is not a well-formed cursor produced by <see cref="Encode"/> — a malformed or tampered-with
    /// cursor, never a thrown exception a caller has to guard against.
    /// </summary>
    public static bool TryDecode(
        string? cursor,
        out int statusOrder,
        out string staffNameKey,
        out Guid id)
    {
        statusOrder = 0;
        staffNameKey = string.Empty;
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

        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out statusOrder))
        {
            return false;
        }

        if (!Guid.TryParse(parts[2], out id))
        {
            return false;
        }

        staffNameKey = parts[1];
        return true;
    }
}
