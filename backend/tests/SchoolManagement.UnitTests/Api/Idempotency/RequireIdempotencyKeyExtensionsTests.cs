using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SchoolManagement.Api.Idempotency;

namespace SchoolManagement.UnitTests.Api.Idempotency;

/// <summary>
/// <see cref="RequireIdempotencyKeyExtensions.BuildFingerprintAsync"/> — TASK-0005b's approved fix
/// for the multipart fingerprint gap: before this method folded an uploaded file's bytes into the
/// fingerprint, every upload's fingerprint collapsed to the same method/path/caller triple regardless
/// of content, because the endpoint binds a bare <see cref="IFormFile"/> rather than an
/// <c>IBaseCommand</c> the generic JSON-serialise path could see.
/// </summary>
public sealed class RequireIdempotencyKeyExtensionsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task BuildFingerprintAsync_WithDifferentFileBytes_SameEverythingElse_ProducesDifferentFingerprints()
    {
        var context = CreateHttpContext();
        var fileA = new FakeFormFile([1, 2, 3, 4]);
        var fileB = new FakeFormFile([9, 9, 9, 9]);

        var fingerprintA = await RequireIdempotencyKeyExtensions.BuildFingerprintAsync(
            context, "admin-1", command: null, fileA, JsonOptions, TestContext.Current.CancellationToken);
        var fingerprintB = await RequireIdempotencyKeyExtensions.BuildFingerprintAsync(
            context, "admin-1", command: null, fileB, JsonOptions, TestContext.Current.CancellationToken);

        fingerprintA.ShouldNotBe(fingerprintB);
    }

    [Fact]
    public async Task BuildFingerprintAsync_WithIdenticalFileBytes_ProducesIdenticalFingerprints()
    {
        var context = CreateHttpContext();
        var fileA = new FakeFormFile([1, 2, 3, 4]);
        var fileB = new FakeFormFile([1, 2, 3, 4]); // Same bytes, a distinct instance.

        var fingerprintA = await RequireIdempotencyKeyExtensions.BuildFingerprintAsync(
            context, "admin-1", command: null, fileA, JsonOptions, TestContext.Current.CancellationToken);
        var fingerprintB = await RequireIdempotencyKeyExtensions.BuildFingerprintAsync(
            context, "admin-1", command: null, fileB, JsonOptions, TestContext.Current.CancellationToken);

        fingerprintA.ShouldBe(fingerprintB);
    }

    [Fact]
    public async Task BuildFingerprintAsync_ReadingTheFile_DoesNotConsumeItForTheEndpointsOwnLaterRead()
    {
        // BuildFingerprintAsync's own remarks: OpenReadStream() must reopen from the start on every
        // call, so hashing the bytes here must never affect the endpoint's own subsequent read of the
        // same IFormFile when it builds the actual command.
        var context = CreateHttpContext();
        var file = new FakeFormFile([5, 6, 7, 8]);

        await RequireIdempotencyKeyExtensions.BuildFingerprintAsync(
            context, "admin-1", command: null, file, JsonOptions, TestContext.Current.CancellationToken);

        using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        buffer.ToArray().ShouldBe(new byte[] { 5, 6, 7, 8 });
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/settings/identity/logo";
        return context;
    }

    /// <summary>Minimal <see cref="IFormFile"/> test double — only <see cref="OpenReadStream"/> is exercised.</summary>
    private sealed class FakeFormFile(byte[] bytes) : IFormFile
    {
        public string ContentType => "image/png";

        public string ContentDisposition => string.Empty;

        public IHeaderDictionary Headers { get; } = new HeaderDictionary();

        public long Length => bytes.Length;

        public string Name => "file";

        public string FileName => "upload.png";

        public void CopyTo(Stream target) => OpenReadStream().CopyTo(target);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            OpenReadStream().CopyToAsync(target, cancellationToken);

        // A fresh stream every call, matching a real buffered IFormFile's "reopens from the start" behaviour.
        public Stream OpenReadStream() => new MemoryStream(bytes, writable: false);
    }
}
