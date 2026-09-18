using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Security;

/// <summary>
/// Ties one <c>admin_account</c> to one <see cref="Role"/> with a scope (spec 6.1.5): "A record
/// joining one admin account, one role, one session, and either the flag school_wide or a set of arm
/// identifiers" (spec 4.2). This is what actually grants a role — <see cref="Role"/> itself "has no
/// scope of its own" (spec 4.2).
/// </summary>
/// <remarks>
/// <para>
/// Escalation rules 1 and 3 (spec 6.1.7) are NOT enforced here — like <see cref="Role"/>'s own
/// remarks say of rule 2, they need data this entity never sees (the ACTING account's id and its own
/// effective grants). See <see cref="RolePrivilegeEscalationGuard.ValidateNotSelfAssignment"/> (rule
/// 1) and <see cref="RoleScopeGuard.ValidateGrantWithinActorScope"/> (rule 3), both called by the
/// creating/revoking handler.
/// </para>
/// <para>
/// <see cref="SessionId"/> is <see langword="null"/> only for the seeded Super Admin role's
/// assignment (spec 6.1.5, 4.2.2: "sessionless and permanent"). TASK-0030 does not build a route that
/// can ever produce one — <c>CreateRoleAssignmentHandler</c> rejects an attempt to assign
/// <see cref="SeededRoles.SuperAdminId"/> outright, because <c>is_super_admin</c> is a flag bypass,
/// not a role assignment (human ruling 2026-09-05: spec 6.1.7 rule 4 governs over 4.2.2's looser
/// wording — see <c>AdminAccount</c>'s remarks). The <c>isSuperAdminAssignment</c> parameter on
/// <see cref="Create"/> exists so the schema and this validation already match spec
/// 6.1.5's literal field table, without a later migration, whenever that ruling is revisited.
/// </para>
/// </remarks>
public sealed class RoleAssignment : Entity<Guid>, IAuditableEntity
{
    private readonly List<Guid> _armIds = [];

    private RoleAssignment(
        Guid id,
        Guid adminAccountId,
        Guid roleId,
        Guid? sessionId,
        ScopeType scopeType,
        IEnumerable<Guid> armIds,
        Guid grantedBy)
        : base(id)
    {
        AdminAccountId = adminAccountId;
        RoleId = roleId;
        SessionId = sessionId;
        ScopeType = scopeType;
        _armIds.AddRange(armIds);
        GrantedBy = grantedBy;
        Status = RoleAssignmentStatus.Active;
    }

    // EF Core materialisation constructor.
    private RoleAssignment()
        : base()
    {
    }

    /// <summary>The account this assignment grants a role to (spec 6.1.5). Never equal to <see cref="GrantedBy"/> (rule 1).</summary>
    public Guid AdminAccountId { get; private set; }

    /// <summary>The role being granted. Must reference an active role at creation time; the caller checks this.</summary>
    public Guid RoleId { get; private set; }

    /// <summary>
    /// The session this assignment applies to, or <see langword="null"/> only for the sessionless,
    /// permanent Super Admin assignment (spec 6.1.5, 4.2.2) — see the type remarks.
    /// </summary>
    public Guid? SessionId { get; private set; }

    /// <summary>School-wide, or limited to <see cref="ArmIds"/> (spec 6.1.5).</summary>
    public ScopeType ScopeType { get; private set; }

    /// <summary>
    /// Non-empty only when <see cref="ScopeType"/> is <see cref="Security.ScopeType.ArmList"/>. Every
    /// id here must belong to <see cref="SessionId"/> — the caller checks this (needs a repository
    /// lookup this entity cannot perform).
    /// </summary>
    public IReadOnlyList<Guid> ArmIds => _armIds;

    /// <summary>The acting account that created this assignment. Never equal to <see cref="AdminAccountId"/> (rule 1).</summary>
    public Guid GrantedBy { get; private set; }

