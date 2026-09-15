using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Enrolments;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// The real <see cref="IPupilArmOfRecordLookup"/> (TASK-0059): resolves a pupil's arm from their
/// OPEN <c>Enrolment</c> row, per spec 02 §5.2 — never a client-supplied arm id (spec 4.2.1, spec
/// 9.2). Replaces <c>NotYetImplementedPupilArmOfRecordLookup</c>, which threw because no
/// pupil/enrolment module existed yet.
/// </summary>
internal sealed class PupilArmOfRecordLookup(IEnrolmentRepository enrolments) : IPupilArmOfRecordLookup
{
    /// <inheritdoc />
    public async Task<Guid?> GetArmIdAsync(Guid pupilId, CancellationToken cancellationToken)
    {
        var openEnrolment = await enrolments
            .FindOpenReadOnlyByPupilIdAsync(pupilId, cancellationToken)
            .ConfigureAwait(false);

        return openEnrolment?.ArmId;
    }
}
