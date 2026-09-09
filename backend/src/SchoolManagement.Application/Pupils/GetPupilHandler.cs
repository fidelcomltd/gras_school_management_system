using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils;

/// <summary>Handles <see cref="GetPupilQuery"/>. See <see cref="PupilAccessGuard"/>'s remarks for the scope shape.</summary>
internal sealed class GetPupilQueryHandler(
    IPupilRepository pupils,
    IEffectivePrivilegeProvider grantsProvider,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetPupilQuery, Result<PupilDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilDto>> HandleAsync(GetPupilQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<PupilDto>(Error.Unauthenticated("authentication.required", "Sign in to perform this action."));
        }

        var grants = await grantsProvider.GetGrantsAsync(userId, cancellationToken).ConfigureAwait(false);
        var scope = PupilAccessGuard.Resolve(grants, Privileges.Pupil.View);

        if (scope == PupilAccessScope.Forbidden)
        {
            return Result.Failure<PupilDto>(Error.Forbidden(
                "pupil.view_forbidden", "You do not hold the privilege required to view this pupil."));
        }

        var pupil = await pupils.FindReadOnlyByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (pupil is null)
        {
            return Result.Failure<PupilDto>(Error.NotFound("pupil.not_found", "No pupil was found with that id."));
        }

        if (scope == PupilAccessScope.ArmRestricted)
        {
            // The pupil has no arm reference at all today (see PupilAccessGuard's remarks), so it is
            // never within an arm-scoped grant's reach — 403, matching how a resolved-but-out-of-scope
            // target behaves everywhere else in this codebase (PrivilegeDecision.IsAuthorized).
            return Result.Failure<PupilDto>(Error.Forbidden(
                "pupil.view_forbidden", "You do not hold the privilege required to view this pupil."));
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return Result.Success(PupilMapper.ToDto(pupil, today));
    }
}