    /// <summary>Active or revoked (spec 6.1.5).</summary>
    public RoleAssignmentStatus Status { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new, active assignment (spec 6.1.5). The caller must already have checked: the
    /// target account is not deactivated, the role is active and not <c>Super Admin</c>, the session
    /// exists, every arm in <paramref name="armIds"/> belongs to <paramref name="sessionId"/>, rule 1
    /// (<paramref name="grantedBy"/> not equal to <paramref name="adminAccountId"/>) and rule 3 — none
    /// of those lookups is available to this entity.
    /// </summary>
    /// <param name="id">The new assignment's identifier.</param>
    /// <param name="adminAccountId">The account receiving the role.</param>
    /// <param name="roleId">The role being granted.</param>
    /// <param name="sessionId">
    /// The session this assignment applies to. Must be non-null unless
    /// <paramref name="isSuperAdminAssignment"/> is <see langword="true"/>.
    /// </param>
    /// <param name="scopeType">School-wide or arm-list.</param>
    /// <param name="armIds">Required and non-empty when <paramref name="scopeType"/> is arm-list; otherwise must be empty.</param>
    /// <param name="grantedBy">The acting account.</param>
    /// <param name="isSuperAdminAssignment">
    /// See the type remarks — TASK-0030 never passes <see langword="true"/> from an HTTP endpoint.
    /// </param>
    public static Result<RoleAssignment> Create(
        Guid id,
        Guid adminAccountId,
        Guid roleId,
        Guid? sessionId,
        ScopeType scopeType,
        IReadOnlyCollection<Guid> armIds,
        Guid grantedBy,
        bool isSuperAdminAssignment = false)
    {
        ArgumentNullException.ThrowIfNull(armIds);

        if (id == Guid.Empty)
        {
            return Result.Failure<RoleAssignment>(
                Error.Validation("role_assignment.id_required", "Id must not be empty."));
        }

        if (adminAccountId == Guid.Empty)
        {
            return Result.Failure<RoleAssignment>(Error.Validation(
                "role_assignment.admin_account_id_required", "AdminAccountId must not be empty."));
        }

        if (roleId == Guid.Empty)
        {
            return Result.Failure<RoleAssignment>(
                Error.Validation("role_assignment.role_id_required", "RoleId must not be empty."));
        }

        if (grantedBy == Guid.Empty)
        {
            return Result.Failure<RoleAssignment>(
                Error.Validation("role_assignment.granted_by_required", "GrantedBy must not be empty."));
        }

        if (isSuperAdminAssignment)
        {
            if (sessionId is not null)
            {
                return Result.Failure<RoleAssignment>(Error.Validation(
                    "role_assignment.super_admin_session_must_be_null",
                    "The Super Admin assignment is sessionless and permanent."));
            }
        }
        else if (sessionId is null || sessionId == Guid.Empty)
        {
            return Result.Failure<RoleAssignment>(
                Error.Validation("role_assignment.session_id_required", "SessionId must not be empty."));
        }

        switch (scopeType)
        {
            case ScopeType.ArmList when armIds.Count == 0:
                return Result.Failure<RoleAssignment>(Error.Validation(
                    "role_assignment.arm_ids_required",
                    "At least one arm is required for an arm-scoped assignment."));

            case ScopeType.SchoolWide when armIds.Count > 0:
                return Result.Failure<RoleAssignment>(Error.Validation(
                    "role_assignment.arm_ids_not_allowed",
                    "A school-wide assignment must not name any arms."));
        }

        if (armIds.Any(armId => armId == Guid.Empty))
        {
            return Result.Failure<RoleAssignment>(
                Error.Validation("role_assignment.arm_id_invalid", "Every arm id must be non-empty."));
        }

        var normalizedArmIds = armIds.Distinct().OrderBy(armId => armId).ToArray();

        return Result.Success(new RoleAssignment(
            id, adminAccountId, roleId, sessionId, scopeType, normalizedArmIds, grantedBy));
    }

    /// <summary>Moves to <see cref="RoleAssignmentStatus.Revoked"/> (spec 6.1.5, 6.1.10). Idempotent-safe: revoking an already-revoked row is a no-op.</summary>
    public void Revoke() => Status = RoleAssignmentStatus.Revoked;
}
