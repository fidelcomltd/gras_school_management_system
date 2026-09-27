using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

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
        var closedSessions = new HashSet<Guid>();
        foreach (var sessionId in assignments.Select(assignment => assignment.SessionId).OfType<Guid>().Distinct())
        {
            var session = await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
            sessionNames[sessionId] = session?.Name ?? "Unknown session";
            if (session?.State == SessionState.Closed)
            {
                closedSessions.Add(sessionId);
            }
        }

        var armClasses = new Dictionary<Guid, (string Name, int Order, string LabelKey)>();
        if (assignments.Any(assignment => assignment.ArmIds.Count > 0))
        {
            var levelsById = (await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(level => level.Id);
            foreach (var arm in await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
            {
                // ArmMapper's own fallback, so a class reads the same here as in the arms list.
                var level = levelsById.GetValueOrDefault(arm.ClassLevelId);
                armClasses[arm.Id] = (ArmDisplayName.Compose(level?.Name ?? "Unknown level", arm.Label), level?.ProgressionOrder ?? int.MaxValue, arm.LabelKey);
            }
        }

        return new AssignmentNameLookup(roleNames, sessionNames, closedSessions, armClasses);
    }
}

/// <summary>Names loaded by <see cref="AssignmentNames"/>; an id it did not load reads as unknown rather than failing.</summary>
internal sealed class AssignmentNameLookup(
    IReadOnlyDictionary<Guid, string> roleNames,
    IReadOnlyDictionary<Guid, string> sessionNames,
    IReadOnlySet<Guid> closedSessions,
    IReadOnlyDictionary<Guid, (string Name, int Order, string LabelKey)> armClasses)
{
    /// <summary>What an arm deleted since it was assigned reads as (TASK-0046 C removes it from the assignment).</summary>
    public const string RemovedClass = "a removed class";

    public string Role(Guid roleId) => roleNames.GetValueOrDefault(roleId, "Unknown role");

    public string? Session(Guid? sessionId) => sessionId is { } id ? sessionNames.GetValueOrDefault(id, "Unknown session") : null;

    /// <summary>Whether the assignment's session is Closed (a sessionless assignment never is).</summary>
    public bool IsClosed(Guid? sessionId) => sessionId is { } id && closedSessions.Contains(id);

    public string Arm(Guid armId) => armClasses.TryGetValue(armId, out var arm) ? arm.Name : RemovedClass;

    /// <summary>
    /// The named classes behind <paramref name="armIds"/>, in class order (level progression, then label), each name
    /// once; a removed arm is left out.
    /// </summary>
    public IReadOnlyList<string> ClassesInOrder(IEnumerable<Guid> armIds) =>
        [.. armIds.Where(armClasses.ContainsKey).Select(id => armClasses[id])
            .OrderBy(arm => arm.Order).ThenBy(arm => arm.LabelKey, StringComparer.Ordinal)
            .Select(arm => arm.Name).Distinct()];
}
