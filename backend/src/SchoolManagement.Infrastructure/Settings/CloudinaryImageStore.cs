using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// <see cref="ISchoolImageStore"/> backed by Cloudinary (TASK-0005b stage D) — the real store for
/// every deployed environment.
/// </summary>
/// <remarks>
/// <para>
/// PRIVATE, NOT A CDN (orchestrator design, 2026-09-21). Every upload goes up as
/// <c>type: authenticated</c>, so guessing a URL gets a 401 from Cloudinary and the bytes come back
/// only through this API's own privilege-checked endpoint.
/// </para>
/// <para>
/// IMMUTABLE. A new asset id per upload, <c>Overwrite = false</c>, and no delete method on the port
/// — so a publication snapshot can go on referencing an old logo forever
/// (04-module-school-settings.md line 77).
/// </para>
/// <para>
/// The asset id this returns is <c>{prefix}/{guid}.{ext}</c>: Cloudinary's public id, plus the
/// format suffix its delivery URL needs. Keeping the suffix inside the id is what lets
/// <see cref="OpenAsync"/> work from the id alone, with no second lookup of the
/// <see cref="SchoolImage"/> row's content type.
/// </para>
/// </remarks>
internal sealed class CloudinaryImageStore(ICloudinaryGateway gateway, IOptions<CloudinaryOptions> options)
    : ISchoolImageStore
{
    /// <summary>
    /// A raw resource's public id keeps its extension (Cloudinary appends none to a raw asset), so a PDF's asset id is its
    /// public id and ends with this; <see cref="CloudinaryGateway.OpenAsync"/> reads it to pick the raw delivery URL.
    /// </summary>
    public const string RawAssetSuffix = ".pdf";

    private const string PngContentType = "image/png";
    private const string JpegContentType = "image/jpeg";
    private const string PdfContentType = "application/pdf";

    /// <inheritdoc />
    public async Task<string> PutAsync(
        ReadOnlyMemory<byte> bytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        using var content = new MemoryStream(bytes.ToArray(), writable: false);

        if (contentType == PdfContentType)
        {
            var rawPublicId = $"{options.Value.FolderPrefix}/{Guid.CreateVersion7()}{RawAssetSuffix}";

            // The same private, immutable, opaque-id posture as an image below.
            var rawParameters = new RawUploadParams
            {
                File = new FileDescription(rawPublicId, content),
                PublicId = rawPublicId,
                Type = "authenticated",
                Overwrite = false,
                UseFilename = false,
                UniqueFilename = false,
                Invalidate = false,
            };

            return await gateway.UploadRawAsync(rawParameters, cancellationToken).ConfigureAwait(false);
        }

        var extension = ExtensionFor(contentType);
        var publicId = $"{options.Value.FolderPrefix}/{Guid.CreateVersion7()}";

        var parameters = new ImageUploadParams
        {
            // The name carries no meaning to Cloudinary here (UseFilename is off) but shows up in
            // its logs, where "which asset is this" is otherwise unanswerable.
            File = new FileDescription($"{publicId}.{extension}", content),
            PublicId = publicId,

            // The whole privacy posture in one line: an authenticated asset is not deliverable
            // without a signature computed from the API secret.
            Type = "authenticated",

            // Assets are immutable: a colliding id must fail, never silently replace what a
            // published result is still pointing at.
            Overwrite = false,

            // The id above is already unique and deliberately opaque. Letting Cloudinary derive one
            // from the filename would put a caller-influenced string in the URL.
            UseFilename = false,
            UniqueFilename = false,

            // Nothing is cached publicly, so there is no CDN copy to invalidate — and requesting
            // invalidation on every upload costs a purge Cloudinary rate-limits.
            Invalidate = false,
        };

        var storedPublicId = await gateway.UploadAsync(parameters, cancellationToken).ConfigureAwait(false);

        return $"{storedPublicId}.{extension}";
    }

    /// <inheritdoc />
    public Task<Stream> OpenAsync(string assetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        return gateway.OpenAsync(assetId, cancellationToken);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        PngContentType => "png",
        JpegContentType => "jpg",

        // ISchoolImageProcessor only ever emits these two (spec 9.6), so anything else is a defect
        // upstream rather than a user-supplied value to reject politely.
        _ => throw new ArgumentOutOfRangeException(
            nameof(contentType),
            contentType,
            $"Only '{PngContentType}', '{JpegContentType}' and '{PdfContentType}' are stored."),
    };
}
