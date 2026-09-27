using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>
/// The display names behind a set of assignments (TASK-0046 A): role, session and class names, so a client never
/// resolves them from three lists it may not be allowed to read. Roles and sessions are fetched per distinct id (an
/// account holds a handful); arms and levels once, only when an arm-scoped assignment is present.
/// </summary>
internal sealed class AssignmentNames(
    IRoleRepository roles,
    IAcademicSessionRepository sessions,
    IArmRepository arms,
    IClassLevelRepository levels)
{
    /// <summary>Loads every name <paramref name="assignments"/> refer to.</summary>
    public async Task<AssignmentNameLookup> LoadAsync(IReadOnlyCollection<RoleAssignment> assignments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var roleNames = new Dictionary<Guid, string>();
        foreach (var roleId in assignments.Select(assignment => assignment.RoleId).Distinct())
        {
            var role = await roles.FindReadOnlyByIdAsync(roleId, cancellationToken).ConfigureAwait(false);
            roleNames[roleId] = role?.Name ?? "Unknown role";
        }

        var sessionNames = new Dictionary<Guid, string>();
        foreach (var sessionId in assignments.Select(assignment => assignment.SessionId).OfType<Guid>().Distinct())
        {
            var session = await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
            sessionNames[sessionId] = session?.Name ?? "Unknown session";
        }

        var armNames = new Dictionary<Guid, string>();
        if (assignments.Any(assignment => assignment.ArmIds.Count > 0))
        {
            var levelNames = (await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
                .ToDictionary(level => level.Id, level => level.Name);
            foreach (var arm in await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
            {
                armNames[arm.Id] = ArmDisplayName.Compose(levelNames.GetValueOrDefault(arm.ClassLevelId, "Class"), arm.Label);
            }
        }

        return new AssignmentNameLookup(roleNames, sessionNames, armNames);
    }
}

/// <summary>Names loaded by <see cref="AssignmentNames"/>; an id it did not load reads as unknown rather than failing.</summary>
internal sealed class AssignmentNameLookup(
    IReadOnlyDictionary<Guid, string> roleNames,
    IReadOnlyDictionary<Guid, string> sessionNames,
    IReadOnlyDictionary<Guid, string> armNames)
{
    public string Role(Guid roleId) => roleNames.GetValueOrDefault(roleId, "Unknown role");

    public string? Session(Guid? sessionId) => sessionId is { } id ? sessionNames.GetValueOrDefault(id, "Unknown session") : null;

    public string Arm(Guid armId) => armNames.GetValueOrDefault(armId, "Unknown class");
}
