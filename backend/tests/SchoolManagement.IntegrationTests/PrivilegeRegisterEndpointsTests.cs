using System.Net;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Security.PrivilegeRegister;
using SchoolManagement.Domain.Security;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0028 dispatch 1's <c>GET /api/v1/privileges</c> against the approved
/// contract delta (<c>.agent/decisions/2026-Q3-contract-deltas.md</c>, entry <c>TASK-0028</c>,
/// section 1): authenticated-only with no privilege required (spec 6.1.14), the full grouped
/// shape, and the exclusion of legacy <c>guardian.*</c> aliases.
/// </summary>
public sealed class PrivilegeRegisterEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string PrivilegesUrl = "/api/v1/privileges";

    [Fact]
    public async Task Get_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        var response = await Client.GetAsync(new Uri(PrivilegesUrl, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AsAnyAuthenticatedCaller_Returns200WithTheFullGroupedRegister()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await GetAsync(PrivilegesUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PrivilegeRegisterResponse>(response);

        body.Groups.Select(group => group.Key).ShouldBe(
        [
            "administration",
            "settings",
            "academic_structure",
            "pupils_and_subjects",
            "results",
            "pins_and_reports",
        ]);

        body.Groups.Sum(group => group.Privileges.Count).ShouldBe(PrivilegeRegistry.All.Count);
    }

    [Fact]
    public async Task Get_ReturnsVerbatimGroupTitlesAndSpecOrderedRows()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await GetAsync(PrivilegesUrl, jar);
        var body = await ReadAsync<PrivilegeRegisterResponse>(response);

        var administration = body.Groups.Single(group => group.Key == "administration");
        administration.Title.ShouldBe("Administration and access control");

        // Spec 4.4.1's first row, in spec table order.
        administration.Privileges[0].Code.ShouldBe("admin.view");
        administration.Privileges[0].Permits.ShouldBe("List and open admin accounts.");
        administration.Privileges[0].Scopable.ShouldBeFalse();

        var academicStructure = body.Groups.Single(group => group.Key == "academic_structure");

        // arm.view is spec 4.4.3's one scopable row proven here over the wire, not just in the
        // domain-level PrivilegeRegistryTests.
        var armView = academicStructure.Privileges.Single(privilege => privilege.Code == "arm.view");
        armView.Scopable.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_NeverReturnsALegacyGuardianAlias()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await GetAsync(PrivilegesUrl, jar);
        var body = await ReadAsync<PrivilegeRegisterResponse>(response);

        var codes = body.Groups.SelectMany(group => group.Privileges).Select(privilege => privilege.Code);

        codes.ShouldNotContain(code => code.StartsWith("guardian.", StringComparison.Ordinal));

        // The canonical replacement is present instead.
        codes.ShouldContain(Privileges.Contact.View);
    }

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> GetAsync(string url, CookieJar jar) => GetAsync(Client, url, jar);

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload) =>
        PostAsync(Client, url, jar, payload);

    private static async Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = System.Net.Http.Json.JsonContent.Create(payload),
        };

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
