using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Persistence port for the append-only <see cref="ConfigVersion"/> ledger. Implemented in Infrastructure.</summary>
/// <remarks>
/// WRITES take the domain entity (<see cref="AddAsync"/>); READS return DTOs projected in the query,
/// the same asymmetry <c>ISampleRecordRepository</c> documents — nothing here ever loads a
/// <see cref="ConfigVersion"/> back out to map it in memory.
/// </remarks>
public interface IConfigVersionRepository
{
    /// <summary>
    /// Stages a new version row for insertion. Does NOT commit; the unit-of-work behaviour does that
    /// when the command returns a successful result. Never call this for a save that lost its
    /// optimistic-concurrency check — the loser gets no row (spec 6.2.11).
    /// </summary>
    /// <param name="configVersion">The version to add.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task AddAsync(ConfigVersion configVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one cursor page of version summaries, newest first (spec 9.5 — never offset).
    /// </summary>
    /// <param name="beforeVersionNumber">
    /// When supplied (a decoded cursor), only rows with a strictly smaller version number are
    /// returned. <see langword="null"/> for the first page.
    /// </param>
    /// <param name="pageSize">Already validated and clamped by the caller.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<CursorPage<ConfigVersionSummaryDto>> ListAsync(
        long? beforeVersionNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns one version's full detail, including its snapshot, or <see langword="null"/> when no
    /// row with that id exists.
    /// </summary>
    /// <param name="id">The version's id.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<ConfigVersionDetailDto?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);
}
