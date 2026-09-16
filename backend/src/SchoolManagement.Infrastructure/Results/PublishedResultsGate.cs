using SchoolManagement.Application.Abstractions.Results;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>
/// Default <see cref="IPublishedResultsGate"/>: honestly zero. See the interface's remarks — no
/// <c>result_set</c>/publish module exists anywhere in this codebase as of TASK-0069, so zero
/// published result sets is today's only correct answer, not a stand-in. Replace this with a real
/// query against <c>result_set</c> when the results/publish module lands.
/// </summary>
internal sealed class PublishedResultsGate : IPublishedResultsGate
{
    /// <inheritdoc />
    public Task<int> CountPublishedInSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}
