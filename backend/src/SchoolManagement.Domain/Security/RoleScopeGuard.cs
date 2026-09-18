using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Security;

/// <summary>
/// Enforces spec 4.2's escalation rule at the point a role would be assigned with an arm-list
/// scope: "an attempt to create an arm-scoped assignment for a role containing a non-scopable
/// privilege is rejected at save time."
/// </summary>
/// <remarks>
/// Pure and persistence-free by design. Role and assignment persistence is out of scope for
/// TASK-0002 (see the task card); this guard is the reusable rule the future assignment-creation
/// handler calls, expressed so it is testable today without a database.
/// </remarks>
public static class RoleScopeGuard
{
    /// <summary>
    /// Checks whether <paramref name="rolePrivileges"/> may be granted with <paramref name="scopeType"/>.
    /// </summary>
    /// <param name="rolePrivileges">The privilege codes the role holds.</param>
    /// <param name="scopeType">The scope the assignment would use.</param>
    /// <returns>
    /// A success when the scope is <see cref="ScopeType.SchoolWide"/> (the rule only restricts
    /// arm-list scope) or every privilege in the role is scopable; otherwise a
    /// <see cref="ErrorType.Validation"/> failure carrying the exact message from spec 4.2.
    /// </returns>
    public static Result ValidateAssignable(IReadOnlyCollection<string> rolePrivileges, ScopeType scopeType)
    {
        ArgumentNullException.ThrowIfNull(rolePrivileges);

        if (scopeType != ScopeType.ArmList)
        {
            return Result.Success();
        }

        var nonScopable = rolePrivileges
            .Select(PrivilegeAliases.Resolve)
            .Where(code => PrivilegeRegistry.TryGet(code, out var definition) && definition is { Scopable: false })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        if (nonScopable.Length == 0)
        {
            return Result.Success();
        }

        var joined = string.Join(", ", nonScopable);

        // Exact wording from spec 4.2. Do not rephrase it: the acceptance criterion is the literal
        // sentence, and a school-facing message that has been "improved" is a silent contract break.
        return Result.Failure(Error.Validation(
            "role_assignment.non_scopable_privileges",
            $"This role contains privileges that cannot be limited to an arm: {joined}. Remove them " +
            "from the role, or assign the role school-wide."));
    }

    /// <summary>
    /// Enforces spec 6.1.7 rule 3 at the point a <see cref="RoleAssignment"/> would be created: "No
    /// account may grant a scope wider than its own." The caller resolves the acting account's OWN
    /// scope for the assigning privilege it is exercising (<c>role.assign</c> for a school-wide
    /// target, <c>role.scope.assign</c> for an arm-list target) via
    /// <c>IEffectivePrivilegeProvider</c> and passes it in as
    /// <paramref name="actorScopeIsSchoolWide"/>/<paramref name="actorArmIds"/> — this type stays
    /// pure and persistence-free, matching <see cref="ValidateAssignable"/>'s own shape.
    /// </summary>
    /// <remarks>
    /// Both <c>role.assign</c> and <c>role.scope.assign</c> are themselves non-scopable
    /// (<see cref="PrivilegeRegistry"/>), so today every actual grant of either is school-wide, and in
    /// practice this method's arm-list branch never rejects a real caller reached through the live
    /// assignment endpoint — it activates the moment that registration changes, or against a directly
    /// seeded assignment in a test, rather than being an always-true stub (the thing this whole card
    /// exists to stop building more of).
    /// </remarks>
    /// <param name="actorScopeIsSchoolWide">
    /// Whether the acting account holds the relevant assigning privilege through at least one
    /// school-wide grant — an unbounded own-scope, which always satisfies rule 3.
    /// </param>
    /// <param name="actorArmIds">
    /// The union of arm ids the acting account's OWN grant(s) of the relevant privilege cover, when
    /// <paramref name="actorScopeIsSchoolWide"/> is <see langword="false"/>. Ignored otherwise.
    /// </param>
    /// <param name="requestedScopeType">The scope the new assignment would use.</param>
    /// <param name="requestedArmIds">The arms the new assignment would name — empty for school-wide.</param>
    /// <returns>
    /// A success when the requested scope does not exceed the actor's own; otherwise an
    /// <see cref="ErrorType.Forbidden"/> failure, error code <c>role_assignment.scope_exceeds_actor</c>.
    /// </returns>
    public static Result ValidateGrantWithinActorScope(
        bool actorScopeIsSchoolWide,
        IReadOnlySet<Guid> actorArmIds,
        ScopeType requestedScopeType,
        IReadOnlyCollection<Guid> requestedArmIds)
    {
        ArgumentNullException.ThrowIfNull(actorArmIds);
        ArgumentNullException.ThrowIfNull(requestedArmIds);

        if (actorScopeIsSchoolWide)
        {
            return Result.Success();
        }

        if (requestedScopeType == ScopeType.SchoolWide)
        {
            return Result.Failure(ScopeExceedsActorError);
        }

        return requestedArmIds.All(actorArmIds.Contains)
            ? Result.Success()
            : Result.Failure(ScopeExceedsActorError);
    }

    private static readonly Error ScopeExceedsActorError = Error.Forbidden(
        "role_assignment.scope_exceeds_actor",
        "You cannot grant a scope wider than your own. You may only assign arms that your own " +
        "assignment already covers.");
}
