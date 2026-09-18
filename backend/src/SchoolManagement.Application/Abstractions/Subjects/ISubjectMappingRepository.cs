using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Abstractions.Subjects;

/// <summary>Persistence port for <see cref="SubjectMapping"/>.</summary>
/// <remarks>Admin-configuration-sized, same convention as <see cref="ISubjectRepository"/>.</remarks>
public interface ISubjectMappingRepository
{
    /// <summary>Adds a new mapping. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(SubjectMapping mapping, CancellationToken cancellationToken);

    /// <summary>Every TRACKED mapping (any status) for <paramref name="termId"/> — for a whole-grid save.</summary>
    Task<IReadOnlyList<SubjectMapping>> ListByTermTrackedAsync(Guid termId, CancellationToken cancellationToken);

    /// <summary>Every read-only ACTIVE mapping for <paramref name="termId"/> — for the grid read and the resolver's callers.</summary>
    Task<IReadOnlyList<SubjectMapping>> ListActiveByTermReadOnlyAsync(Guid termId, CancellationToken cancellationToken);

    /// <summary>
    /// Every read-only ACTIVE mapping for <paramref name="classLevelId"/> in <paramref name="termId"/>
    /// — the level half of the resolver (spec 8.1: "level mappings for the term").
    /// </summary>
    Task<IReadOnlyList<SubjectMapping>> ListActiveByLevelAndTermReadOnlyAsync(
        Guid classLevelId, Guid termId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether an ACTIVE mapping already exists on <c>(subjectId, classLevelId, termId)</c> — spec
    /// 6.6.3's partial-unique invariant, checked in the handler before insert as the friendly half of
    /// the pair (the database index is the backstop).
    /// </summary>
    Task<bool> IsActivelyMappedAsync(Guid subjectId, Guid classLevelId, Guid termId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="subjectId"/> has EVER been mapped, any level, any term, any status —
    /// spec 6.6.8's subject-delete rule: "permitted only where it has never been mapped."
    /// </summary>
    Task<bool> AnyEverForSubjectAsync(Guid subjectId, CancellationToken cancellationToken);

    /// <summary>Every read-only ACTIVE mapping for <paramref name="subjectId"/> across every term — for the list view's per-term counts and the mark-check's level lookup.</summary>
    Task<IReadOnlyList<SubjectMapping>> ListActiveBySubjectAndTermReadOnlyAsync(
        Guid subjectId, Guid termId, CancellationToken cancellationToken);

    /// <summary>
    /// Distinct subject ids EVER actively mapped to <paramref name="classLevelId"/>, any term — the
    /// list view's <c>levelId</c> filter when no <c>termId</c> is also given.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListSubjectIdsEverMappedToLevelAsync(Guid classLevelId, CancellationToken cancellationToken);
}
