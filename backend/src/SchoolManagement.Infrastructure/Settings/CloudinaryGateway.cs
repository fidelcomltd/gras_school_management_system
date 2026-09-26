using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// The one place the Cloudinary SDK is called. Exists so <see cref="CloudinaryImageStore"/>'s
/// decisions — the asset id it generates, the upload parameters it sets — are unit-testable without
/// a network, a Cloudinary account, or a mocking framework.
/// </summary>
/// <remarks>
/// The seam takes the SDK's own <see cref="ImageUploadParams"/> rather than a type of ours, so what
/// a test asserts is the real parameter object that would go to Cloudinary. A hand-rolled request
/// record here would leave the mapping onto the SDK — the part that actually decides whether an
/// asset is private — untested.
/// </remarks>
internal interface ICloudinaryGateway
{
    /// <summary>Uploads one asset and returns the public id Cloudinary stored it under.</summary>
    /// <param name="parameters">Fully-populated upload parameters.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<string> UploadAsync(ImageUploadParams parameters, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads one asset as a <c>raw</c> resource (a PDF document scan) and returns its public id. Cloudinary blocks PDF
    /// delivery for image resources on accounts without that setting enabled; a raw resource is served byte for byte.
    /// </summary>
    /// <param name="parameters">Fully-populated upload parameters; the public id carries the file extension.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<string> UploadRawAsync(RawUploadParams parameters, CancellationToken cancellationToken);

    /// <summary>Opens a readable stream over a stored asset's bytes. An id ending <c>.pdf</c> is a raw resource.</summary>
    /// <param name="assetId">A public id with its format suffix, as <c>UploadAsync</c>'s caller built it.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<Stream> OpenAsync(string assetId, CancellationToken cancellationToken);
}

/// <summary>The real <see cref="ICloudinaryGateway"/>, over CloudinaryDotNet.</summary>
/// <remarks>
/// <para>
/// DELIVERY IS A SIGNED URL, NOT A PUBLIC ONE. Assets are uploaded with <c>type: authenticated</c>
/// (see <see cref="CloudinaryImageStore"/>), so Cloudinary serves them only to a URL carrying a
/// signature derived from the API secret. This class mints that URL and fetches the bytes
/// server-side; the URL never reaches a browser, because the API streams the bytes back through its
/// own privilege-checked endpoint (spec 9.6).
/// </para>
/// <para>
/// Singleton. <see cref="Cloudinary"/> is thread-safe and holds the credentials; the
/// <see cref="HttpClient"/> comes from <see cref="IHttpClientFactory"/> per call so handler
/// recycling still applies.
/// </para>
/// </remarks>
internal sealed class CloudinaryGateway : ICloudinaryGateway
{
    /// <summary>The named <see cref="HttpClient"/> asset downloads use.</summary>
    public const string HttpClientName = "cloudinary-assets";

    private readonly Cloudinary _cloudinary;
    private readonly IHttpClientFactory _httpClientFactory;

    public CloudinaryGateway(IOptions<CloudinaryOptions> options, IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var configured = options.Value;
        _cloudinary = new Cloudinary(new Account(configured.CloudName, configured.ApiKey, configured.ApiSecret));
        _cloudinary.Api.Secure = true;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(ImageUploadParams parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var result = await _cloudinary.UploadAsync(parameters, cancellationToken).ConfigureAwait(false);
        return PublicIdOrThrow(result, parameters.PublicId);
    }

    /// <inheritdoc />
    public async Task<string> UploadRawAsync(RawUploadParams parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var result = await _cloudinary.UploadAsync(parameters, "raw", cancellationToken).ConfigureAwait(false);
        return PublicIdOrThrow(result, parameters.PublicId);
    }

    /// <inheritdoc />
    public async Task<Stream> OpenAsync(string assetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var builder = assetId.EndsWith(CloudinaryImageStore.RawAssetSuffix, StringComparison.Ordinal)
            ? _cloudinary.Api.Url.ResourceType("raw")
            : _cloudinary.Api.UrlImgUp;
        var url = builder
            .Secure(true)
            .Type("authenticated")
            .Signed(true)
            .BuildUrl(assetId);

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client
            .GetAsync(new Uri(url, UriKind.Absolute), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Matches ISchoolImageStore.OpenAsync's contract: assets are never deleted, so a
            // non-success here means the id was never ours — a defect, not a branch to handle.
            throw new InvalidOperationException(
                $"Cloudinary returned {(int)response.StatusCode} for asset '{assetId}'.");
        }

        // Buffered rather than handed back as the live network stream: the caller may outlive this
        // method's `using` scope on the response, and a school logo is at most a few hundred KB.
        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;

        return buffer;
    }

    // The SDK reports an upload failure in the result, not as an exception. Left unchecked, a rejected upload would
    // return a public id of null and be written into a row.
    private static string PublicIdOrThrow(UploadResult result, string publicId) =>
        result.Error is { } error
            ? throw new InvalidOperationException($"Cloudinary rejected the upload of '{publicId}': {error.Message}")
            : result.PublicId;
}
