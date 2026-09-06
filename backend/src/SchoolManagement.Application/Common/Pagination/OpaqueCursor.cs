using System.Globalization;
using System.Text;

namespace SchoolManagement.Application.Common.Pagination;

/// <summary>
/// Encodes and decodes the opaque cursor value spec 9.5 requires ("the cursor opaque to the
/// client"). Wraps a single <see cref="long"/> — the ordering key a cursor-paginated query sorts
/// by — as a base64 string, so a client can hold it, compare it for equality, and echo it back, but
/// never construct or interpret one itself.
/// </summary>
/// <remarks>
/// PUBLIC rather than internal: the value is decoded in the Application-layer handler (a validation
/// concern — a malformed cursor is a 422, not a 500) but encoded in the Infrastructure-layer
/// repository, which is the only place that knows the last row's raw ordering key without a second
/// round trip. Both need this type, and there is no <c>InternalsVisibleTo</c> grant between the two
/// production assemblies (only the test projects get one) to make an internal alternative work.
/// </remarks>
public static class OpaqueCursor
{
    /// <summary>Encodes <paramref name="value"/> as an opaque cursor.</summary>
    public static string Encode(long value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything
    /// that is not a base64-encoded integer — a malformed or tampered-with cursor, never a thrown
    /// exception a caller has to guard against.
    /// </summary>
    public static bool TryDecode(string? cursor, out long value)
    {
        value = 0;

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

        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
