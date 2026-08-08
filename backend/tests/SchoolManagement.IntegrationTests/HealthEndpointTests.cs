using System.Net;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Verifies the liveness/readiness split, which an orchestrator depends on behaving correctly.
/// </summary>
public sealed class HealthEndpointTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Live_IsHealthy()
    {
        RequireDatabase();

        var response = await Client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Live_RunsNoDependencyChecks()
    {
        RequireDatabase();

        // Liveness must report on the PROCESS only. If it included the database, a brief database outage
        // would fail liveness across the fleet and the orchestrator would restart every instance —
        // converting a recoverable dependency blip into a full cold start.
        var response = await Client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        using var document = await ReadJsonAsync(response);
        var checks = document.RootElement.GetProperty("checks");

        checks.GetArrayLength().ShouldBe(0, "The liveness probe must not evaluate any dependency checks.");
    }

    [Fact]
    public async Task Ready_IncludesTheDatabaseCheck()
    {
        RequireDatabase();

        var response = await Client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = await ReadJsonAsync(response);
        var names = document.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .ToArray();

        names.ShouldContain("database");
    }

    [Fact]
    public async Task Ready_DoesNotLeakDiagnosticDetail()
    {
        RequireDatabase();

        // Probes are anonymous by necessity. The default health-check writer emits exception messages and
        // durations, which would hand an unauthenticated caller server names and library versions.
        var response = await Client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldNotContain("exception", Case.Insensitive);
        body.ShouldNotContain("Host=", Case.Insensitive);
        body.ShouldNotContain("Password", Case.Insensitive);
        body.ShouldNotContain("duration", Case.Insensitive);
    }

    [Fact]
    public async Task HealthEndpoints_AreAnonymous()
    {
        RequireDatabase();

        // Under the placeholder authentication scheme every caller is anonymous, so a 200 here proves the
        // probes are genuinely exempt from the deny-by-default policy. A probe cannot present credentials.
        var live = await Client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);
        var ready = await Client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
