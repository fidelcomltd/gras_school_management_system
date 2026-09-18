using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Security;

/// <summary>
/// Enforces spec 6.1.7 rule 2 at the point a role's privilege set would change: "No account may add
/// a privilege to a role that it does not itself currently hold. This stops an account holding
/// <c>role.update</c> but not <c>settings.grading.update</c> from writing the latter into a role and
/// then assigning that role to a colleague as a proxy."
/// </summary>
/// <remarks>
/// <para>
/// Pure and persistence-free, copying <see cref="RoleScopeGuard"/>'s shape: no repository, no audit
/// write, no privilege resolution — the caller (a command handler) supplies the role's existing
/// privileges, the requested new set, and the acting account's own resolved grants, and is
/// responsible for writing the audit event spec 6.1.7's preamble requires on rejection ("all
/// enforced server-side, all producing an audit event on rejection so that an attempt is visible
/// even though it failed") — this type has no seam to do that itself.
/// </para>
/// <para>
/// Evaluated against the ADDED privileges only: a privilege already on the role (or already absent
/// and staying absent) is never an "add", so leaving it untouched never trips this guard — matching
/// the approved delta's exact wording. On <c>POST /roles</c> (create), the caller passes an empty
/// <c>existingPrivileges</c> set, so every requested privilege is, correctly, an
/// addition. REMOVAL is unrestricted by this guard (rule 2 governs additions only) — a caller may
/// always narrow a role's privileges.
/// </para>
/// <para>
/// One consequence spec 6.1.7 states explicitly and this guard does not special-case: "a Super Admin
/// can always widen a role, because a Super Admin holds everything." A Super Admin's
/// <c>actorPrivileges</c> already contains the whole register (the <c>is_super_admin</c> flag path
/// every <c>IEffectivePrivilegeProvider</c> implementation has resolved directly since TASK-0003, most
/// recently <c>RoleAssignmentEffectivePrivilegeProvider</c>), so no addition it makes can ever be an
/// offending one — the general rule already produces that outcome with no special path.
/// </para>
/// </remarks>
public static class RolePrivilegeEscalationGuard
{
    /// <summary>
    /// Checks whether moving a role from <paramref name="existingPrivileges"/> to
    /// <paramref name="requestedPrivileges"/> is permitted for an actor holding
    /// <paramref name="actorPrivileges"/>.
    /// </summary>
    /// <param name="existingPrivileges">
    /// The role's privileges before this change (canonical codes). Empty for a brand-new role.
    /// </param>
    /// <param name="requestedPrivileges">The role's privileges after this change (canonical codes).</param>
    /// <param name="actorPrivileges">The acting account's own resolved, canonical privilege codes.</param>
    /// <returns>
    /// A success when every newly-requested privilege is one the actor already holds; otherwise an
    /// <see cref="ErrorType.Forbidden"/> failure carrying the exact message from the approved contract
    /// delta (spec 6.1.7 rule 2) and error code <c>role.privilege_escalation</c>.
    /// </returns>
    public static Result ValidateAddition(
        IReadOnlyCollection<string> existingPrivileges,
        IReadOnlyCollection<string> requestedPrivileges,
        IReadOnlyCollection<string> actorPrivileges)
    {
        ArgumentNullException.ThrowIfNull(existingPrivileges);
        ArgumentNullException.ThrowIfNull(requestedPrivileges);
        ArgumentNullException.ThrowIfNull(actorPrivileges);

        var existingSet = new HashSet<string>(existingPrivileges, StringComparer.Ordinal);
        var actorSet = new HashSet<string>(actorPrivileges, StringComparer.Ordinal);

        // Register order, per the approved delta ("Several, comma-joined in register order") — never
        // alphabetical, which would put "arm.view" ahead of "admin.view" and disagree with the spec's
        // own worked example ordering.
        var registerOrder = PrivilegeRegistry.All
            .Select((definition, index) => (definition.Code, index))
            .ToDictionary(pair => pair.Code, pair => pair.index, StringComparer.Ordinal);

        var offending = requestedPrivileges
            .Where(code => !existingSet.Contains(code))
            .Where(code => !actorSet.Contains(code))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => registerOrder.TryGetValue(code, out var index) ? index : int.MaxValue)
            .ToArray();

        if (offending.Length == 0)
        {
            return Result.Success();
        }

        var joined = string.Join(", ", offending);

        // Exact wording from the approved contract delta (spec 6.1.7 rule 2). Do not rephrase it —
        // see RoleScopeGuard's own remarks for why a "corrected" message is a silent contract break.
        var message = offending.Length == 1
            ? $"You do not hold {joined} and cannot add it to a role."
            : $"You do not hold {joined} and cannot add them to a role.";

        return Result.Failure(Error.Forbidden("role.privilege_escalation", message));
    }

    /// <summary>
    /// Enforces spec 6.1.7 rule 1 at the point a <see cref="RoleAssignment"/> would be created,
    /// edited or revoked: "No account may create, edit or revoke a role_assignment where
    /// admin_account_id equals its own id." Placed here rather than in a third guard type, per
    /// TASK-0030's card — this rule is an escalation control in the same family as rule 2 (both stop
    /// an account from expanding its own or a colleague's reach through a channel other than a Super
    /// Admin explicitly choosing to).
    /// </summary>
    /// <param name="actingAccountId">The account performing the create/edit/revoke.</param>
    /// <param name="targetAdminAccountId">The <c>admin_account_id</c> the assignment names or would name.</param>
    /// <returns>
    /// A success when the two ids differ; otherwise a <see cref="ErrorType.Forbidden"/> failure
    /// carrying spec 6.1.7 rule 1's exact message and error code <c>role_assignment.self_assignment_forbidden</c>.
    /// </returns>
    public static Result ValidateNotSelfAssignment(Guid actingAccountId, Guid targetAdminAccountId)
    {
        if (actingAccountId != targetAdminAccountId)
        {
            return Result.Success();
        }

        // Exact wording from spec 6.1.7 rule 1. Do not rephrase it — see this file's own remarks on
        // rule 2's message for why a "corrected" message is a silent contract break.
        return Result.Failure(Error.Forbidden(
            "role_assignment.self_assignment_forbidden",
            "You cannot change your own roles. Ask another Super Admin."));
    }
}
