using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Classes;

/// <summary>What adding one pupil to an arm does to its capacity (spec 6.4.6).</summary>
/// <param name="Capacity">The arm's soft limit.</param>
/// <param name="EnrolledAfter">The open, non-pending enrolments once this pupil is added.</param>
/// <param name="OverCapacity">Whether the pupil takes the arm past its capacity.</param>
/// <param name="CanOverride">Whether the caller holds <c>arm.capacity.override</c> for this arm.</param>
internal sealed record ArmCapacityCheck(int Capacity, int EnrolledAfter, bool OverCapacity, bool CanOverride);

/// <summary>
/// The soft capacity limit shared by every route that puts one pupil into an arm: admission approval, a transfer and a
/// reactivation (spec 6.4.6). Over capacity blocks only a caller without <c>arm.capacity.override</c>; an override is
/// audited with the arm, the pupil and the resulting count.
/// </summary>
internal sealed class ArmCapacityGuard(
    IEnrolmentRepository enrolments,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
{
    private const string ArmEntityType = "arm";

    /// <summary>Reads the arm's count and the caller's override grant. Writes nothing.</summary>
    public async Task<ArmCapacityCheck> CheckAsync(Arm arm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arm);

        var openCount = await enrolments.CountOpenExcludingPendingByArmAsync(arm.Id, cancellationToken).ConfigureAwait(false);

        if (openCount < arm.Capacity)
        {
            return new ArmCapacityCheck(arm.Capacity, openCount + 1, OverCapacity: false, CanOverride: false);
        }

        var grants = await effectivePrivilegeProvider
            .GetGrantsAsync(currentUser.UserId ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        var canOverride = PupilAccessGuard.Resolve(grants, Privileges.Arm.CapacityOverride) switch
        {
            PupilAccessScope.SchoolWide => true,
            PupilAccessScope.ArmRestricted => PupilAccessGuard.ResolveArmIds(grants, Privileges.Arm.CapacityOverride).Contains(arm.Id),
            _ => false,
        };

        return new ArmCapacityCheck(arm.Capacity, openCount + 1, OverCapacity: true, canOverride);
    }

    /// <summary>
    /// Enforces a <see cref="CheckAsync"/> result: a failure when over capacity without the override, and the override's
    /// own audit event when over capacity with it.
    /// </summary>
    public async Task<Result> EnforceAsync(
        Arm arm, string armDisplayName, Guid pupilId, ArmCapacityCheck check, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arm);
        ArgumentNullException.ThrowIfNull(check);

        if (!check.OverCapacity)
        {
            return Result.Success();
        }

        if (!check.CanOverride)
        {
            return Result.Failure(Error.Conflict(
                "arm.at_capacity",
                $"{armDisplayName} is at its capacity of {arm.Capacity}. Raise the capacity or choose another arm."));
        }

        await auditSink.RecordAsync(
            Privileges.Arm.CapacityOverride,
            ArmEntityType,
            arm.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pupilId"] = pupilId.ToString("D", CultureInfo.InvariantCulture),
                ["capacity"] = arm.Capacity,
                ["resultingCount"] = check.EnrolledAfter,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
