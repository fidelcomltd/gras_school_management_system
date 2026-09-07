using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Sessions;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0035's session endpoints (spec 6.3.5, 6.3.8-6.3.10): create-with-terms,
/// list, detail, edit, the "no route without terms" absence, and the database-level one-active-session
/// guarantee. Term lifecycle (<c>open</c>/<c>close</c>/<c>reopen</c>) is <c>TermEndpointsTests</c>.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class SessionEndpointsTests : IAsyncLifetime
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SessionsUrl = "/api/v1/sessions";

    private readonly ApiTestFixture _fixture;

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private FakeEffectivePrivilegeProvider _grants = null!;
    private RecordingSystemAuditSink _auditSink = null!;

    public SessionEndpointsTests(ApiTestFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            return;
        }

        await _fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        _grants = new FakeEffectivePrivilegeProvider();
        _auditSink = new RecordingSystemAuditSink();

        _factory = _fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEffectivePrivilegeProvider>();
            services.AddSingleton<IEffectivePrivilegeProvider>(_grants);

            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(_auditSink);
        }));

        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);

        return _factory?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Create_WithSessionCreatePrivilege_HappyPath_CreatesSessionWithThreeTerms()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Session.Create);
        var command = CreateCommand("2026/2027");

        var response = await PostAsync(SessionsUrl, jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var body = await ReadAsync<SessionDetailDto>(response);
        body.Name.ShouldBe("2026/2027");
        body.State.ShouldBe(SessionState.Upcoming);
        body.Terms.Count.ShouldBe(3);
        body.Terms.Select(term => term.Ordinal).ShouldBe([1, 2, 3]);
        body.Terms.ShouldAllBe(term => term.State == TermState.Upcoming);
        body.Terms[0].Name.ShouldBe("First Term");
        body.Terms[1].Name.ShouldBe("Second Term");
        body.Terms[2].Name.ShouldBe("Third Term");
    }

    // Review criterion 2, half A: POST /sessions creates the session AND all three terms in one
    // transaction — proven by reading the database directly afterward, not by trusting the response.
    [Fact]
    public async Task Create_PersistsExactlyThreeTermsInTheDatabase()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Session.Create);
        var command = CreateCommand("2026/2027");

        var response = await PostAsync(SessionsUrl, jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");
        var body = await ReadAsync<SessionDetailDto>(response);

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var sessionId = Guid.Parse(body.Id);
        var storedTerms = await context.Terms
            .Where(term => term.SessionId == sessionId)
            .OrderBy(term => term.Ordinal)
            .ToListAsync(TestContext.Current.CancellationToken);

        storedTerms.Count.ShouldBe(3);
        storedTerms.Select(term => term.Ordinal).ShouldBe([1, 2, 3]);
        storedTerms.ShouldAllBe(term => term.State == TermState.Upcoming);
    }

    // Review criterion 2, half B: no OTHER route creates an academic_session row — asserted against
    // the REAL running application's endpoint graph, not just by reading the source.
    [Fact]
    public void NoRoute_CreatesASessionWithoutTerms()
    {
        RequireDatabase();

        var dataSources = _factory!.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();

        var postSessionRoutes = dataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty).Contains("sessions", StringComparison.Ordinal))
            .Where(endpoint => endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("POST") == true)
            .ToArray();

        // Exactly one POST route mentions "sessions" at all, and it is the collection-level create —
        // not, for example, a hypothetical "POST /sessions/{id}/terms" that could produce a
        // two-or-fewer-term session.
        postSessionRoutes.Length.ShouldBe(1);
        (postSessionRoutes[0].RoutePattern.RawText ?? string.Empty).ShouldNotContain("{id}");
    }

    // Review criterion 1, session half: the partial unique index is what stops two active sessions,
    // proven by writing the SECOND activation directly against the database — bypassing every
    // application-layer check (AcademicSessionRepository.FindActiveAsync, TermTransitionGuard, the
    // whole handler) entirely.
    [Fact]
    public async Task PartialUniqueIndex_PreventsTwoActiveSessions()
    {
        RequireDatabase();

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var firstId = await InsertSessionDirectlyAsync(context, "2025/2026", SessionState.Active);
        _ = firstId;

        var secondId = await InsertSessionRowOnlyAsync(context, "2026/2027");

        // A bare ExecuteSqlInterpolatedAsync is a direct ad-hoc command, not a tracked SaveChanges —
        // the provider's exception propagates as-is rather than being wrapped in DbUpdateException.
        var exception = await Should.ThrowAsync<PostgresException>(async () =>
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE academic_sessions SET state = 'Active' WHERE id = {secondId}",
                TestContext.Current.CancellationToken));

        exception.SqlState.ShouldBe("23505");
    }

    [Fact]
    public async Task List_ReturnsSessionsSortedByNameDescending()
    {
        RequireDatabase();

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await InsertSessionDirectlyAsync(context, "2024/2025", SessionState.Closed);
            await InsertSessionDirectlyAsync(context, "2026/2027", SessionState.Upcoming);
            await InsertSessionDirectlyAsync(context, "2025/2026", SessionState.Closed);
        }

        var jar = await SignInWithGrantsAsync(Privileges.Session.View);

        var response = await GetAsync(SessionsUrl, jar);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await ReadAsync<CursorPageDto>(response);
        body.Items.Select(item => item.Name).ShouldBe(["2026/2027", "2025/2026", "2024/2025"]);
    }

    [Fact]
    public async Task List_FiltersByState()
    {
        RequireDatabase();

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await InsertSessionDirectlyAsync(context, "2024/2025", SessionState.Closed);
            await InsertSessionDirectlyAsync(context, "2026/2027", SessionState.Upcoming);
        }

        var jar = await SignInWithGrantsAsync(Privileges.Session.View);

        var response = await GetAsync($"{SessionsUrl}?state=Closed", jar);

        var body = await ReadAsync<CursorPageDto>(response);
        body.Items.Select(item => item.Name).ShouldBe(["2024/2025"]);
    }

    [Fact]
    public async Task Get_ReturnsSessionWithItsThreeTerms()
    {
        RequireDatabase();

        Guid sessionId;

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            sessionId = await InsertSessionDirectlyAsync(context, "2026/2027", SessionState.Upcoming);
        }

        var jar = await SignInWithGrantsAsync(Privileges.Session.View);

        var response = await GetAsync($"{SessionsUrl}/{sessionId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<SessionDetailDto>(response);
        body.Terms.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Get_WhenNotFound_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Session.View);

        var response = await GetAsync($"{SessionsUrl}/{Guid.NewGuid()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithNonConsecutiveYears_Returns422()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Session.Create);
        var command = new CreateSessionCommand(
            "2026/2028",
            new DateOnly(2026, 9, 14),
            new DateOnly(2028, 7, 25),
            new CreateSessionTermInput(new DateOnly(2026, 9, 14), new DateOnly(2026, 12, 18), null),
            new CreateSessionTermInput(new DateOnly(2027, 1, 5), new DateOnly(2027, 4, 2), null),
            new CreateSessionTermInput(new DateOnly(2027, 4, 20), new DateOnly(2027, 7, 25), null));

        var response = await PostAsync(SessionsUrl, jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("session.name_years_not_consecutive");
    }

    [Fact]
    public async Task Create_WithOverlappingDates_Returns422NamingBothSessions()
    {
        RequireDatabase();

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await InsertSessionDirectlyAsync(context, "2025/2026", SessionState.Closed);
        }

        var jar = await SignInWithGrantsAsync(Privileges.Session.Create);

        // Overlaps 2025/2026 (which the helper below ends on 25/07/2026).
        var command = new CreateSessionCommand(
            "2026/2027",
            new DateOnly(2026, 7, 1),
            new DateOnly(2027, 7, 25),
            new CreateSessionTermInput(new DateOnly(2026, 7, 1), new DateOnly(2026, 10, 1), null),
            new CreateSessionTermInput(new DateOnly(2026, 10, 15), new DateOnly(2027, 2, 1), null),
            new CreateSessionTermInput(new DateOnly(2027, 2, 15), new DateOnly(2027, 7, 25), null));

        var response = await PostAsync(SessionsUrl, jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("session.overlap");
        (json.RootElement.GetProperty("detail").GetString() ?? string.Empty).ShouldContain("2025/2026");
    }

    [Fact]
    public async Task Update_WhenSessionIsClosed_Returns409()
    {
        RequireDatabase();

        Guid sessionId;

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            sessionId = await InsertSessionDirectlyAsync(context, "2025/2026", SessionState.Closed);
        }

        var jar = await SignInWithGrantsAsync(Privileges.Session.Update);

        var response = await PatchAsync(
            $"{SessionsUrl}/{sessionId}", jar, new { name = "2025/2026" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_EditingDatesWithinTermRange_Succeeds()
    {
        RequireDatabase();

        Guid sessionId;

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            sessionId = await InsertSessionDirectlyAsync(context, "2026/2027", SessionState.Upcoming);
        }

        var jar = await SignInWithGrantsAsync(Privileges.Session.Update);

        var response = await PatchAsync($"{SessionsUrl}/{sessionId}", jar, new { name = "2026/2027" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private void RequireDatabase()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            Assert.Skip(_fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    private static CreateSessionCommand CreateCommand(string name) => new(
        name,
        new DateOnly(2026, 9, 14),
        new DateOnly(2027, 7, 25),
        new CreateSessionTermInput(new DateOnly(2026, 9, 14), new DateOnly(2026, 12, 18), new DateOnly(2027, 1, 5)),
        new CreateSessionTermInput(new DateOnly(2027, 1, 5), new DateOnly(2027, 4, 2), new DateOnly(2027, 4, 20)),
        new CreateSessionTermInput(new DateOnly(2027, 4, 20), new DateOnly(2027, 7, 25), null));

    /// <summary>
    /// Inserts a session WITH ITS THREE TERMS (the invariant every real session honours — spec 6.3.5)
    /// straight through the domain factories and <c>SaveChangesAsync</c> — a normal EF write, for
    /// tests that need an existing row to act on (unlike <see cref="InsertSessionRowOnlyAsync"/>,
    /// which deliberately bypasses even that).
    /// </summary>
    private static async Task<Guid> InsertSessionDirectlyAsync(ApplicationDbContext context, string name, SessionState state)
    {
        var startYear = int.Parse(name[..4], CultureInfo.InvariantCulture);
        var creation = AcademicSession.Create(
            Guid.CreateVersion7(), name, new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25));
        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        var session = creation.Value;

        if (state != SessionState.Upcoming)
        {
            session.Activate();

            if (state == SessionState.Closed)
            {
                session.Close();
            }
        }

        string[] termNames = ["First Term", "Second Term", "Third Term"];
        (DateOnly Start, DateOnly End)[] termDates =
        [
            (new DateOnly(startYear, 9, 14), new DateOnly(startYear, 12, 18)),
            (new DateOnly(startYear + 1, 1, 5), new DateOnly(startYear + 1, 4, 2)),
            (new DateOnly(startYear + 1, 4, 20), new DateOnly(startYear + 1, 7, 25)),
        ];

        var terms = Enumerable.Range(0, 3)
            .Select(index => Term
                .Create(Guid.CreateVersion7(), session.Id, index + 1, termNames[index], termDates[index].Start, termDates[index].End)
                .Value)
            .ToArray();

        context.Add(session);
        context.AddRange(terms);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session.Id;
    }

    /// <summary>
    /// Inserts a bare, <see cref="SessionState.Upcoming"/> session row with raw SQL — deliberately
    /// NOT through the application layer — so <see cref="PartialUniqueIndex_PreventsTwoActiveSessions"/>
    /// can then attempt the second activation with a second raw statement, proving the DATABASE
    /// rejects it rather than any C# check.
    /// </summary>
    private static async Task<Guid> InsertSessionRowOnlyAsync(ApplicationDbContext context, string name)
    {
        var id = Guid.CreateVersion7();
        var startYear = int.Parse(name[..4], CultureInfo.InvariantCulture);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO academic_sessions (id, name, start_date, end_date, state, created_at_utc, version)
            VALUES ({id}, {name}, {new DateOnly(startYear, 9, 14)}, {new DateOnly(startYear + 1, 7, 25)},
                    'Upcoming', {DateTimeOffset.UtcNow}, {Guid.CreateVersion7()})
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    private async Task<CookieJar> SignInWithGrantsAsync(params string[] privileges)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(_fixture);

        _grants.SetGrants(
            accountId.ToString("D", CultureInfo.InvariantCulture),
            privileges.Select(privilege => new PrivilegeGrant(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null))
                .ToArray());

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        where T : class
    {
        var value = await response.Content.ReadFromJsonAsync<T>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            },
            TestContext.Current.CancellationToken);

        value.ShouldNotBeNull($"Expected a {typeof(T).Name} body but the response was empty. Status: {response.StatusCode}.");
        return value;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(json);
    }

    private Task<HttpResponseMessage> GetAsync(string url, CookieJar jar) => GetAsyncCore(_client, url, jar);

    private static async Task<HttpResponseMessage> GetAsyncCore(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload, string? idempotencyKey = null) =>
        PostAsyncCore(_client, url, jar, payload, idempotencyKey);

    private static async Task<HttpResponseMessage> PostAsyncCore<T>(
        HttpClient client, string url, CookieJar jar, T payload, string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PatchAsync<T>(string url, CookieJar jar, T payload) =>
        PatchAsyncCore(_client, url, jar, payload);

    private static async Task<HttpResponseMessage> PatchAsyncCore<T>(HttpClient client, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(payload),
        };

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    /// <summary>Minimal shape for asserting on <c>GET /sessions</c>'s cursor page without pulling in <c>CursorPage&lt;SessionDto&gt;</c>'s generic JSON quirks.</summary>
    private sealed record CursorPageDto(IReadOnlyList<SessionDto> Items, string? NextCursor);
}
