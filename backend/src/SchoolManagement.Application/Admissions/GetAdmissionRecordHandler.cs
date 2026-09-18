using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Admissions;

/// <summary>Handles <see cref="GetAdmissionRecordQuery"/>.</summary>
/// <remarks>
/// The read twin of <see cref="UpdateAdmissionRecordHandler"/>: same pending-only-404 rule, same
/// mapper — reused, not restated. School-wide <c>pupil.view</c> only (declared on the route in
/// <c>AdmissionEndpoints.MapEndpoints</c>, not resolved here) — a pending pupil has no open
/// enrolment, so an arm-scoped grant could never resolve a target arm, the exact reasoning
/// <c>ListAdmissionsQueueQuery</c>'s own route already established for the queue read. No write:
/// no audit row, no entity mutation.
/// </remarks>
internal sealed class GetAdmissionRecordQueryHandler(
    IAdmissionRecordRepository admissionRecords,
    IPupilRepository pupils,
    IClassLevelRepository classLevels)
    : IRequestHandler<GetAdmissionRecordQuery, Result<AdmissionRecordDto>>
{
    /// <inheritdoc />
    public async Task<Result<AdmissionRecordDto>> HandleAsync(
        GetAdmissionRecordQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // FindReadOnlyByIdAsync ignores the pending-exclusion filter (spec 6.5.14) — direct-id access
        // is never subject to it (PupilRepository's own remarks), which is exactly what this route
        // needs: it exists ONLY to read a pending record, the same reasoning
        // UpdateAdmissionRecordHandler already applies.
        var pupil = await pupils.FindReadOnlyByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (pupil is null || pupil.Status != PupilStatus.Pending)
        {
            return Result.Failure<AdmissionRecordDto>(Error.NotFound(
                "admission_record.not_found", "No pending admission was found with that id."));
        }

        var record = await admissionRecords.FindReadOnlyByPupilIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            // Cannot happen given CreatePupilHandler's same-transaction invariant — defensive, not a
            // reachable branch in normal operation.
            return Result.Failure<AdmissionRecordDto>(Error.NotFound(
                "admission_record.not_found", "No pending admission was found with that id."));
        }

        var allLevels = await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var classAdmittedIntoName = allLevels
            .FirstOrDefault(level => level.Id == record.ClassAdmittedInto)?.Name;

        return Result.Success(AdmissionRecordMapper.ToDto(record, classAdmittedIntoName));
    }
}
