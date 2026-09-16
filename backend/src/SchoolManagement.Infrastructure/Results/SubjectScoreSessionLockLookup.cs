using SchoolManagement.Application.Abstractions.Results;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>
/// Default <see cref="ISubjectScoreSessionLockLookup"/>: honestly <see langword="false"/>. See the
/// interface's remarks — no <c>subject_score</c> table exists anywhere in this codebase as of
/// TASK-0069, so "no marks exist in any session" is today's only correct answer, not a stand-in.
/// Replace this with a real query against <c>subject_score</c> when the scoring module lands.
/// </summary>
internal sealed class SubjectScoreSessionLockLookup : ISubjectScoreSessionLockLookup
{
    /// <inheritdoc />
    public Task<bool> AnyScoreExistsInSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
