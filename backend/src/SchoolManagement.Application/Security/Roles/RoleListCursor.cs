using System.Text;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>
/// Encodes and decodes the opaque cursor for <c>GET /api/v1/roles</c> (approved delta §2, spec 9.5).
/// </summary>
/// <remarks>
/// Unlike <see cref="SchoolManagement.Application.Auth.AdminAccounts.AdminAccountListCursor"/>'s fixed
/// composite key (that list has exactly one server-chosen default order), this list's sort field is
/// CALLER-CHOSEN between <c>name</c> and <c>status</c> (approved delta §2's whitelist) — so the
/// cursor carries whichever single sort value the last row on the page had (already lower-invariant
/// for a name sort, so a plain ordinal string comparison matches the query's own
/// case-insensitive ordering) plus the id tie-break, rather than baking in a fixed set of fields.
/// The caller is trusted to keep requesting the SAME <c>sort</c>/<c>direction</c> across pages — a
/// client switching sort mid-pagination gets a cursor that decodes fine but orders against the new
/// field, which is no worse than any other keyset-pagination API under the same misuse.
/// </remarks>
public static class RoleListCursor
{
    // ASCII unit separator (0x1F): never producible by a role name (validated 1..60 chars of
    // ordinary text) or a status name, so splitting on it can never misparse a real value.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>Encodes the last row's sort-field value and id.</summary>
    public static string Encode(string sortValue, Guid id)
    {
        ArgumentNullException.ThrowIfNull(sortValue);

        var raw = string.Join(FieldSeparator, sortValue, id.ToString("D"));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Attempts to decode <paramref name="cursor"/>. Returns <see langword="false"/> for anything that
    /// is not a well-formed cursor produced by <see cref="Encode"/>.
    /// </summary>
    public static bool TryDecode(string? cursor, out string sortValue, out Guid id)
    {
        sortValue = string.Empty;
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

        if (parts.Length != 2)
        {
            return false;
        }

        if (!Guid.TryParse(parts[1], out id))
        {
            return false;
        }

        sortValue = parts[0];
        return true;
    }
}
