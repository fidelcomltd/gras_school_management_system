using System.Globalization;
using System.Text;

namespace SchoolManagement.Application.Audit;

/// <summary>
/// Encodes and decodes the opaque cursor for <c>GET /api/v1/audit-events</c> (spec 6.1.12, 9.5):
/// "sorted newest first by default". <c>occurred_at</c> is NOT unique — two events can land in the
/// same millisecond — so the ordering key is the COMPOSITE <c>(occurred_at, id)</c>, both DESCENDING;
/// <c>id</c> is the BIGSERIAL spec 6.1.12 adds precisely to break that tie ("Monotonic, so ordering
/// is unambiguous even within the same millisecond"). A cursor keyed on <c>occurred_at</c> alone
/// would silently skip or repeat rows the moment two share a timestamp.
/// </summary>
/// <remarks>
/// PUBLIC rather than internal, matching <see cref="SchoolManagement.Application.Common.Pagination.OpaqueCursor"/>'s
/// own reasoning: decoded in the Application-layer handler (a malformed cursor is a 422, not a
/// 500), encoded in the Infrastructure-layer repository, which alone knows the last row's raw
/// ordering key without a second round trip.
/// </remarks>
public static class AuditEventListCursor
{
    // ASCII unit separator (0x1F): never producible by either field's own representation
    // (ticks and a bigint id are both decimal digit strings), so splitting on it can never misparse.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>Encodes the composite ordering key of the last row on a page.</summary>
    public static string Encode(DateTimeOffset occurredAt, long id)
    {
        var raw = string.Join(
            FieldSeparator,
            occurredAt.UtcTicks.ToString(CultureInfo.InvariantCulture),
            id.ToString(CultureInfo.InvariantCulture));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything
    /// that is not a well-formed cursor produced by <see cref="Encode"/> — a malformed or
    /// tampered-with cursor, never a thrown exception a caller has to guard against.
    /// </summary>
    public static bool TryDecode(string? cursor, out DateTimeOffset occurredAt, out long id)
    {
        occurredAt = default;
        id = 0;

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

        if (parts.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
        {
            return false;
        }

        if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            return false;
        }

        occurredAt = new DateTimeOffset(ticks, TimeSpan.Zero);
        return true;
    }
}
