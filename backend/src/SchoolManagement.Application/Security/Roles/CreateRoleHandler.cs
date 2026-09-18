using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>Handles <see cref="CreateRoleCommand"/>.</summary>
internal sealed class CreateRoleCommandHandler(
    IRoleRepository roles,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CreateRoleCommand, Result<RoleDto>>
{
    private const string EntityType = "role";

    /// <inheritdoc />
    public async Task<Result<RoleDto>> HandleAsync(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } actorId)
        {
            return Result.Failure<RoleDto>(Error.Unauthenticated(
                "authentication.required",
                "Sign in to perform this action."));
        }

        // Pre-checked rather than relying on the unique index alone (AGENTS.md §4's recipe) — a clean
        // 409 with a stable error code beats translating a constraint-violation exception.
        var normalizedNameKey = request.Name.Trim().ToLowerInvariant();

        if (await roles.NameExistsAsync(normalizedNameKey, excludingId: null, cancellationToken)
            .ConfigureAwait(false))
        {
            return Result.Failure<RoleDto>(Error.Conflict(
                "role.name_duplicate",
                "A role with that name already exists."));
        }

        // Built in-memory, not yet persisted: validates the reserved name, field lengths and privilege
        // registry membership (unknown codes named as the offender) before the escalation check ever
        // runs, so a genuinely unrecognised code is reported as such rather than misreported as an
        // escalation attempt against a code that was never valid to begin with.
        var creation = Role.Create(Guid.CreateVersion7(), request.Name, request.Description, request.Privileges);

        if (creation.IsFailure)
        {
            return Result.Failure<RoleDto>(creation.Error);
        }

        var role = creation.Value;

        var grants = await effectivePrivilegeProvider.GetGrantsAsync(actorId, cancellationToken)
            .ConfigureAwait(false);
        var actorPrivileges = grants.Select(grant => grant.Privilege).ToArray();

        // Spec 6.1.7 rule 2: a brand-new role has no existing privileges, so every requested one is an
        // "add".
        var escalation = RolePrivilegeEscalationGuard.ValidateAddition(
            existingPrivileges: [],
            requestedPrivileges: role.Privileges,
            actorPrivileges: actorPrivileges);

        if (escalation.IsFailure)
        {
            // Spec 6.1.7 preamble: "producing an audit event on rejection so that an attempt is
            // visible even though it failed." Nothing was persisted, so there is no entity id to name.
            // RecordRejectionAsync (not RecordAsync): this row must survive the ambient
            // transaction's rollback below, which a same-transaction write would not (TASK-0048).
            await auditSink.RecordRejectionAsync(
                "role.privilege_escalation",
                EntityType,
                entityId: null,
                metadata: null,
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<RoleDto>(escalation.Error);
        }

        await roles.AddAsync(role, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Role.Create,
            EntityType,
            role.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(RoleMapper.ToDto(role));
    }
}
