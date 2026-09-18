using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Abstractions.Subjects;

/// <summary>Persistence port for <see cref="Subject"/>.</summary>
/// <remarks>
/// Admin-configuration-sized (28 seeded, a school adds a handful more at most) — every read loads the
/// full set and filters/sorts/pages in the Application handler, same convention as
/// <c>IClassLevelRepository</c>/<c>IArmRepository</c>.
/// </remarks>
public interface ISubjectRepository
{
    /// <summary>Adds a new subject. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(Subject subject, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED subject by id, for a command that will mutate it.</summary>
    Task<Subject?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a read-only subject by id, for a query. <c>AsNoTracking</c>.</summary>
    Task<Subject?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="subject"/> permanently. No <c>SaveChangesAsync</c>.</summary>
    Task RemoveAsync(Subject subject, CancellationToken cancellationToken);

    /// <summary>Every subject, active and inactive, <c>AsNoTracking</c>.</summary>
    Task<IReadOnlyList<Subject>> ListAllReadOnlyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="normalizedNameKey"/> (already lower-invariant) is already used by
    /// another subject — spec 6.6.2: "Unique, case-insensitive," no status carve-out.
    /// </summary>
    Task<bool> NameExistsAsync(string normalizedNameKey, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>
    /// The other subject already using <paramref name="normalizedCodeKey"/> (already lower-invariant),
    /// or <see langword="null"/> if none — spec 6.6.2: "Unique" where supplied, spec 6.6.8: "The code
    /// MTH is already used by Mathematics" names the conflicting subject, so this returns the entity
    /// rather than a bare bool. Never called for a <see langword="null"/> code.
    /// </summary>
    Task<Subject?> FindByCodeKeyAsync(string normalizedCodeKey, Guid? excludingId, CancellationToken cancellationToken);
}
