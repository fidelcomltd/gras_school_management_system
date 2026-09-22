using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Settings;
using SchoolManagement.IntegrationTests.Infrastructure;
using SkiaSharp;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0005b stages B2 and C end to end: multipart upload through the real pipeline (CSRF, required
/// Idempotency-Key, the per-route body-size limit, the processor, the in-memory store) and the
/// privilege-checked serving endpoints with their headers.
/// </summary>
public sealed class SchoolImageEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SettingsUrl = "/api/v1/settings";
    private const string LogoUrl = "/api/v1/settings/identity/logo";
    private const string SignatureUrl = "/api/v1/settings/identity/signature";

    [Fact]
    public async Task UploadLogo_ThenSettingsAndEverySizeServeIt()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();

        var upload = await UploadAsync(LogoUrl, jar, Png(400, 320));

        upload.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await upload.Content.ReadFromJsonAsync<SchoolImageDto>(TestContext.Current.CancellationToken);
        dto!.Width.ShouldBe(400);
        dto.Height.ShouldBe(320);

        var settings = await ReadRootAsync(await GetAsync(SettingsUrl, jar));
        settings.GetProperty("identity").GetProperty("logo").GetProperty("width").GetInt32().ShouldBe(400);

        var original = await GetAsync($"{LogoUrl}/original", jar);
        original.StatusCode.ShouldBe(HttpStatusCode.OK);
        original.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        original.Content.Headers.ContentDisposition!.ToString().ShouldBe("inline; filename=logo.png");
        original.Headers.CacheControl!.Private.ShouldBeTrue();
        original.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");

        var small = await GetAsync($"{LogoUrl}/64", jar);
        small.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var decoded = SKBitmap.Decode(await small.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        decoded.Width.ShouldBe(64);
    }

    [Fact]
    public async Task GetLogo_BeforeAnyUpload_Returns404NotUploaded()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetAsync($"{LogoUrl}/200", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadRootAsync(response)).GetProperty("errorCode").GetString().ShouldBe("school_image.not_uploaded");
    }

    [Fact]
    public async Task UploadSignature_ThenServeIt_AtItsOriginalSize()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();

        (await UploadAsync(SignatureUrl, jar, Png(600, 200))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var served = await GetAsync(SignatureUrl, jar);
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var decoded = SKBitmap.Decode(await served.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        decoded.Width.ShouldBe(600);
        decoded.Height.ShouldBe(200);
    }

    [Fact]
    public async Task UploadLogo_WithSvgBytes_Returns422UnsupportedType()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();

        var response = await UploadAsync(LogoUrl, jar, svg);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadRootAsync(response)).GetProperty("errorCode").GetString().ShouldBe("school_image.unsupported_type");
    }

    [Fact]
    public async Task UploadLogo_BelowTheMinimumSize_Returns422TooSmall()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();

        var response = await UploadAsync(LogoUrl, jar, Png(299, 400));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadRootAsync(response)).GetProperty("errorCode").GetString().ShouldBe("school_image.too_small");
    }

    [Fact]
    public async Task UploadSignature_OverTheCap_Returns422TooLarge()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();

        // The route's RequestSizeLimit (cap + 64 KB) is applied by routing to Kestrel's
        // IHttpMaxRequestBodySizeFeature, which TestServer does not implement (verified in
        // Microsoft.AspNetCore.TestHost 10.0.10), so a 413 cannot be observed in-process. What IS
        // observable is the processor's own cap, the backstop that holds on every host.
        var response = await UploadAsync(SignatureUrl, jar, new byte[1024 * 1024 + 1]);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadRootAsync(response)).GetProperty("errorCode").GetString().ShouldBe("school_image.too_large");
    }

    [Fact]
    public async Task UploadLogo_SameKey_ReplaysForTheSameBytes_AndConflictsForDifferentBytes()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();
        var key = Guid.NewGuid().ToString("D");
        var image = Png(400, 400);

        (await UploadAsync(LogoUrl, jar, image, key)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var replay = await UploadAsync(LogoUrl, jar, image, key);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        replay.Headers.GetValues("Idempotency-Replay").ShouldContain("true");

        var conflict = await UploadAsync(LogoUrl, jar, Png(500, 500), key);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadRootAsync(conflict)).GetProperty("errorCode").GetString().ShouldBe("idempotency.key_conflict");
    }

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private async Task<HttpResponseMessage> UploadAsync(string url, CookieJar jar, byte[] bytes, string? idempotencyKey = null)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var form = new MultipartFormDataContent { { file, "file", "upload.bin" } };
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString("D"));
        jar.ApplyWithCsrf(request);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> GetAsync(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private static async Task<JsonElement> ReadRootAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);

        using var request = new HttpRequestMessage(HttpMethod.Post, SignInUrl)
        {
            Content = JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(request);
        var signIn = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(signIn);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }
}
