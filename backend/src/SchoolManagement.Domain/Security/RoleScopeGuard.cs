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
}
