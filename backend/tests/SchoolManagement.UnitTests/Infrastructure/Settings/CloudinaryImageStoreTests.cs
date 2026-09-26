using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SchoolManagement.Infrastructure.Settings;

namespace SchoolManagement.UnitTests.Infrastructure.Settings;

/// <summary>
/// Tests <see cref="CloudinaryImageStore"/> (TASK-0005b stage D) through a fake gateway — no
/// network, no account, per the card's "tests never touch the network".
/// </summary>
/// <remarks>
/// What is asserted is the upload PARAMETERS, because they are where this adapter's guarantees
/// live: <c>authenticated</c> is what makes the asset private, and <c>Overwrite = false</c> plus a
/// freshly generated public id are what make assets immutable so a publication snapshot can keep
/// pointing at an old logo.
/// </remarks>
public sealed class CloudinaryImageStoreTests
{
    private readonly RecordingCloudinaryGateway _gateway = new();

    private CloudinaryImageStore CreateStore(string folderPrefix = "gras/prod") =>
        new(_gateway, Options.Create(new CloudinaryOptions
        {
            CloudName = "cloud",
            ApiKey = "key",
            ApiSecret = "secret",
            FolderPrefix = folderPrefix,
        }));

    [Fact]
    public async Task PutAsync_UploadsPrivatelyAndImmutably()
    {
        var assetId = await CreateStore().PutAsync(new byte[] { 1, 2, 3 }, "image/png", CancellationToken.None);

        var parameters = _gateway.LastParameters.ShouldNotBeNull();
        parameters.Type.ShouldBe("authenticated");
        parameters.Overwrite.ShouldBe(false);
        parameters.UseFilename.ShouldBe(false);
        parameters.UniqueFilename.ShouldBe(false);
        parameters.PublicId.ShouldStartWith("gras/prod/");

        // The returned id is the public id plus the format suffix the signed delivery URL needs.
        assetId.ShouldBe(parameters.PublicId + ".png");
    }

    [Fact]
    public async Task PutAsync_CalledTwice_GeneratesTwoPublicIds()
    {
        var store = CreateStore();

        var first = await store.PutAsync(new byte[] { 1 }, "image/png", CancellationToken.None);
        var second = await store.PutAsync(new byte[] { 1 }, "image/png", CancellationToken.None);

        second.ShouldNotBe(first);
    }

    [Fact]
    public async Task PutAsync_UsesTheJpegSuffixForJpeg()
    {
        var assetId = await CreateStore().PutAsync(new byte[] { 1 }, "image/jpeg", CancellationToken.None);

        assetId.ShouldEndWith(".jpg");
    }

    [Fact]
    public async Task PutAsync_RejectsAContentTypeTheProcessorNeverEmits()
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => CreateStore().PutAsync(new byte[] { 1 }, "image/gif", CancellationToken.None));
    }

    [Fact]
    public async Task PutAsync_APdf_GoesUpAsAPrivateImmutableRawResource_WhoseIdKeepsItsExtension()
    {
        var assetId = await CreateStore().PutAsync("%PDF-1.4"u8.ToArray(), "application/pdf", CancellationToken.None);

        _gateway.LastParameters.ShouldBeNull();
        var parameters = _gateway.LastRawParameters.ShouldNotBeNull();
        parameters.Type.ShouldBe("authenticated");
        parameters.Overwrite.ShouldBe(false);
        parameters.UseFilename.ShouldBe(false);
        parameters.PublicId.ShouldStartWith("gras/prod/");
        parameters.PublicId.ShouldEndWith(CloudinaryImageStore.RawAssetSuffix);
        assetId.ShouldBe(parameters.PublicId);
    }

    [Fact]
    public async Task OpenAsync_AsksTheGatewayForTheIdItWasGiven()
    {
        using var stream = await CreateStore().OpenAsync("gras/prod/abc.png", CancellationToken.None);

        _gateway.LastOpenedAssetId.ShouldBe("gras/prod/abc.png");
    }

    /// <summary>Captures what the store asked Cloudinary for, and answers plausibly.</summary>
    private sealed class RecordingCloudinaryGateway : ICloudinaryGateway
    {
        public ImageUploadParams? LastParameters { get; private set; }

        public string? LastOpenedAssetId { get; private set; }

        public RawUploadParams? LastRawParameters { get; private set; }

        public Task<string> UploadRawAsync(RawUploadParams parameters, CancellationToken cancellationToken)
        {
            LastRawParameters = parameters;
            return Task.FromResult(parameters.PublicId);
        }

        public Task<string> UploadAsync(ImageUploadParams parameters, CancellationToken cancellationToken)
        {
            LastParameters = parameters;

            // Cloudinary echoes the public id back on success, which is what the real gateway returns.
            return Task.FromResult(parameters.PublicId);
        }

        public Task<Stream> OpenAsync(string assetId, CancellationToken cancellationToken)
        {
            LastOpenedAssetId = assetId;

            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        }
    }
}
