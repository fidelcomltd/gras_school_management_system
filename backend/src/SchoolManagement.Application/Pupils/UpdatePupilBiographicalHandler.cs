using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils;

/// <summary>Handles <see cref="UpdatePupilBiographicalCommand"/>. See <see cref="PupilAccessGuard"/>'s remarks for the scope shape.</summary>
internal sealed class UpdatePupilBiographicalHandler(
    IPupilRepository pupils,
    IEffectivePrivilegeProvider grantsProvider,
    IPupilArmOfRecordLookup armOfRecordLookup,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdatePupilBiographicalCommand, Result<PupilDto>>
{
    private const string EntityType = "pupil";

    /// <inheritdoc />
    public async Task<Result<PupilDto>> HandleAsync(
        UpdatePupilBiographicalCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<PupilDto>(Error.Unauthenticated("authentication.required", "Sign in to perform this action."));
        }

        // Spec 6.5.10: "the number is immutable and no ordinary edit path exists." Checked BEFORE
        // the scope/existence checks below so a caller who could never reach the record anyway still
        // gets the more specific conflict when they attempt the one thing this route can never do —
        // matching CreateArmHandler-family ordering ("build first, then guard") only loosely; here
        // the rule is absolute regardless of the record's state, so it is checked first.
        if (request.RegistrationNumber is not null)
        {
            return Result.Failure<PupilDto>(Error.Conflict(
                "pupil.registration_number_immutable",
                "The registration number cannot be changed through this endpoint. It is issued once, at admission approval, and never edited afterwards."));
        }

        var grants = await grantsProvider.GetGrantsAsync(userId, cancellationToken).ConfigureAwait(false);
        var scope = PupilAccessGuard.Resolve(grants, Privileges.Pupil.Update);

        if (scope == PupilAccessScope.Forbidden)
        {
            return Result.Failure<PupilDto>(Error.Forbidden(
                "pupil.update_forbidden", "You do not hold the privilege required to edit this pupil."));
        }

        var pupil = await pupils.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (pupil is null)
        {
            return Result.Failure<PupilDto>(Error.NotFound("pupil.not_found", "No pupil was found with that id."));
        }

        if (scope == PupilAccessScope.ArmRestricted)
        {
            // TASK-0059: same real arm-of-record resolution as GetPupilQueryHandler — see its remarks.
            var armId = await armOfRecordLookup.GetArmIdAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
            var allowedArmIds = PupilAccessGuard.ResolveArmIds(grants, Privileges.Pupil.Update);

            if (armId is not { } resolvedArmId || !allowedArmIds.Contains(resolvedArmId))
            {
                return Result.Failure<PupilDto>(Error.Forbidden(
                    "pupil.update_forbidden", "You do not hold the privilege required to edit this pupil."));
            }
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var update = pupil.UpdateBiographical(
            request.Surname,
            request.FirstName,
            request.MiddleName,
            request.Sex,
            request.DateOfBirth,
            today,
            request.Nationality,
            request.StateOfOrigin,
            request.Lga,
            request.HomeAddress,
            request.PreviousSchool,
            request.PreviousClass,
            request.OtherInformation);

        if (update.IsFailure)
        {
            return Result.Failure<PupilDto>(update.Error);
        }

        await auditSink.RecordAsync(
            Privileges.Pupil.Update,
            EntityType,
            pupil.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(PupilMapper.ToDto(pupil, today));
    }
}
