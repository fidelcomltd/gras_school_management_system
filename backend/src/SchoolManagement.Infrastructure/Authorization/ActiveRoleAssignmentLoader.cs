using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// An account's active assignments with their roles, loaded once per request scope. Both the privilege provider and the
/// role-name lookup read it, so a sign-in, <c>me</c> or refresh reads the assignment and role rows once, not twice.
/// </summary>
internal sealed class ActiveRoleAssignmentLoader(
    IRoleAssignmentRepository assignments, IRoleRepository roles, IAcademicSessionRepository sessions)
{
    private bool _activeSessionLoaded;
    private Guid? _activeSessionId;

    /// <summary>
    /// The assignments that count NOW (TASK-0046 B, ruled 2026-09-28; spec 4.2.2): those in the active session and any
    /// sessionless one; all of them while no session is active yet. The active session is read once per scope.
    /// </summary>
    public async Task<IReadOnlyList<(RoleAssignment Assignment, Role Role)>> LoadCurrentAsync(
        Guid accountId, CancellationToken cancellationToken)
    {
        var all = await LoadAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (all.All(pair => pair.Assignment.SessionId is null))
        {
            return all;
        }

        if (!_activeSessionLoaded)
        {
            _activeSessionId = (await sessions.FindActiveAsync(cancellationToken).ConfigureAwait(false))?.Id;
            _activeSessionLoaded = true;
        }

        return _activeSessionId is { } active
            ? [.. all.Where(pair => pair.Assignment.SessionId is null || pair.Assignment.SessionId == active)]
            : all;
    }

    private readonly Dictionary<Guid, IReadOnlyList<(RoleAssignment Assignment, Role Role)>> _byAccount = [];

    /// <summary>Each active assignment whose role still resolves, paired with that role.</summary>
    public async Task<IReadOnlyList<(RoleAssignment Assignment, Role Role)>> LoadAsync(
        Guid accountId, CancellationToken cancellationToken)
    {
        if (_byAccount.TryGetValue(accountId, out var loaded))
        {
            return loaded;
        }

        var active = await assignments.ListActiveForAccountReadOnlyAsync(accountId, cancellationToken).ConfigureAwait(false);
        var roleCache = new Dictionary<Guid, Role?>();
        var pairs = new List<(RoleAssignment, Role)>(active.Count);
        foreach (var assignment in active)
        {
            if (!roleCache.TryGetValue(assignment.RoleId, out var role))
            {
                role = await roles.FindReadOnlyByIdAsync(assignment.RoleId, cancellationToken).ConfigureAwait(false);
                roleCache[assignment.RoleId] = role;
            }

            if (role is not null)
            {
                pairs.Add((assignment, role));
            }
        }

        _byAccount[accountId] = pairs;
        return pairs;
    }
}
