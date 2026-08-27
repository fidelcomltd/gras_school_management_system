using SchoolManagement.Application.Abstractions.Authorization;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// Default <see cref="IPupilArmOfRecordLookup"/>: throws. No pupil/enrolment module exists yet, so
/// no route declares <see cref="ScopeParameterKind.Pupil"/> in TASK-0002 — this registration exists
/// only so <c>IScopeResolver</c> can be constructed; it is never reached at runtime by this card's
/// routes. Implement against the pupil/enrolment module when it lands.
/// </summary>
internal sealed class NotYetImplementedPupilArmOfRecordLookup : IPupilArmOfRecordLookup
{
    /// <inheritdoc />
    public Task<Guid?> GetArmIdAsync(Guid pupilId, CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Pupil arm-of-record lookup is not implemented yet — no route uses " +
            "ScopeParameterKind.Pupil as of TASK-0002. Implement this against the pupil/enrolment " +
            "module when it lands.");
}

/// <summary>
/// Default <see cref="IResultSetArmLookup"/>: throws, for the same reason as
/// <see cref="NotYetImplementedPupilArmOfRecordLookup"/> — no results module exists yet.
/// </summary>
internal sealed class NotYetImplementedResultSetArmLookup : IResultSetArmLookup
{
    /// <inheritdoc />
    public Task<Guid?> GetArmIdAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Result-set arm lookup is not implemented yet — no route uses " +
            "ScopeParameterKind.ResultSet as of TASK-0002. Implement this against the results " +
            "module when it lands.");
}
