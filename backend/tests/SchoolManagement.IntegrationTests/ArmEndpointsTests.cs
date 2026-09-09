using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Classes;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0039's arm endpoints (spec 6.4.3, 6.4.5, 6.4.6, 6.4.7, 6.4.8, 6.4.9): the
/// per-session record, the composed display name, label uniqueness backed by a database index, the
/// session-state and level-state creation preconditions, bulk creation, and the automatic close on
/// session close (that last one is proven end to end in <c>TermEndpointsTests</c>, alongside the
/// resolved <c>POST /terms/{id}/open</c> arm precondition — both are this card's drift resolutions).
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class ArmEndpointsTests : IAsyncLifetime
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string ArmsUrl = "/api/v1/arms";

    private readonly ApiTestFixture _fixture;

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private FakeEffectivePrivilegeProvider _grants = null!;
    private RecordingSystemAuditSink _auditSink = null!;

    public ArmEndpointsTests(ApiTestFixture fixture) => _fixture = fixture;

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

    // Spec 6.4.7: "Primary 2A for 2026/2027 and Primary 2A for 2027/2028 are different rows."
    [Fact]
    public async Task Create_TheSameLabelInTwoDifferentSessions_BothSucceed()
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync();
        var session1 = await SeedSessionAsync("2026/2027", SessionState.Upcoming);
        var session2 = await SeedSessionAsync("2027/2028", SessionState.Upcoming);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create);

        var first = await PostAsync(ArmsUrl, jar, new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), session1.ToString("D", CultureInfo.InvariantCulture), "A", null, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");
        var second = await PostAsync(ArmsUrl, jar, new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), session2.ToString("D", CultureInfo.InvariantCulture), "A", null, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // Spec 6.4.3, Appendix A entry 12: a level with a single arm still renders level + label.
    [Fact]
    public async Task Create_UnderALevelWithASingleArm_StillReturnsTheComposedDisplayName()
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync("Primary 4");
        var sessionId = await SeedSessionAsync("2026/2027", SessionState.Upcoming);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create);

        var response = await PostAsync(ArmsUrl, jar, new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), sessionId.ToString("D", CultureInfo.InvariantCulture), "A", null, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadAsync<ArmDto>(response);
        body.DisplayName.ShouldBe("Primary 4A");
    }

    // Spec 6.4.8: "Label entered as lowercase b: normalised to uppercase on save."
    [Fact]
    public async Task Create_WithALowercaseSingleLetterLabel_NormalisesToUppercase()
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync();
        var sessionId = await SeedSessionAsync("2026/2027", SessionState.Upcoming);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create);

        var response = await PostAsync(ArmsUrl, jar, new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), sessionId.ToString("D", CultureInfo.InvariantCulture), "b", null, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadAsync<ArmDto>(response);
        body.Label.ShouldBe("B");
    }

    [Fact]
    public async Task Create_DuplicateLabelSameLevelAndSession_Returns409()
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync();
        var sessionId = await SeedSessionAsync("2026/2027", SessionState.Upcoming);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create);
        var command = new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), sessionId.ToString("D", CultureInfo.InvariantCulture), "A", null, null);

        var first = await PostAsync(ArmsUrl, jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await PostAsync(ArmsUrl, jar, command with { }, idempotencyKey: $"key-{Guid.NewGuid():N}");

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(second);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("arm.label_duplicate");
    }

    // Acceptance criterion: a concurrency test proving the UNIQUE INDEX — not the application
    // validator — rejects the second of two SIMULTANEOUS inserts. Both requests are built and fired
    // before either is awaited, exactly the technique AdminAccountEndpointsTests uses for its own
    // concurrent-suspend proof.
    [Fact]
    public async Task Create_TwoSimultaneousInsertsWithTheSameLabel_TheDatabaseIndexRejectsTheSecond()
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync();
        var sessionId = await SeedSessionAsync("2026/2027", SessionState.Upcoming);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create);

        var command = new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), sessionId.ToString("D", CultureInfo.InvariantCulture), "A", null, null);

        var request1 = BuildPostRequest(ArmsUrl, jar, command, $"key-{Guid.NewGuid():N}");
        var request2 = BuildPostRequest(ArmsUrl, jar, command with { }, $"key-{Guid.NewGuid():N}");

        try
        {
            var task1 = _client.SendAsync(request1, TestContext.Current.CancellationToken);
            var task2 = _client.SendAsync(request2, TestContext.Current.CancellationToken);

            var results = await Task.WhenAll(task1, task2);
            var statusCodes = results.Select(response => response.StatusCode).OrderBy(code => code).ToArray();

            statusCodes.ShouldBe([HttpStatusCode.Created, HttpStatusCode.Conflict]);

            foreach (var result in results)
            {
                result.Dispose();
            }
        }
        finally
        {
            request1.Dispose();
            request2.Dispose();
        }
    }

    [Fact]
    public async Task Create_AgainstAClosedSession_Returns409()
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync();
        var sessionId = await SeedSessionAsync("2025/2026", SessionState.Closed);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create);

        var response = await PostAsync(ArmsUrl, jar, new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), sessionId.ToString("D", CultureInfo.InvariantCulture), "A", null, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("arm.session_closed");
    }

    [Theory]
    [InlineData(SessionState.Upcoming)]
    [InlineData(SessionState.Active)]
    public async Task Create_AgainstAnUpcomingOrActiveSession_Succeeds(SessionState state)
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync();
        var sessionId = await SeedSessionAsync("2026/2027", state);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create);

        var response = await PostAsync(ArmsUrl, jar, new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), sessionId.ToString("D", CultureInfo.InvariantCulture), "A", null, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // Spec 6.4.7: "May an arm exist under an inactive level? Existing arms yes, new arms no."
    [Fact]
    public async Task Create_UnderAnInactiveLevel_FailsButAnExistingArmUnderItSurvives()
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync();
        var sessionId = await SeedSessionAsync("2026/2027", SessionState.Upcoming);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create, Privileges.Arm.View);

        var existing = await PostAsync(ArmsUrl, jar, new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), sessionId.ToString("D", CultureInfo.InvariantCulture), "A", null, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");
        existing.StatusCode.ShouldBe(HttpStatusCode.Created);
        var existingArmId = (await ReadAsync<ArmDto>(existing)).Id;

        await DeactivateLevelAsync(levelId);

        var rejected = await PostAsync(ArmsUrl, jar, new CreateArmCommand(
            levelId.ToString("D", CultureInfo.InvariantCulture), sessionId.ToString("D", CultureInfo.InvariantCulture), "B", null, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");
        rejected.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var survivorResponse = await GetAsync($"{ArmsUrl}/{existingArmId}", jar);
        survivorResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<ArmDto>(survivorResponse)).Status.ShouldBe(ArmStatus.Active);
    }

    [Fact]
    public async Task GetNextLabel_WithNoExistingArms_ReturnsA()
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync();
        var sessionId = await SeedSessionAsync("2026/2027", SessionState.Upcoming);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.View);

        var response = await GetAsync(
            $"{ArmsUrl}/next-label?levelId={levelId:D}&sessionId={sessionId:D}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<NextArmLabelResponse>(response)).Label.ShouldBe("A");
    }

    [Fact]
    public async Task GetNextLabel_WithAAndBAlreadyTaken_ReturnsC()
    {
        RequireDatabase();

        var levelId = await SeedLevelIdAsync();
        var sessionId = await SeedSessionAsync("2026/2027", SessionState.Upcoming);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create, Privileges.Arm.View);

        foreach (var label in new[] { "A", "B" })
        {
            var response = await PostAsync(ArmsUrl, jar, new CreateArmCommand(
                levelId.ToString("D", CultureInfo.InvariantCulture), sessionId.ToString("D", CultureInfo.InvariantCulture), label, null, null),
                idempotencyKey: $"key-{Guid.NewGuid():N}");
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var next = await GetAsync($"{ArmsUrl}/next-label?levelId={levelId:D}&sessionId={sessionId:D}", jar);

        (await ReadAsync<NextArmLabelResponse>(next)).Label.ShouldBe("C");
    }

    // Spec 6.4.6: capacity is a SOFT limit — an update is never blocked by it.
    [Fact]
    public async Task Update_Capacity_IsNeverBlocked()
    {
        RequireDatabase();

        var armId = await SeedArmAsync(await SeedLevelIdAsync(), await SeedSessionAsync("2026/2027", SessionState.Upcoming), "A");
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Update);

        var response = await PatchAsync($"{ArmsUrl}/{armId}", jar, new { capacity = 5 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<ArmDto>(response)).Capacity.ShouldBe(5);
    }

    [Fact]
    public async Task Update_FormTeacher_WithoutTheAssignPrivilege_Returns403()
    {
        RequireDatabase();

        var (teacherId, _, _) = await AdminAccountSeeder.SeedRegularAsync(_fixture);
        var armId = await SeedArmAsync(await SeedLevelIdAsync(), await SeedSessionAsync("2026/2027", SessionState.Upcoming), "A");
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Update);

        var response = await PatchAsync(
            $"{ArmsUrl}/{armId}", jar, new { formTeacherAdminId = teacherId.ToString("D", CultureInfo.InvariantCulture) });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_FormTeacher_WithTheAssignPrivilege_Succeeds()
    {
        RequireDatabase();

        var (teacherId, _, _) = await AdminAccountSeeder.SeedRegularAsync(_fixture);
        var armId = await SeedArmAsync(await SeedLevelIdAsync(), await SeedSessionAsync("2026/2027", SessionState.Upcoming), "A");
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Update, Privileges.Arm.FormTeacherAssign);

        var response = await PatchAsync(
            $"{ArmsUrl}/{armId}", jar, new { formTeacherAdminId = teacherId.ToString("D", CultureInfo.InvariantCulture) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<ArmDto>(response)).FormTeacherAdminId.ShouldBe(teacherId.ToString("D", CultureInfo.InvariantCulture));
    }

    // Spec 6.4.7: "The arm becomes read-only... " once its session has closed.
    [Fact]
    public async Task Update_OnAClosedArm_Returns409()
    {
        RequireDatabase();

        var armId = await SeedArmAsync(
            await SeedLevelIdAsync(), await SeedSessionAsync("2025/2026", SessionState.Closed), "A", ArmStatus.Closed);
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Update);

        var response = await PatchAsync($"{ArmsUrl}/{armId}", jar, new { capacity = 20 });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("arm.closed_immutable");
    }

    [Fact]
    public async Task Delete_AnArmThatHasNeverHeldAnEnrolment_Succeeds()
    {
        RequireDatabase();

        var armId = await SeedArmAsync(await SeedLevelIdAsync(), await SeedSessionAsync("2026/2027", SessionState.Upcoming), "A");
        var jar = await SignInWithGrantsAsync(Privileges.Arm.Delete);

        var response = await DeleteAsync($"{ArmsUrl}/{armId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Arm.Delete);

        var response = await DeleteAsync($"{ArmsUrl}/{Guid.NewGuid()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Spec 6.4.3/6.4.8: bulk creation continues from each level's highest existing label, skips
    // levels given zero arms, and dryRun writes NOTHING — asserted by row count, not response shape.
    [Fact]
    public async Task BulkCreate_ContinuesFromHighestLabel_SkipsZeroArmLevels_AndDryRunWritesNothing()
    {
        RequireDatabase();

        var levelWithExisting = await SeedLevelIdAsync("Primary 1");
        var levelSkipped = await SeedLevelIdAsync("Primary 2");
        var levelFresh = await SeedLevelIdAsync("Primary 3");
        var sessionId = await SeedSessionAsync("2026/2027", SessionState.Upcoming);
        await SeedArmAsync(levelWithExisting, sessionId, "A");

        var jar = await SignInWithGrantsAsync(Privileges.Arm.Create);

        var command = new BulkCreateArmsCommand(
            sessionId.ToString("D", CultureInfo.InvariantCulture),
            [
                new BulkCreateArmsLevelEntry(levelWithExisting.ToString("D", CultureInfo.InvariantCulture), 1, null),
                new BulkCreateArmsLevelEntry(levelSkipped.ToString("D", CultureInfo.InvariantCulture), 0, null),
                new BulkCreateArmsLevelEntry(levelFresh.ToString("D", CultureInfo.InvariantCulture), 2, 25),
            ],
            DryRun: true);

        int countBeforeDryRun;

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            countBeforeDryRun = await context.Arms.CountAsync(TestContext.Current.CancellationToken);
        }

        var dryRunResponse = await PostAsync(ArmsUrl + "/bulk", jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");
        dryRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dryRunBody = await ReadAsync<BulkCreateArmsResponse>(dryRunResponse);
        dryRunBody.Created.Count.ShouldBe(3); // 1 (continues at B) + 0 (skipped) + 2 (A, B)

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var countAfterDryRun = await context.Arms.CountAsync(TestContext.Current.CancellationToken);
            countAfterDryRun.ShouldBe(countBeforeDryRun); // dry run wrote nothing
        }

        var realResponse = await PostAsync(ArmsUrl + "/bulk", jar, command with { DryRun = false }, idempotencyKey: $"key-{Guid.NewGuid():N}");
        realResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var realBody = await ReadAsync<BulkCreateArmsResponse>(realResponse);

        var continued = realBody.Created.Single(arm => arm.ClassLevelId == levelWithExisting.ToString("D", CultureInfo.InvariantCulture));
        continued.Label.ShouldBe("B"); // continues from the existing "A"

        realBody.Created.ShouldNotContain(arm => arm.ClassLevelId == levelSkipped.ToString("D", CultureInfo.InvariantCulture));

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var countAfterReal = await context.Arms.CountAsync(TestContext.Current.CancellationToken);
            countAfterReal.ShouldBe(countBeforeDryRun + 3);
        }
    }

    private void RequireDatabase()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            Assert.Skip(_fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    /// <summary>Returns the id of a seeded ACTIVE level by name (defaults to "Primary 1").</summary>
    private async Task<Guid> SeedLevelIdAsync(string name = "Primary 1")
    {
        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await context.ClassLevels.AsNoTracking().SingleAsync(
            level => level.Name == name, TestContext.Current.CancellationToken)).Id;
    }

    private async Task DeactivateLevelAsync(Guid levelId)
    {
        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var level = await context.ClassLevels.SingleAsync(l => l.Id == levelId, TestContext.Current.CancellationToken);
        level.Deactivate();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Creates a session directly through the domain factory (no terms — arms do not need them).</summary>
    private async Task<Guid> SeedSessionAsync(string name, SessionState state)
    {
        var startYear = int.Parse(name[..4], CultureInfo.InvariantCulture);
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), name, new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;

        if (state != SessionState.Upcoming)
        {
            session.Activate();

            if (state == SessionState.Closed)
            {
                session.Close();
            }
        }

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session.Id;
    }

    /// <summary>Seeds one arm directly through the domain factory. Returns its id.</summary>
    private async Task<Guid> SeedArmAsync(Guid levelId, Guid sessionId, string label, ArmStatus status = ArmStatus.Active)
    {
        var arm = Arm.Create(Guid.CreateVersion7(), levelId, sessionId, label, null, null).Value;

        if (status == ArmStatus.Inactive)
        {
            arm.Deactivate();
        }
        else if (status == ArmStatus.Closed)
        {
            arm.Close();
        }

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload, string? idempotencyKey = null) =>
        PostAsyncCore(_client, url, jar, payload, idempotencyKey);

    private static async Task<HttpResponseMessage> PostAsyncCore<T>(
        HttpClient client, string url, CookieJar jar, T payload, string? idempotencyKey)
    {
        using var request = BuildPostRequest(url, jar, payload, idempotencyKey);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private static HttpRequestMessage BuildPostRequest<T>(string url, CookieJar jar, T payload, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        jar.ApplyWithCsrf(request);
        return request;
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

    private Task<HttpResponseMessage> DeleteAsync(string url, CookieJar jar) => DeleteAsyncCore(_client, url, jar);

    private static async Task<HttpResponseMessage> DeleteAsyncCore(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
