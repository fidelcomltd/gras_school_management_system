using System.Net;
using Microsoft.Extensions.Options;
using SchoolManagement.Infrastructure.Settings;

namespace SchoolManagement.UnitTests.Infrastructure.Settings;

/// <summary>
/// Which URL <see cref="CloudinaryGateway.OpenAsync"/> fetches, through a recording handler: no network, no account.
/// </summary>
/// <remarks>
/// Staging answered a signed DELIVERY url for a raw PDF with 401 (Cloudinary's account-level PDF delivery restriction), so
/// a PDF must go through the signed download API. What is asserted is the request the gateway actually sends.
/// </remarks>
public sealed class CloudinaryGatewayTests : IDisposable
{
    private readonly RecordingHandler _handler = new();

    /// <inheritdoc />
    public void Dispose() => _handler.Dispose();

    private CloudinaryGateway CreateGateway() =>
        new(Options.Create(new CloudinaryOptions { CloudName = "cloud", ApiKey = "key", ApiSecret = "secret", FolderPrefix = "gras/test" }),
            new SingleClientFactory(_handler));

    [Fact]
    public async Task OpenAsync_APdf_UsesTheSignedDownloadApi_ForAnAuthenticatedRawResource()
    {
        using var stream = await CreateGateway().OpenAsync("gras/test/abc.pdf", CancellationToken.None);

        var uri = _handler.LastRequestUri.ShouldNotBeNull();
        uri.Host.ShouldBe("api.cloudinary.com");
        uri.AbsolutePath.ShouldBe("/v1_1/cloud/raw/download");
        var query = Uri.UnescapeDataString(uri.Query);
        query.ShouldContain("public_id=gras/test/abc.pdf");
        query.ShouldContain("type=authenticated");
        query.ShouldContain("signature=");
        query.ShouldContain("expires_at=");
    }

    [Fact]
    public async Task OpenAsync_AnImage_StillUsesASignedAuthenticatedDeliveryUrl()
    {
        using var stream = await CreateGateway().OpenAsync("gras/test/abc.jpg", CancellationToken.None);

        var uri = _handler.LastRequestUri.ShouldNotBeNull();
        uri.Host.ShouldBe("res.cloudinary.com");
        uri.AbsolutePath.ShouldStartWith("/cloud/image/authenticated/s--");
        uri.AbsolutePath.ShouldEndWith("/gras/test/abc.jpg");
    }

    [Fact]
    public async Task OpenAsync_ARefusal_NamesCloudinarysOwnReason()
    {
        _handler.Status = HttpStatusCode.Unauthorized;
        _handler.Reason = "deny or ACL failure";

        var error = await Should.ThrowAsync<InvalidOperationException>(() => CreateGateway().OpenAsync("gras/test/abc.pdf", CancellationToken.None));

        error.Message.ShouldBe("Cloudinary returned 401 for asset 'gras/test/abc.pdf' (deny or ACL failure).");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public string? Reason { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            var response = new HttpResponseMessage(Status) { Content = new ByteArrayContent([1, 2, 3]) };
            if (Reason is not null)
            {
                response.Headers.Add("x-cld-error", Reason);
            }

            return Task.FromResult(response);
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
