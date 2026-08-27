namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// Resolves a pupil to their arm of record for the active term, from the pupil's OPEN ENROLMENT.
/// </summary>
/// <remarks>
/// Spec 4.2.1 and spec 9.2 are explicit that this must never be a client-supplied arm id: "a
/// client-supplied arm id is an attack surface." A SEAM — no pupil/enrolment module exists yet, so
/// no route uses <see cref="ScopeParameterKind.Pupil"/> in TASK-0002 (see the task card's
/// out-of-scope list: "any privilege check on a route that does not exist yet"). Implement this
/// against the pupil/enrolment module when it lands.
/// </remarks>
public interface IPupilArmOfRecordLookup
{
    /// <summary>Returns the pupil's current arm, or <see langword="null"/> if it cannot be resolved.</summary>
    /// <param name="pupilId">The pupil to resolve.</param>
    /// <param name="cancellationToken">Propagated to any underlying query.</param>
    Task<Guid?> GetArmIdAsync(Guid pupilId, CancellationToken cancellationToken);
}
