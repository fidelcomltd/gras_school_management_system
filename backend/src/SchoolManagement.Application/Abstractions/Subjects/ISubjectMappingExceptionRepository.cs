using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Abstractions.Subjects;

/// <summary>Persistence port for <see cref="SubjectMappingException"/>.</summary>
public interface ISubjectMappingExceptionRepository
{
    /// <summary>Adds a new exception. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(SubjectMappingException exception, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED exception by id, for the delete command.</summary>
    Task<SubjectMappingException?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="exception"/> permanently. No <c>SaveChangesAsync</c>.</summary>
    Task RemoveAsync(SubjectMappingException exception, CancellationToken cancellationToken);

    /// <summary>Every read-only exception for <paramref name="armId"/> in <paramref name="termId"/> — the resolver's other half.</summary>
    Task<IReadOnlyList<SubjectMappingException>> ListByArmAndTermReadOnlyAsync(
        Guid armId, Guid termId, CancellationToken cancellationToken);

    /// <summary>Every read-only exception for <paramref name="termId"/>, any arm — the grid's "arm exception summary" (spec 6.6.7).</summary>
    Task<IReadOnlyList<SubjectMappingException>> ListByTermReadOnlyAsync(Guid termId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a row already exists on <c>(armId, subjectId, termId)</c>, regardless of mode — spec
    /// 6.6.4's single-row invariant, checked in the handler before insert as the friendly half (the
    /// database index is the backstop).
    /// </summary>
    Task<bool> ExistsAsync(Guid armId, Guid subjectId, Guid termId, CancellationToken cancellationToken);
}
