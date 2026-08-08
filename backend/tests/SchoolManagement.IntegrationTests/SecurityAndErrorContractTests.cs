using System.Net;
using SchoolManagement.Api.Http;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Verifies the deny-by-default authorisation guarantee, the error contract, and the security headers.
/// </summary>
public sealed class SecurityAndErrorContractTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task AnEndpointWithNoAuthorizationMetadata_Returns401()
    {
        RequireDatabase();

        // THE MOST IMPORTANT TEST IN THIS FILE. /whoami declares no [Authorize] and no .AllowAnonymous():
        // it is protected purely by the fallback policy. A 200 here would mean an endpoint nobody
        // remembered to secure is public, which is the failure mode the fallback policy exists to remove.
        var response = await Client.GetAsync(
            new Uri("/api/v1/reference/whoami", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized,
            "Endpoints must be protected by default. A 200 means the authorisation fallback policy is " +
            "not in effect and every unmarked endpoint in the application is public.");
    }

    [Fact]
    public async Task AnAnonymousEndpoint_IsReachableWithoutCredentials()
    {
        RequireDatabase();

        // The other half: .AllowAnonymous() must actually work, or deny-by-default would make the API
        // unusable rather than secure.
        var response = await Client.GetAsync(
            new Uri("/api/v1/reference/ping?name=Ada", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ErrorResponses_UseProblemJsonWithATraceId()
    {
        RequireDatabase();

        var response = await Client.GetAsync(
            new Uri("/api/v1/reference/ping?name=", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        // RFC 9457 members.
        root.TryGetProperty("type", out var type).ShouldBeTrue();
        root.TryGetProperty("title", out _).ShouldBeTrue();
        root.TryGetProperty("status", out _).ShouldBeTrue();
        root.TryGetProperty("detail", out _).ShouldBeTrue();

        // Our additions. traceId is what makes a support ticket traceable to a log line.
        root.TryGetProperty("traceId", out var traceId).ShouldBeTrue();
        traceId.GetString().ShouldNotBeNullOrWhiteSpace();

        root.TryGetProperty("errorCode", out var errorCode).ShouldBeTrue();
        errorCode.GetString().ShouldNotBeNullOrWhiteSpace();

        // The type must be the stable URN clients branch on.
        type.GetString().ShouldStartWith(ApiProblem.TypeUrnPrefix);
    }

    [Fact]
    public async Task ErrorResponses_DoNotLeakStackTraces()
    {
        RequireDatabase();

        var response = await Client.GetAsync(
            new Uri("/api/v1/reference/ping?name=", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldNotContain("at SchoolManagement.", Case.Insensitive);
        body.ShouldNotContain("stackTrace", Case.Insensitive);
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        RequireDatabase();

        var response = await Client.GetAsync(
            new Uri("/api/v1/reference/ping?name=Ada", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("Referrer-Policy").ShouldContain("no-referrer");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.Contains("Content-Security-Policy").ShouldBeTrue();
        response.Headers.Contains("Cross-Origin-Resource-Policy").ShouldBeTrue();
    }

    [Fact]
    public async Task Responses_EchoTheCorrelationId()
    {
        RequireDatabase();

        const string suppliedId = "test-correlation-0001";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/api/v1/reference/ping?name=Ada", UriKind.Relative));

        request.Headers.Add(CorrelationIdMiddleware.HeaderName, suppliedId);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).ShouldContain(suppliedId);
    }

    [Fact]
    public async Task AMaliciousCorrelationIdIsReplacedRatherThanEchoed()
    {
        RequireDatabase();

        // Header injection / log forging: the inbound value is written into a response header and into
        // logs, so it must never be reflected unvalidated.
        const string malicious = "abc<script>alert(1)</script>";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/api/v1/reference/ping?name=Ada", UriKind.Relative));

        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, malicious);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        echoed.ShouldNotBe(malicious);
        echoed.ShouldNotContain("<");
    }

    [Fact]
    public async Task AnUnknownRoute_Returns401NotFound_BecauseOfTheFallbackPolicy()
    {
        RequireDatabase();

        // This asserts 401, not 404, and that is the CORRECT behaviour rather than a workaround.
        //
        // ASP.NET Core's authorisation middleware applies the fallback policy to requests that match NO
        // endpoint, not just to endpoints lacking authorisation metadata. With deny-by-default, an
        // unknown path is therefore rejected before the router's 404 is ever produced.
        //
        // It is worth knowing, and worth keeping: an unauthenticated caller cannot enumerate which
        // routes exist by probing for 404 versus 401. The cost is that a typo'd URL looks like an auth
        // problem, which is why this is documented in the OpenAPI description and in ASSUMPTIONS.md.
        //
        // (An earlier version of this test asserted 404 — an assumption I had not verified. The
        // behaviour was correct; the expectation was wrong.)
        var response = await Client.GetAsync(
            new Uri("/api/v1/reference/does-not-exist", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized,
            "The fallback policy applies to unmatched routes, so unknown paths are rejected before " +
            "routing can 404. If this ever returns 404, the deny-by-default policy has been weakened.");
    }

    [Fact]
    public async Task AnUnknownRouteUnderAnAnonymousPrefix_StillDoesNotLeakRouteExistence()
    {
        RequireDatabase();

        // The same protection holds regardless of how close the path is to a real anonymous endpoint:
        // matching happens on the whole route, so "nearly /ping" is still no endpoint at all.
        var response = await Client.GetAsync(
            new Uri("/api/v1/reference/pingg?name=Ada", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
