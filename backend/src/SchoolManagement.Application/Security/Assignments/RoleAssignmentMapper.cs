using System.Globalization;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>Maps a <see cref="RoleAssignment"/> to the wire <see cref="RoleAssignmentDto"/>.</summary>
internal static class RoleAssignmentMapper
{
    /// <summary>Projects <paramref name="assignment"/> to its wire shape.</summary>
    public static RoleAssignmentDto ToDto(RoleAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return new RoleAssignmentDto(
            assignment.Id.ToString("D", CultureInfo.InvariantCulture),
            assignment.AdminAccountId.ToString("D", CultureInfo.InvariantCulture),
            assignment.RoleId.ToString("D", CultureInfo.InvariantCulture),
            assignment.SessionId?.ToString("D", CultureInfo.InvariantCulture),
            assignment.ScopeType,
            assignment.ArmIds.Select(armId => armId.ToString("D", CultureInfo.InvariantCulture)).ToArray(),
            assignment.GrantedBy.ToString("D", CultureInfo.InvariantCulture),
            assignment.Status,
            assignment.CreatedAtUtc);
    }
}
