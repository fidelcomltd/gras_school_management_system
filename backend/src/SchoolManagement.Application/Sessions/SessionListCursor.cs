using System.Text;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// Encodes and decodes the opaque cursor for <c>GET /api/v1/sessions</c> (spec 9.5, 6.3.8). The list
/// has exactly one server-chosen order — <c>name</c> descending — and the session's own
/// <c>Name</c> is unique (spec 6.3.3), so it is a sufficient, self-tie-breaking keyset value: unlike
/// <c>RoleListCursor</c>, no id tie-break is needed.
/// </summary>
public static class SessionListCursor
{
    /// <summary>Encodes the last row's <c>name</c>.</summary>
    public static string Encode(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(name));
    }

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything
    /// that is not a well-formed cursor produced by <see cref="Encode"/>.
    /// </summary>
    public static bool TryDecode(string? cursor, out string name)
    {
        name = string.Empty;

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

        name = Encoding.UTF8.GetString(bytes);
        return true;
    }
}
