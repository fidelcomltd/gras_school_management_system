using System.Globalization;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>Maps a <see cref="Role"/> to the wire <see cref="RoleDto"/>, shared by every role endpoint.</summary>
internal static class RoleMapper
{
    /// <summary>Projects <paramref name="role"/> to its wire shape.</summary>
    public static RoleDto ToDto(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleDto(
            role.Id.ToString("D", CultureInfo.InvariantCulture),
            role.Name,
            role.Description,
            role.IsSystem,
            role.Privileges,
            role.Status);
    }
}
