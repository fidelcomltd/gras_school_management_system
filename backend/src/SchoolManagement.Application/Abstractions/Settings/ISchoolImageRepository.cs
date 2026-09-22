using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Abstractions.Settings;

/// <summary>
/// Persistence port for <see cref="SchoolImage"/> rows (TASK-0005b stage B2). Implemented in
/// Infrastructure. Rows are never updated or deleted (see the entity's own remarks) — this port has
/// no update or delete method for that reason.
/// </summary>
public interface ISchoolImageRepository
{
    /// <summary>
    /// Adds every rendition produced by one upload. No <c>SaveChangesAsync</c> of its own — the
    /// unit-of-work behaviour commits.
    /// </summary>
    Task AddRangeAsync(IReadOnlyList<SchoolImage> images, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the Original rendition of upload group <paramref name="uploadGroupId"/> and maps it,
    /// with its uploader's staff name, to the wire DTO.
    /// </summary>
    /// <param name="uploadGroupId">
    /// <see cref="SchoolProfile.CurrentLogoGroupId"/> or <see cref="SchoolProfile.CurrentSignatureGroupId"/>.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <see langword="null"/> when <paramref name="uploadGroupId"/> is itself <see langword="null"/>
    /// (nothing uploaded yet) or — a data defect, since assets are never deleted — resolves to no row.
    /// </returns>
    Task<SchoolImageDto?> FindCurrentDtoAsync(Guid? uploadGroupId, CancellationToken cancellationToken);
}
