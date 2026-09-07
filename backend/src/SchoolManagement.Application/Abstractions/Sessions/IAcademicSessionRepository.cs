using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Sessions;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Abstractions.Sessions;

/// <summary>Persistence port for <see cref="AcademicSession"/>.</summary>
public interface IAcademicSessionRepository
{
    /// <summary>Adds a new session. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(AcademicSession session, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED session by id, for a command that will mutate it.</summary>
    Task<AcademicSession?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a read-only projection-friendly session by id, for a query. <c>AsNoTracking</c>.</summary>
    Task<AcademicSession?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the one TRACKED session currently in <see cref="SessionState.Active"/>, if any (spec
    /// 6.3.3/6.3.9: at most one). Tracked because opening a new First Term closes it as a side effect.
    /// </summary>
    Task<AcademicSession?> FindActiveAsync(CancellationToken cancellationToken);

    /// <summary>Spec 6.3.3: "Unique." <paramref name="excludingId"/> excludes the session being edited.</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>
    /// The first existing session (read-only) whose range overlaps <paramref name="startDate"/>..
    /// <paramref name="endDate"/>, or <see langword="null"/> (spec 6.3.9: "Sessions cannot overlap").
    /// <paramref name="excludingId"/> excludes the session being edited from its own check.
    /// </summary>
    Task<AcademicSession?> FindOverlappingAsync(
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludingId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cursor-paginated list (spec 9.5, 6.3.8), fixed sort by <c>name</c> descending (newest first —
    /// names are the fixed-width <c>YYYY/YYYY</c> format, so lexicographic order is chronological).
    /// </summary>
    /// <param name="state"><see langword="null"/> for every state (spec 6.3.8: "No filters beyond state").</param>
    /// <param name="cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="pageSize">Already clamped to <see cref="CursorPageRequest.MaxPageSize"/> by the caller.</param>
    /// <param name="cancellationToken">Propagated to the underlying query.</param>
    Task<CursorPage<SessionDto>> ListAsync(
        SessionState? state,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken);
}
