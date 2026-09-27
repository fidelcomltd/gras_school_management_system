using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>The states and LGAs the pupil forms offer (spec 6.5.4): exactly the list the server validates against.</summary>
public sealed class PupilGeographyEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Url = "/api/v1/geography/states";

    [Fact]
    public async Task ReturnsEveryStateWithItsLgas_InTheServersOwnSpellings_Cacheably()
    {
        RequireDatabase();
        var jar = await SignInAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, Url);
        jar.Apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.Private.ShouldBeTrue();
        var geography = await ReadAsync<NigerianGeographyDto>(response);
        geography.States.Select(state => state.Name).ShouldBe(NigerianGeography.States);
        geography.States.Single(state => state.Name == "Lagos").Lgas.ShouldBe(NigerianGeography.LgasOf("Lagos"));
        geography.States.ShouldAllBe(state => state.Lgas.Count > 0);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        RequireDatabase();

        (await Client.GetAsync(Url, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<CookieJar> SignInAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        using (var csrf = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf"))
        {
            jar.Capture(await Client.SendAsync(csrf, TestContext.Current.CancellationToken));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }
}
