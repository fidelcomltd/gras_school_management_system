namespace SchoolManagement.Application.Abstractions.Settings;

/// <summary>
/// Private blob storage for a processed school logo or signature rendition (TASK-0005b). The seam
/// that keeps the choice of store (Cloudinary, stage D) out of Application. Implemented in
/// Infrastructure: <c>InMemorySchoolImageStore</c> for tests, <c>CloudinaryImageStore</c> (stage D)
/// everywhere else.
/// </summary>
/// <remarks>
/// <para>
/// Assets are IMMUTABLE and never deleted (orchestrator design, 2026-09-21): every
/// <see cref="PutAsync"/> call writes a brand-new asset and returns its own id — there is no update
/// or delete method on this port, on purpose, so a publication snapshot can keep referencing an old
/// asset id forever (04-module-school-settings.md line 77).
/// </para>
/// <para>
/// This port never speaks HTTP or a public URL. <c>GetSchoolLogo</c>/<c>GetSchoolSignature</c>
/// (stage C) stream <see cref="OpenAsync"/>'s result back through the API's own privilege-checked
/// endpoint — never a redirect to the store — so §9.6's privilege check, <c>Content-Disposition</c>
/// and <c>nosniff</c> all stay under this service's control.
/// </para>
/// </remarks>
public interface ISchoolImageStore
{
    /// <summary>
    /// Stores one rendition's bytes and returns a new, opaque asset id.
    /// </summary>
    /// <param name="bytes">The processed, metadata-free image bytes (an <c>ISchoolImageProcessor</c> output).</param>
    /// <param name="contentType">Either <c>image/png</c> or <c>image/jpeg</c>.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>An opaque id that later resolves the same bytes through <see cref="OpenAsync"/>.</returns>
    Task<string> PutAsync(ReadOnlyMemory<byte> bytes, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a readable stream over a previously stored asset's bytes.
    /// </summary>
    /// <param name="assetId">An id previously returned by <see cref="PutAsync"/>.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A readable stream positioned at the start of the asset's bytes.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="assetId"/> does not resolve to a stored asset. Since assets are never
    /// deleted, this can only happen for an id that was never returned by this store in the first
    /// place — a defect, not an expected failure a caller should branch on.
    /// </exception>
    Task<Stream> OpenAsync(string assetId, CancellationToken cancellationToken);
}
