using SchoolManagement.Domain.Enrolments;

namespace SchoolManagement.Application.Abstractions.Enrolments;

/// <summary>
/// One pupil on an arm's roster (spec 09 §6.7.4) — the row identity a score sheet or the computation
/// engine (TASK-0071) needs, nothing else.
/// </summary>
/// <param name="PupilId">The pupil's id.</param>
/// <param name="RegistrationNumber">Null only if somehow unissued; every <see cref="Domain.Pupils.PupilStatus.Active"/> pupil has one in practice (spec 6.5.10).</param>
/// <param name="Surname">Row-ordering key (spec 6.7.4: "Surname ascending... never affected by marks").</param>
/// <param name="FirstName">For the composed display name.</param>
/// <param name="MiddleName">For the composed display name. Optional.</param>
public sealed record ArmRosterPupil(Guid PupilId, string? RegistrationNumber, string Surname, string FirstName, string? MiddleName);

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

    /// <summary>
    /// The arm's score-sheet roster (spec 09 §6.7.4, TASK-0076): every pupil with an OPEN enrolment in
    /// <paramref name="armId"/> whose <see cref="Domain.Pupils.PupilStatus"/> is
    /// <see cref="Domain.Pupils.PupilStatus.Active"/> — pending is already excluded by
    /// <c>PupilConfiguration</c>'s query filter, checked again here the same defence-in-depth way
    /// <see cref="CountOpenExcludingPendingByArmAsync"/> does, because Transferred/Withdrawn/Graduated
    /// pupils must not appear even if a future change ever left their enrolment open. Ordered surname
    /// then id (fixed, never affected by marks). <c>AsNoTracking</c>. TASK-0071's computation engine
    /// reuses this for the same roster.
    /// </summary>
    Task<IReadOnlyList<ArmRosterPupil>> ListActiveRosterByArmAsync(Guid armId, CancellationToken cancellationToken);
}
