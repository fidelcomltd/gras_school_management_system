using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// Handles <see cref="DeleteArmCommand"/>.
/// </summary>
/// <remarks>
/// Spec 6.4.7's precondition is "no enrolment has ever existed." Enrolment (spec 07 §6.5/§6.6) does
/// not exist ANYWHERE in this codebase yet, so the precondition cannot be false today — this is not a
/// bypass of the kind <c>SuperAdminFlagEffectivePrivilegeProvider</c> is: the condition this handler
/// would check is currently unsatisfiable in the other direction (an enrolment can never have
/// existed), so permitting delete unconditionally is the CORRECT result, not a more-permissive
/// approximation of it. The seam for the future check is still DEFERRED and named here so the
/// enrolment card replaces this remark with a real lookup rather than rediscovering the gap.
/// </remarks>
internal sealed class DeleteArmHandler(
    IArmRepository arms,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<DeleteArmCommand, Result>
{
    private const string EntityType = "arm";

    /// <inheritdoc />
    public async Task<Result> HandleAsync(DeleteArmCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arm = await arms.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (arm is null)
        {
            return Result.Failure(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        await arms.RemoveAsync(arm, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Arm.Delete,
            EntityType,
            arm.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
