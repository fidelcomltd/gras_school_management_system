using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>
/// Handles <see cref="CopyAssignmentsToSessionCommand"/> (TASK-0046 B). Each ACTIVE assignment in the source session is
/// copied into the target, or skipped with a reason, never silently changed: a school-wide one copies as is; an
/// arm-scoped one maps each class to the target session's class with the same level and label, and is skipped whole
/// when any class has no match. Skipped too: the caller's own (escalation rule 1), a deactivated account, an archived
/// role, the Super Admin role, a role that can no longer take the scope, and anything the target already covers (the
/// same role school-wide, or over every one of the classes). The route requires <c>role.assign</c>; an arm-scoped copy
/// also needs <c>role.scope.assign</c> within the caller's own scope, exactly as creating one does (rule 3). One audit
/// event per created assignment, naming its source.
/// </summary>
internal sealed class CopyAssignmentsToSessionCommandHandler(
    IRoleAssignmentRepository assignments,
    IAdminAccountRepository accounts,
    IRoleRepository roles,
    IAcademicSessionRepository sessions,
    IArmRepository arms,
    AssignmentNames assignmentNames,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CopyAssignmentsToSessionCommand, Result<AssignmentCopyResultDto>>
{
    private const string EntityType = "role_assignment";

    /// <inheritdoc />
    public async Task<Result<AssignmentCopyResultDto>> HandleAsync(CopyAssignmentsToSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } actorIdText || !Guid.TryParse(actorIdText, out var actorId))
        {
            return Result.Failure<AssignmentCopyResultDto>(Error.Unauthenticated("authentication.required", "Sign in to perform this action."));
        }

        var from = await sessions.FindReadOnlyByIdAsync(Guid.Parse(request.FromSessionId), cancellationToken).ConfigureAwait(false);
        var to = await sessions.FindReadOnlyByIdAsync(Guid.Parse(request.ToSessionId), cancellationToken).ConfigureAwait(false);
        if (from is null || to is null)
        {
            return Result.Failure<AssignmentCopyResultDto>(Error.NotFound("session.not_found", "No session was found with that id."));
        }

        if (to.State == SessionState.Closed)
        {
            return Result.Failure<AssignmentCopyResultDto>(Error.Conflict(
                "role_assignment.session_closed",
                $"The session {to.Name} is closed. Assignments can only be made in an open session."));
        }

        var source = await assignments.ListActiveForSessionReadOnlyAsync(from.Id, cancellationToken).ConfigureAwait(false);
        var held = (await assignments.ListActiveForSessionReadOnlyAsync(to.Id, cancellationToken).ConfigureAwait(false)).ToList();
        var scopeGrants = (await effectivePrivilegeProvider.GetGrantsAsync(actorIdText, cancellationToken).ConfigureAwait(false))
            .Where(grant => string.Equals(grant.Privilege, Privileges.Role.ScopeAssign, StringComparison.Ordinal))
            .ToList();
        var actorScopeIsSchoolWide = scopeGrants.Any(grant => grant.Scope == ScopeType.SchoolWide);
        var actorArmIds = scopeGrants.Where(grant => grant.Scope == ScopeType.ArmList).SelectMany(grant => grant.ArmIds).ToHashSet();
        var names = await assignmentNames.LoadAsync(source, cancellationToken).ConfigureAwait(false);

        var allArms = await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var armsById = allArms.ToDictionary(arm => arm.Id);
        var targetArms = allArms
            .Where(arm => arm.SessionId == to.Id && arm.Status == ArmStatus.Active)
            .ToDictionary(arm => (arm.ClassLevelId, arm.LabelKey));

        var accountsById = new Dictionary<Guid, AdminAccount?>();
        var rolesById = new Dictionary<Guid, Role?>();
        foreach (var assignment in source)
        {
            if (!accountsById.ContainsKey(assignment.AdminAccountId))
            {
                accountsById[assignment.AdminAccountId] = await accounts.FindReadOnlyByIdAsync(assignment.AdminAccountId, cancellationToken).ConfigureAwait(false);
            }

            if (!rolesById.ContainsKey(assignment.RoleId))
            {
                rolesById[assignment.RoleId] = await roles.FindReadOnlyByIdAsync(assignment.RoleId, cancellationToken).ConfigureAwait(false);
            }
        }

        var copied = new List<AssignmentCopyRowDto>();
        var skipped = new List<AssignmentCopyRowDto>();
        foreach (var assignment in source
            .OrderBy(assignment => accountsById[assignment.AdminAccountId]?.StaffName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(assignment => names.Role(assignment.RoleId), StringComparer.OrdinalIgnoreCase))
        {
            var account = accountsById[assignment.AdminAccountId];
            var role = rolesById[assignment.RoleId];
            AssignmentCopyRowDto Row(IReadOnlyList<string> armNames, string? reason) => new(
                assignment.Id.ToString("D", CultureInfo.InvariantCulture),
                assignment.AdminAccountId.ToString("D", CultureInfo.InvariantCulture),
                account?.StaffName ?? "Unknown account",
                names.Role(assignment.RoleId),
                assignment.ScopeType,
                armNames,
                reason);
            var sourceArmNames = assignment.ArmIds.Select(names.Arm).ToArray();

            var mapped = new List<Guid>();
            string? unmatched = null;
            foreach (var armId in assignment.ArmIds)
            {
                if (armsById.TryGetValue(armId, out var arm) && targetArms.TryGetValue((arm.ClassLevelId, arm.LabelKey), out var match))
                {
                    mapped.Add(match.Id);
                }
                else
                {
                    unmatched ??= names.Arm(armId);
                }
            }

            var reason = assignment.AdminAccountId == actorId ? "Your own roles are copied by another Super Admin."
                : account is null ? "The account no longer exists."
                : account.Status == AdminAccountStatus.Deactivated ? "The account is deactivated."
                : role is null || role.Status == RoleStatus.Archived ? "The role is archived."
                : role.Id == SeededRoles.SuperAdminId ? "The Super Admin role is the account's own flag, never an assignment."
                : RoleScopeGuard.ValidateAssignable(role.Privileges, assignment.ScopeType) is { IsFailure: true } unassignable
                    ? unassignable.Error.Description
                : unmatched is not null ? $"{unmatched} has no class in {to.Name}."
                : assignment.ScopeType == ScopeType.ArmList
                    && (scopeGrants.Count == 0
                        || RoleScopeGuard.ValidateGrantWithinActorScope(actorScopeIsSchoolWide, actorArmIds, ScopeType.ArmList, mapped).IsFailure)
                    ? "You do not hold role.scope.assign over these classes."
                : held.Any(existing => Covers(existing, assignment.AdminAccountId, assignment.RoleId, assignment.ScopeType, mapped))
                    ? $"Already assigned in {to.Name}."
                : null;

            var copy = reason is null
                ? RoleAssignment.Create(Guid.CreateVersion7(), assignment.AdminAccountId, assignment.RoleId, to.Id, assignment.ScopeType, mapped, actorId)
                : null;
            if (reason is null && copy!.IsFailure)
            {
                reason = copy.Error.Description;
            }

            if (reason is not null)
            {
                skipped.Add(Row(sourceArmNames, reason));
                continue;
            }

            var created = copy!.Value;
            held.Add(created);
            copied.Add(Row([.. created.ArmIds.Select(names.Arm)], null));
            if (request.DryRun)
            {
                continue;
            }

            await assignments.AddAsync(created, cancellationToken).ConfigureAwait(false);
            await auditSink.RecordAsync(
                created.ScopeType == ScopeType.SchoolWide ? Privileges.Role.Assign : Privileges.Role.ScopeAssign,
                EntityType,
                created.Id.ToString("D", CultureInfo.InvariantCulture),
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["copiedFrom"] = assignment.Id.ToString("D", CultureInfo.InvariantCulture),
                    ["fromSessionId"] = from.Id.ToString("D", CultureInfo.InvariantCulture),
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new AssignmentCopyResultDto(request.DryRun, from.Name, to.Name, copied, skipped));
    }

    // The target already gives this account this role at least as widely: school-wide, or over every one of the classes.
    private static bool Covers(RoleAssignment existing, Guid accountId, Guid roleId, ScopeType scopeType, List<Guid> armIds) =>
        existing.AdminAccountId == accountId
        && existing.RoleId == roleId
        && (existing.ScopeType == ScopeType.SchoolWide
            || (scopeType == ScopeType.ArmList && armIds.All(existing.ArmIds.Contains)));
}
