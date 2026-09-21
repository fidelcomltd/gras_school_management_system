using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>Persistence port for <see cref="RemarkTemplate"/> (TASK-0086 stage B).</summary>
public interface IRemarkTemplateRepository
{
    /// <summary>Every template of <paramref name="kind"/>, in CREATION order. <c>AsNoTracking</c>.</summary>
    Task<IReadOnlyList<RemarkTemplate>> ListByKindReadOnlyAsync(RemarkKind kind, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a template of <paramref name="kind"/> already exists whose stored
    /// <see cref="RemarkTemplate.TextKey"/> equals <paramref name="normalizedTextKey"/> (already
    /// trimmed and lower-invariant) — delta item 4's "a duplicate within a kind (trimmed,
    /// case-insensitive) is 409."
    /// </summary>
    Task<bool> ExistsWithTextAsync(RemarkKind kind, string normalizedTextKey, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED template by id, for the delete command. <see langword="null"/> when none exists.</summary>
    Task<RemarkTemplate?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Stages a brand-new template for insertion. Does NOT commit.</summary>
    Task AddAsync(RemarkTemplate remarkTemplate, CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="remarkTemplate"/> permanently — a hard delete (spec delta item 4: text is copied, never referenced). Does NOT commit.</summary>
    Task RemoveAsync(RemarkTemplate remarkTemplate, CancellationToken cancellationToken);
}
