using SchoolManagement.Application.Abstractions.Authorization;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// Default <see cref="IResultSetArmLookup"/>: throws. No results module exists yet, so no route
/// declares <see cref="ScopeParameterKind.ResultSet"/> — this registration exists only so
/// <c>IScopeResolver</c> can be constructed; it is never reached at runtime by any route today.
/// Implement against the results module when it lands.
/// </summary>
/// <remarks>
/// <see cref="IPupilArmOfRecordLookup"/>'s own stand-in of this shape, <c>NotYetImplementedPupilArmOfRecordLookup</c>,
/// was DELETED by TASK-0059 — the pupil/enrolment module it was waiting on now exists, and
/// <c>PupilArmOfRecordLookup</c> resolves the real arm from a pupil's open enrolment. That lookup
/// still has no route declaring <see cref="ScopeParameterKind.Pupil"/> against it (pupil routes use
/// the handler-level <c>PupilAccessGuard</c> pattern instead — see its own remarks), so it remains
/// unreached in production today; it is simply no longer a throwing stub.
/// </remarks>
internal sealed class NotYetImplementedResultSetArmLookup : IResultSetArmLookup
{
    /// <inheritdoc />
    public Task<Guid?> GetArmIdAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Result-set arm lookup is not implemented yet — no route uses " +
            "ScopeParameterKind.ResultSet as of TASK-0002. Implement this against the results " +
            "module when it lands.");
}
