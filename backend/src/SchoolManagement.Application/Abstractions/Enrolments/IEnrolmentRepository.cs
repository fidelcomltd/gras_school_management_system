using SchoolManagement.Domain.Enrolments;

namespace SchoolManagement.Application.Abstractions.Enrolments;

/// <summary>Persistence port for <see cref="Enrolment"/>.</summary>
/// <remarks>
/// Pupil-scale, same convention as <c>IPupilRepository</c> — reads go through targeted,
/// database-side queries rather than "load everything and filter in memory"
/// (<c>IArmRepository</c>'s admin-configuration-sized shape does not fit an enrolment table that
/// grows with every admission, transfer and promotion).
/// </remarks>
public interface IEnrolmentRepository
{
    /// <summary>Adds a new enrolment. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(Enrolment enrolment, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a TRACKED open enrolment for <paramref name="pupilId"/>, for a command that will close
    /// or transfer it (TASK-0051 and later cards). <see langword="null"/> when the pupil has no
    /// open enrolment.
    /// </summary>
    Task<Enrolment?> FindOpenTrackedByPupilIdAsync(Guid pupilId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a read-only open enrolment for <paramref name="pupilId"/> — the arm-of-record lookup
    /// (spec 02 §5.2: "asking which arm a pupil is in is a query against enrolment for the open
    /// row"). <see langword="null"/> when the pupil has no open enrolment. <c>AsNoTracking</c>.
    /// </summary>
    Task<Enrolment?> FindOpenReadOnlyByPupilIdAsync(Guid pupilId, CancellationToken cancellationToken);

    /// <summary>
    /// The number of OPEN enrolments in <paramref name="armId"/>, excluding any pupil whose status
    /// is <see cref="Domain.Pupils.PupilStatus.Pending"/> — spec 07 §6.5.14's pending-exclusion
    /// invariant applies to "every ... enrolment count, every capacity calculation" as much as to a
    /// roster. This is the count spec 06 §6.4.6's soft capacity limit reads; enforcing or overriding
    /// that limit is a later card's endpoint.
    /// </summary>
    Task<int> CountOpenExcludingPendingByArmAsync(Guid armId, CancellationToken cancellationToken);
}
