using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Abstractions.Pupils;

/// <summary>Persistence port for <see cref="Pupil"/>.</summary>
/// <remarks>
/// Pupil-scale, unlike <c>IArmRepository</c>'s "load everything, filter in memory" — reads go through
/// cursor-paged, database-side queries, the same shape <c>IAdminAccountRepository.ListAsync</c>
/// established.
/// </remarks>
public interface IPupilRepository
{
    /// <summary>Adds a new pupil. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(Pupil pupil, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a TRACKED pupil by id, for a command that will mutate it. Includes a
    /// <see cref="PupilStatus.Pending"/> record — direct-id access is never subject to the
    /// pending-exclusion invariant, only list/report surfaces are (spec 6.5.14 names lists and
    /// reports, not "the record itself").
    /// </summary>
    Task<Pupil?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a read-only pupil by id, for a query. Same pending-inclusion rule as <see cref="FindTrackedByIdAsync"/>.</summary>
    Task<Pupil?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Cursor-paged, filtered list (spec 6.5.15). Excludes <see cref="PupilStatus.Pending"/> UNLESS
    /// <paramref name="status"/> is explicitly <see cref="PupilStatus.Pending"/> — the structural
    /// default the pending-exclusion invariant requires, applied once here rather than by every
    /// caller.
    /// </summary>
    /// <param name="status"><see langword="null"/> for "every non-pending status."</param>
    /// <param name="search">Matches surname/first/middle name or the registration number, case-insensitively; <see langword="null"/> for none.</param>
    /// <param name="cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="pageSize">Already clamped by the caller.</param>
    /// <param name="asOfDate">"Today", for each row's derived age.</param>
    /// <param name="cancellationToken">Propagated to the underlying query.</param>
    Task<CursorPage<PupilDto>> ListAsync(
        PupilStatus? status,
        string? search,
        string? cursor,
        int pageSize,
        DateOnly asOfDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// The admissions queue (spec 6.5.14, 6.5.17): every <see cref="PupilStatus.Pending"/> record,
    /// unconditionally — the ONE opt-out of the default exclusion that ignores it outright rather
    /// than requiring <c>status=pending</c> on the query string.
    /// </summary>
    Task<CursorPage<PupilDto>> ListAdmissionsQueueAsync(
        string? cursor,
        int pageSize,
        DateOnly asOfDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate candidates for admission step 1 (spec 6.5.11): matches by surname AND first name AND
    /// date of birth. INCLUDES pending records deliberately — the whole point is catching a second,
    /// in-progress admission for the same child, which the ordinary list would otherwise hide.
    /// Contact-phone matching (spec 6.5.11's other half) is the next card's — <c>pupil_contact</c>
    /// does not exist yet.
    /// </summary>
    /// <param name="surname">Exact match, case-insensitive.</param>
    /// <param name="firstName">Exact match, case-insensitive.</param>
    /// <param name="dateOfBirth">Exact match.</param>
    /// <param name="maxResults">A small cap — this is a candidates panel, not a paged list.</param>
    /// <param name="asOfDate">"Today", for each row's derived age.</param>
    /// <param name="cancellationToken">Propagated to the underlying query.</param>
    Task<IReadOnlyList<PupilDto>> FindDuplicatesAsync(
        string surname,
        string firstName,
        DateOnly dateOfBirth,
        int maxResults,
        DateOnly asOfDate,
        CancellationToken cancellationToken);
}
