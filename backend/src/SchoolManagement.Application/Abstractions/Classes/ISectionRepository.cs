using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Application.Abstractions.Classes;

/// <summary>Persistence port for <see cref="Section"/>.</summary>
public interface ISectionRepository
{
    /// <summary>Adds a new section. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(Section section, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED section by id, for a command that will mutate it.</summary>
    Task<Section?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="normalizedNameKey"/> (already lower-invariant) is already used by
    /// another section. <paramref name="excludingId"/> excludes the section being edited.
    /// </summary>
    Task<bool> NameExistsAsync(string normalizedNameKey, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>Every section, <c>AsNoTracking</c>. Never paged — spec 6.4.9: "a two-row seeded list a school extends rarely."</summary>
    Task<IReadOnlyList<Section>> ListAllReadOnlyAsync(CancellationToken cancellationToken);
}
