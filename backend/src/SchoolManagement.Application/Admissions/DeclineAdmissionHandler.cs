using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Admissions;

/// <summary>Handles <see cref="DeclineAdmissionCommand"/>.</summary>
/// <remarks>
/// School-wide <c>pupil.admission.approve</c> only — same reasoning as <c>UpdateAdmissionRecordHandler</c>
/// and <c>ApproveAdmissionCommandHandler</c>: a pending pupil has no open enrolment, so an arm-scoped
/// grant could never resolve a target arm here (see <c>AdmissionEndpoints.MapEndpoints</c>).
/// </remarks>
internal sealed class DeclineAdmissionCommandHandler(
    IPupilRepository pupils,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<DeclineAdmissionCommand, Result<PupilDto>>
{
    private const string EntityType = "pupil";

    /// <inheritdoc />
    public async Task<Result<PupilDto>> HandleAsync(DeclineAdmissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // TRACKED, not read-only: FindTrackedByIdAsync ignores the pending-exclusion query filter
        // (direct-id access is never subject to it — PupilRepository's own remarks), which is exactly
        // what a decline needs: the pupil IS pending, that is the whole point of this route.
        var pupil = await pupils.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (pupil is null)
        {
            return Result.Failure<PupilDto>(Error.NotFound(
                "admission.not_found", "No pending admission was found with that id."));
        }

        // Pupil.DeclineAdmission is the ONLY thing this command touches: no registration number
        // (RegistrationNumber is never written by any decline path) and no counter call at all — the
        // acceptance criterion this proves is "last_serial untouched", asserted directly against the
        // counter row, never inferred from the response carrying no number.
        var decline = pupil.DeclineAdmission();

        if (decline.IsFailure)
        {
            return Result.Failure<PupilDto>(decline.Error);
        }

        await auditSink.RecordAsync(
            Privileges.Pupil.AdmissionApprove,
            EntityType,
            pupil.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["outcome"] = "declined" },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            // Spec 6.5.14: "Requires a reason" — the mandatory reason travels on the audit event, the
            // same convention ChangeAdminAccountStatusCommandHandler already established, since
            // neither Pupil nor AdmissionRecord has a column to hold it.
            reason: request.Reason).ConfigureAwait(false);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        return Result.Success(PupilMapper.ToDto(pupil, today));
    }
}
