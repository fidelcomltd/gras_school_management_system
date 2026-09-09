using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Sessions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0035's term lifecycle (spec 6.3.6, 6.3.9, 6.3.10): schedule edits,
/// open/close/reopen with their named-reason rejections, and the database-level one-active-term
/// guarantee. Session creation/editing is <c>SessionEndpointsTests</c>.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class TermEndpointsTests : IAsyncLifetime
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string TermsUrl = "/api/v1/terms";

    private readonly ApiTestFixture _fixture;

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private FakeEffectivePrivilegeProvider _grants = null!;
    private RecordingSystemAuditSink _auditSink = null!;

    public TermEndpointsTests(ApiTestFixture fixture) => _fixture = fixture;

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
    public async Task Update_EditsScheduleFields()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync("2026/2027", SessionState.Upcoming, [TermState.Upcoming, TermState.Upcoming, TermState.Upcoming]);
        var jar = await SignInWithGrantsAsync(Privileges.Session.Update);

        var response = await PatchAsync(
            $"{TermsUrl}/{termIds[0]}",
            jar,
            new { name = "First Term (renamed)", timesSchoolOpened = 45 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<TermDto>(response);
        body.Name.ShouldBe("First Term (renamed)");
        body.TimesSchoolOpened.ShouldBe(45);
    }

    [Fact]
    public async Task Update_TimesSchoolOpenedOnAClosedTerm_Returns409()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync(
            "2026/2027", SessionState.Active, [TermState.Closed, TermState.Upcoming, TermState.Upcoming], timesSchoolOpened: [60, null, null]);

        var jar = await SignInWithGrantsAsync(Privileges.Session.Update);

        var response = await PatchAsync($"{TermsUrl}/{termIds[0]}", jar, new { timesSchoolOpened = 70 });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("term.times_school_opened_immutable");
    }

    // TASK-0039, spec 6.3.6's third precondition — opening blocked with no arm anywhere in the session.
    [Fact]
    public async Task Open_WithNoArmsForSession_Returns409NamingTheSession()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync("2026/2027", SessionState.Upcoming, [TermState.Upcoming, TermState.Upcoming, TermState.Upcoming]);
        var jar = await SignInWithGrantsAsync(Privileges.Term.Open);

        var response = await PostAsync($"{TermsUrl}/{termIds[0]}/open", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("term.no_arms_for_session");
        (json.RootElement.GetProperty("detail").GetString() ?? string.Empty).ShouldContain("2026/2027");
    }

    [Fact]
    public async Task Open_OrdinalOne_WithNoActiveTermAnywhere_ActivatesTermAndSession()
    {
        RequireDatabase();

        var (sessionId, termIds) = await SeedSessionAsync("2026/2027", SessionState.Upcoming, [TermState.Upcoming, TermState.Upcoming, TermState.Upcoming]);
        await SeedArmAsync(sessionId);
        var jar = await SignInWithGrantsAsync(Privileges.Term.Open);

        var response = await PostAsync($"{TermsUrl}/{termIds[0]}/open", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<TermDto>(response);
        body.State.ShouldBe(TermState.Active);

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = await context.AcademicSessions.SingleAsync(x => x.Id == sessionId, TestContext.Current.CancellationToken);
        session.State.ShouldBe(SessionState.Active);
    }

    // Spec 6.3.5: opening a new session's First Term also closes the previously active session — even
    // though that session's OWN terms are already all closed (the "lame duck" window). Spec 6.4.7,
    // TASK-0039: every arm in that session closes in the SAME transaction.
    [Fact]
    public async Task Open_OrdinalOne_ClosesThePreviouslyActiveSessionAndItsArms()
    {
        RequireDatabase();

        var (oldSessionId, _) = await SeedSessionAsync(
            "2025/2026", SessionState.Active, [TermState.Closed, TermState.Closed, TermState.Closed],
            timesSchoolOpened: [60, 61, 62]);
        var oldArmId = await SeedArmAsync(oldSessionId);
        var (newSessionId, newTermIds) = await SeedSessionAsync("2026/2027", SessionState.Upcoming, [TermState.Upcoming, TermState.Upcoming, TermState.Upcoming]);
        await SeedArmAsync(newSessionId);

        var jar = await SignInWithGrantsAsync(Privileges.Term.Open);

        var response = await PostAsync($"{TermsUrl}/{newTermIds[0]}/open", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var oldSession = await context.AcademicSessions.SingleAsync(x => x.Id == oldSessionId, TestContext.Current.CancellationToken);
        oldSession.State.ShouldBe(SessionState.Closed);

        var oldArm = await context.Arms.AsNoTracking().SingleAsync(a => a.Id == oldArmId, TestContext.Current.CancellationToken);
        oldArm.Status.ShouldBe(ArmStatus.Closed);
    }

    [Fact]
    public async Task Open_OrdinalTwo_WhenPreviousTermNotClosed_Returns409NamingReason()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync("2026/2027", SessionState.Active, [TermState.Active, TermState.Upcoming, TermState.Upcoming]);
        var jar = await SignInWithGrantsAsync(Privileges.Term.Open);

        var response = await PostAsync($"{TermsUrl}/{termIds[1]}/open", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("term.previous_term_not_closed");
        (json.RootElement.GetProperty("detail").GetString() ?? string.Empty).ShouldContain("First Term");
    }

    // Spec 6.3.6's own example wording: "First Term 2026/2027 cannot be opened because Third Term
    // 2025/2026 is still active. Close it first."
    [Fact]
    public async Task Open_OrdinalOne_WhenAnotherTermIsActiveElsewhere_Returns409NamingItAndItsSession()
    {
        RequireDatabase();

        await SeedSessionAsync("2025/2026", SessionState.Active, [TermState.Closed, TermState.Closed, TermState.Active], timesSchoolOpened: [60, 61, null]);
        var (_, newTermIds) = await SeedSessionAsync("2026/2027", SessionState.Upcoming, [TermState.Upcoming, TermState.Upcoming, TermState.Upcoming]);

        var jar = await SignInWithGrantsAsync(Privileges.Term.Open);

        var response = await PostAsync($"{TermsUrl}/{newTermIds[0]}/open", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("term.previous_session_still_active");
        var detail = json.RootElement.GetProperty("detail").GetString() ?? string.Empty;
        detail.ShouldContain("Third Term");
        detail.ShouldContain("2025/2026");
        detail.ShouldContain("still active");
    }

    [Fact]
    public async Task Close_HappyPath_SetsClosedAtAndClosedBy()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync("2026/2027", SessionState.Active, [TermState.Active, TermState.Upcoming, TermState.Upcoming], timesSchoolOpened: [55, null, null]);
        var jar = await SignInWithGrantsAsync(Privileges.Term.Close);

        var response = await PostAsync($"{TermsUrl}/{termIds[0]}/close", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<TermDto>(response);
        body.State.ShouldBe(TermState.Closed);
        body.ClosedAtUtc.ShouldNotBeNull();
        body.ClosedBy.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Close_WithoutTimesSchoolOpened_Returns422NamingTheTerm()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync("2026/2027", SessionState.Active, [TermState.Active, TermState.Upcoming, TermState.Upcoming]);
        var jar = await SignInWithGrantsAsync(Privileges.Term.Close);

        var response = await PostAsync($"{TermsUrl}/{termIds[0]}/close", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("term.times_school_opened_required_to_close");
        (json.RootElement.GetProperty("detail").GetString() ?? string.Empty).ShouldContain("First Term");
    }

    [Fact]
    public async Task Close_WhenNotActive_Returns409()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync("2026/2027", SessionState.Upcoming, [TermState.Upcoming, TermState.Upcoming, TermState.Upcoming]);
        var jar = await SignInWithGrantsAsync(Privileges.Term.Close);

        var response = await PostAsync($"{TermsUrl}/{termIds[0]}/close", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reopen_AsSuperAdminWithValidReason_Succeeds()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync("2026/2027", SessionState.Active, [TermState.Closed, TermState.Upcoming, TermState.Upcoming], timesSchoolOpened: [55, null, null]);

        var (accountId, email) = await AdminAccountSeeder.SeedAsync(_fixture);
        _grants.SetGrants(
            accountId.ToString("D", CultureInfo.InvariantCulture),
            [new PrivilegeGrant(Privileges.Term.Close, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null)]);

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await PostAsync(
            $"{TermsUrl}/{termIds[0]}/reopen", jar, new { reason = "A mark was entered against the wrong subject." });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<TermDto>(response);
        body.State.ShouldBe(TermState.Active);
        body.ClosedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Reopen_AsNonSuperAdmin_Returns403()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync("2026/2027", SessionState.Active, [TermState.Closed, TermState.Upcoming, TermState.Upcoming], timesSchoolOpened: [55, null, null]);
        var jar = await SignInWithGrantsAsync(Privileges.Term.Close);

        var response = await PostAsync(
            $"{TermsUrl}/{termIds[0]}/reopen", jar, new { reason = "A mark was entered against the wrong subject." });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reopen_WithAReasonUnderTenCharacters_Returns422()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync("2026/2027", SessionState.Active, [TermState.Closed, TermState.Upcoming, TermState.Upcoming], timesSchoolOpened: [55, null, null]);

        var (accountId, email) = await AdminAccountSeeder.SeedAsync(_fixture);
        _grants.SetGrants(
            accountId.ToString("D", CultureInfo.InvariantCulture),
            [new PrivilegeGrant(Privileges.Term.Close, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null)]);

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));

        var response = await PostAsync($"{TermsUrl}/{termIds[0]}/reopen", jar, new { reason = "too short" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Reopen_WhenTheFollowingTermHasAlreadyBeenOpened_Returns409()
    {
        RequireDatabase();

        var (_, termIds) = await SeedSessionAsync(
            "2026/2027", SessionState.Active, [TermState.Closed, TermState.Active, TermState.Upcoming], timesSchoolOpened: [55, null, null]);

        var (accountId, email) = await AdminAccountSeeder.SeedAsync(_fixture);
        _grants.SetGrants(
            accountId.ToString("D", CultureInfo.InvariantCulture),
            [new PrivilegeGrant(Privileges.Term.Close, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null)]);

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));

        var response = await PostAsync(
            $"{TermsUrl}/{termIds[0]}/reopen", jar, new { reason = "A mark was entered against the wrong subject." });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("term.reopen_blocked_by_following_term");
    }

    // Review criterion 1, term half — the one this card's spec paragraph (6.3.9) is explicit about:
    // "Two terms marked active by a data error [is] prevented by a partial unique index... The
    // database, not the application, enforces one active term." Proven by writing the SECOND
    // activation directly against the database, bypassing the application layer entirely.
    [Fact]
    public async Task PartialUniqueIndex_PreventsTwoActiveTerms()
    {
        RequireDatabase();

        var (_, firstTermIds) = await SeedSessionAsync("2025/2026", SessionState.Active, [TermState.Active, TermState.Upcoming, TermState.Upcoming]);
        var (_, secondTermIds) = await SeedSessionAsync("2026/2027", SessionState.Upcoming, [TermState.Upcoming, TermState.Upcoming, TermState.Upcoming]);
        _ = firstTermIds;

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // A bare ExecuteSqlInterpolatedAsync is a direct ad-hoc command, not a tracked SaveChanges —
        // the provider's exception propagates as-is rather than being wrapped in DbUpdateException.
        var exception = await Should.ThrowAsync<PostgresException>(async () =>
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE terms SET state = 'Active' WHERE id = {secondTermIds[0]}",
                TestContext.Current.CancellationToken));

        exception.SqlState.ShouldBe("23505");
    }

    private void RequireDatabase()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            Assert.Skip(_fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    /// <summary>
    /// Builds a session with its three terms directly through the domain (no HTTP, no repository) —
    /// a normal EF write, exactly the shape <c>CreateSessionHandler</c> itself would produce, so tests
    /// exercising open/close/reopen/update do not have to go through <c>POST /sessions</c> first.
    /// </summary>
    private async Task<(Guid SessionId, Guid[] TermIds)> SeedSessionAsync(
        string sessionName,
        SessionState sessionState,
        TermState[] termStates,
        int?[]? timesSchoolOpened = null)
    {
        var startYear = int.Parse(sessionName[..4], CultureInfo.InvariantCulture);
        var sessionCreation = AcademicSession.Create(
            Guid.CreateVersion7(), sessionName, new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25));
        sessionCreation.IsSuccess.ShouldBeTrue();
        var session = sessionCreation.Value;

        if (sessionState != SessionState.Upcoming)
        {
            session.Activate();

            if (sessionState == SessionState.Closed)
            {
                session.Close();
            }
        }

        string[] names = ["First Term", "Second Term", "Third Term"];
        (DateOnly Start, DateOnly End)[] dates =
        [
            (new DateOnly(startYear, 9, 14), new DateOnly(startYear, 12, 18)),
            (new DateOnly(startYear + 1, 1, 5), new DateOnly(startYear + 1, 4, 2)),
            (new DateOnly(startYear + 1, 4, 20), new DateOnly(startYear + 1, 7, 25)),
        ];

        var terms = new Term[3];

        for (var index = 0; index < 3; index++)
        {
            var creation = Term.Create(
                Guid.CreateVersion7(), session.Id, index + 1, names[index], dates[index].Start, dates[index].End);
            creation.IsSuccess.ShouldBeTrue();
            var term = creation.Value;

            if (termStates[index] != TermState.Upcoming)
            {
                term.Open();

                // Closing REQUIRES times school opened, so default it only for a term ending up
                // Closed — an Active-only term is left blank unless the caller explicitly wants one
                // set (Close_WithoutTimesSchoolOpened_Returns422NamingTheTerm needs exactly that).
                var times = timesSchoolOpened?[index]
                    ?? (termStates[index] == TermState.Closed ? 60 : (int?)null);

                if (times is not null)
                {
                    term.SetTimesSchoolOpened(times.Value);
                }

                if (termStates[index] == TermState.Closed)
                {
                    term.Close(DateTimeOffset.UtcNow, "seed");
                }
            }

            terms[index] = term;
        }

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Add(session);
        context.AddRange(terms);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (session.Id, terms.Select(term => term.Id).ToArray());
    }

    /// <summary>Seeds one active arm for <paramref name="sessionId"/> under the first seeded level, so an open-term call satisfies TASK-0039's precondition. Returns the new arm's id.</summary>
    private async Task<Guid> SeedArmAsync(Guid sessionId)
    {
        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var level = await context.ClassLevels.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken);
        var arm = Arm.Create(Guid.CreateVersion7(), level.Id, sessionId, "A", null, null).Value;

        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return arm.Id;
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

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload) =>
        PostAsyncCore(_client, url, jar, payload);

    private Task<HttpResponseMessage> PostAsync(string url, CookieJar jar) =>
        PostAsyncCore<object?>(_client, url, jar, null);

    private static async Task<HttpResponseMessage> PostAsyncCore<T>(HttpClient client, string url, CookieJar jar, T? payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = payload is null ? null : JsonContent.Create(payload),
        };

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
}
