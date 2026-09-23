using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils.Records;

/// <summary>
/// The privilege check for a pupil's sub-records, done in the handler because a route-level pupil scope cannot resolve a
/// pupil with no open enrolment (a pending admission, a leaver) and would refuse even a school-wide grant. Same rule as
/// <see cref="GetPupilQueryHandler"/>: a school-wide grant covers every pupil; an arm-scoped grant covers only a pupil
/// whose open enrolment is in one of its arms.
/// </summary>
internal sealed class PupilRecordAccess(
    IPupilRepository pupils, IEffectivePrivilegeProvider grantsProvider, IPupilArmOfRecordLookup armOfRecordLookup, ICurrentUser currentUser)
{
    /// <summary>The pupil, when it exists and <see cref="ICurrentUser"/> holds <paramref name="privilege"/> over it.</summary>
    public async Task<Result<Pupil>> CheckAsync(Guid pupilId, string privilege, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<Pupil>(Error.Unauthenticated("authentication.required", "Sign in to perform this action."));
        }

        var grants = await grantsProvider.GetGrantsAsync(userId, cancellationToken).ConfigureAwait(false);
        var scope = PupilAccessGuard.Resolve(grants, privilege);
        if (scope == PupilAccessScope.Forbidden)
        {
            return Forbidden(privilege);
        }

        if (await pupils.FindReadOnlyByIdAsync(pupilId, cancellationToken).ConfigureAwait(false) is not { } pupil)
        {
            return Result.Failure<Pupil>(Error.NotFound("pupil.not_found", "No pupil was found with that id."));
        }

        if (scope == PupilAccessScope.ArmRestricted
            && (await armOfRecordLookup.GetArmIdAsync(pupilId, cancellationToken).ConfigureAwait(false) is not { } armId
                || !PupilAccessGuard.ResolveArmIds(grants, privilege).Contains(armId)))
        {
            return Forbidden(privilege);
        }

        return Result.Success(pupil);
    }

    private static Result<Pupil> Forbidden(string privilege) =>
        Result.Failure<Pupil>(Error.Forbidden("pupil.record_forbidden", $"You do not hold {privilege} for this pupil."));
}
