using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Persistence port for the assessment structure (spec 6.2.6). Implemented in Infrastructure.</summary>
/// <remarks>
/// Unlike <see cref="IGradingBandRepository"/>, this port is ID-AWARE: an <see cref="AssessmentComponent"/>'s
/// <c>Id</c> is where marks are stored against (6.2.6), so <c>PUT /settings/assessment</c> must edit matched rows
/// in place and add/remove only the rows that genuinely changed — never a blind delete-and-recreate,
/// which would silently orphan every future <c>subject_score</c> row.
/// </remarks>
public interface IAssessmentComponentRepository
{
    /// <summary>Loads every component, read-only, ordered by <see cref="AssessmentComponent.DisplayOrder"/>.</summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<IReadOnlyList<AssessmentComponent>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads every component TRACKED, unordered, for a command that will edit some in place and
    /// add/remove others.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<IReadOnlyList<AssessmentComponent>> ListTrackedAsync(CancellationToken cancellationToken);

    /// <summary>Stages a brand-new component for insertion. Does NOT commit.</summary>
    /// <param name="component">The component to add.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task AddAsync(AssessmentComponent component, CancellationToken cancellationToken);

    /// <summary>Stages a component for deletion. Does NOT commit.</summary>
    /// <param name="component">The component to remove.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task RemoveAsync(AssessmentComponent component, CancellationToken cancellationToken);
}
