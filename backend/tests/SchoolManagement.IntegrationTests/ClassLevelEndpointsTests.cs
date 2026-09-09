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
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0038's class level endpoints (spec 6.4.1, 6.4.2, 6.4.7, 6.4.8, 6.4.9):
/// the nine-level seeded chain, the insert-after worked case, the eight chain rules wired through
/// HTTP, deactivation (including the dedicated strand precondition), delete, reorder, and the two
/// database-level guarantees (name/order uniqueness, the config-version ledger left untouched by a
/// rename). Everything about arms is TASK-0039 and is not exercised here.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class ClassLevelEndpointsTests : IAsyncLifetime
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string LevelsUrl = "/api/v1/levels";

    private readonly ApiTestFixture _fixture;

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private FakeEffectivePrivilegeProvider _grants = null!;
    private RecordingSystemAuditSink _auditSink = null!;

    public ClassLevelEndpointsTests(ApiTestFixture fixture) => _fixture = fixture;

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
    public async Task List_ReturnsNineSeededLevels_InChainOrderWithEntryAndGraduatingFlags()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.View);

        var response = await GetAsync(LevelsUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<CursorPageDto>(response);
        body.Items.Count.ShouldBe(9);
        body.Items.Select(item => item.Name).ShouldBe(
        [
            "Nursery 1", "Nursery 2", "Nursery 3",
            "Primary 1", "Primary 2", "Primary 3", "Primary 4", "Primary 5", "Primary 6",
        ]);

        body.Items[0].IsEntryLevel.ShouldBeTrue();
        body.Items[0].IsGraduatingLevel.ShouldBeFalse();
        body.Items[^1].IsGraduatingLevel.ShouldBeTrue();
        body.Items[^1].IsEntryLevel.ShouldBeFalse();
        body.Items.Skip(1).Take(7).ShouldAllBe(item => !item.IsEntryLevel && !item.IsGraduatingLevel);
    }

    [Fact]
    public async Task List_WithStatusAll_AlsoReturnsAnInactiveLevel()
    {
        RequireDatabase();

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await InsertStandaloneInactiveLevelAsync(context);
        }

        var jar = await SignInWithGrantsAsync(Privileges.Level.View);

        var defaultResponse = await GetAsync(LevelsUrl, jar);
        var defaultBody = await ReadAsync<CursorPageDto>(defaultResponse);
        defaultBody.Items.Count.ShouldBe(9);

        var allResponse = await GetAsync($"{LevelsUrl}?status=all&pageSize=100", jar);
        var allBody = await ReadAsync<CursorPageDto>(allResponse);
        allBody.Items.Count.ShouldBe(10);
        allBody.Items.ShouldContain(item => item.Name == "Reserve Level" && item.Status == LevelStatus.Inactive);
    }

    [Fact]
    public async Task Get_WhenNotFound_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.View);

        var response = await GetAsync($"{LevelsUrl}/{Guid.NewGuid()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Spec 6.4.2's own worked case, end to end: inserting Reception after Nursery 3 creates it with
    // order 4, points Nursery 3 at it and it at Primary 1, and shifts Primary 1-6 up by one — in one
    // transaction. This is also the acceptance criterion that nothing assumes exactly nine levels:
    // Reception becomes the TENTH, and the chain still validates.
    [Fact]
    public async Task Create_WithInsertAfterLevelId_WorkedCase_InsertsAndShiftsLaterOrders()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Create, Privileges.Level.View);
        var (nursery3Id, primary1Id, sectionId) = await FindSeededAsync("Nursery 3", "Primary 1");

        var command = new CreateLevelCommand(
            "Reception", sectionId.ToString("D", CultureInfo.InvariantCulture), null, null,
            nursery3Id.ToString("D", CultureInfo.InvariantCulture));

        var createResponse = await PostAsync(LevelsUrl, jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<LevelDto>(createResponse);
        created.ProgressionOrder.ShouldBe(4);
        created.NextLevelId.ShouldBe(primary1Id.ToString("D", CultureInfo.InvariantCulture));

        var listResponse = await GetAsync($"{LevelsUrl}?pageSize=100", jar);
        var body = await ReadAsync<CursorPageDto>(listResponse);

        body.Items.Count.ShouldBe(10);
        body.Items.Select(item => item.Name).ShouldBe(
        [
            "Nursery 1", "Nursery 2", "Nursery 3", "Reception",
            "Primary 1", "Primary 2", "Primary 3", "Primary 4", "Primary 5", "Primary 6",
        ]);

        var nursery3 = body.Items.Single(item => item.Name == "Nursery 3");
        nursery3.NextLevelId.ShouldBe(created.Id);

        var primary6 = body.Items.Single(item => item.Name == "Primary 6");
        primary6.ProgressionOrder.ShouldBe(10);
        primary6.IsGraduatingLevel.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithDuplicateName_Returns409()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Create);
        var (_, _, sectionId) = await FindSeededAsync("Nursery 3", "Primary 1");

        var command = new CreateLevelCommand(
            "primary 1", sectionId.ToString("D", CultureInfo.InvariantCulture), 20, null, null);

        var response = await PostAsync(LevelsUrl, jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("level.name_duplicate");
    }

    [Fact]
    public async Task Update_RenamesALevel()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Update, Privileges.Level.View);
        var primary1Id = (await FindSeededAsync("Nursery 3", "Primary 1")).SecondId;

        var response = await PatchAsync($"{LevelsUrl}/{primary1Id}", jar, new { name = "Basic 1" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<LevelDto>(response);
        body.Name.ShouldBe("Basic 1");
    }

    // Acceptance criterion: a save that breaks any rule is rejected WHOLE — not merely a 422, but a
    // rolled-back transaction. The name change here would otherwise succeed on its own; bundling it
    // with a chain-breaking nextLevelId change in the SAME request proves neither took effect.
    [Fact]
    public async Task Update_ThatBreaksAChainRule_RollsBackTheWholeRequest()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Update, Privileges.Level.View);
        var (nursery3Id, primary1Id, _) = await FindSeededAsync("Nursery 3", "Primary 1");

        // Primary 1 already exists as the graduating level's PREDECESSOR chain target; pointing
        // Nursery 3 at itself is a clean, unambiguous rule-1 violation (self-reference).
        var response = await PatchAsync(
            $"{LevelsUrl}/{nursery3Id}", jar, new { name = "Renamed Nursery 3", nextLevelId = nursery3Id.ToString("D", CultureInfo.InvariantCulture) });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("level.chain_self_reference");

        var reread = await ReadAsync<LevelDto>(await GetAsync($"{LevelsUrl}/{nursery3Id}", jar));
        reread.Name.ShouldBe("Nursery 3");
        reread.NextLevelId.ShouldBe(primary1Id.ToString("D", CultureInfo.InvariantCulture));
    }

    // Spec 6.4.2's own worked example, verbatim message.
    [Fact]
    public async Task Update_DeactivatingAMidChainLevel_RejectsNamingPredecessorAndSuccessor()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Update, Privileges.Level.Deactivate, Privileges.Level.View);
        var primary3Id = (await FindSeededAsync("Primary 2", "Primary 3")).SecondId;

        var response = await PatchAsync($"{LevelsUrl}/{primary3Id}", jar, new { status = "Inactive" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("level.deactivate_would_strand_successor");
        json.RootElement.GetProperty("detail").GetString().ShouldBe(
            "Deactivating Primary 3 would leave Primary 4 unreachable. Point Primary 2 at Primary 4 " +
            "first, then deactivate Primary 3.");
    }

    // Spec 6.4.2: deactivating Nursery 1, 2, 3 IN TURN leaves Primary 1 as the entry level with no
    // requirement to clear Nursery 3's pointer, and reactivating restores the original chain.
    [Fact]
    public async Task Update_DeactivatingNurseryLevelsInTurn_LeavesPrimaryOneAsEntry_ReactivationRestoresChain()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Update, Privileges.Level.Deactivate, Privileges.Level.View);
        Guid nursery1Id, nursery2Id, nursery3Id, primary1Id;

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            nursery1Id = await IdOfAsync(context, "Nursery 1");
            nursery2Id = await IdOfAsync(context, "Nursery 2");
            nursery3Id = await IdOfAsync(context, "Nursery 3");
            primary1Id = await IdOfAsync(context, "Primary 1");
        }

        foreach (var id in new[] { nursery1Id, nursery2Id, nursery3Id })
        {
            var response = await PatchAsync($"{LevelsUrl}/{id}", jar, new { status = "Inactive" });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var primary1AfterDeactivation = await ReadAsync<LevelDto>(await GetAsync($"{LevelsUrl}/{primary1Id}", jar));
        primary1AfterDeactivation.IsEntryLevel.ShouldBeTrue();

        // Reactivate in reverse — Nursery 3's stored pointer at Primary 1 was never cleared, and the
        // original chain comes back correctly (spec 6.4.2).
        foreach (var id in new[] { nursery3Id, nursery2Id, nursery1Id })
        {
            var response = await PatchAsync($"{LevelsUrl}/{id}", jar, new { status = "Active" });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var nursery1AfterRestoration = await ReadAsync<LevelDto>(await GetAsync($"{LevelsUrl}/{nursery1Id}", jar));
        nursery1AfterRestoration.IsEntryLevel.ShouldBeTrue();

        var primary1AfterRestoration = await ReadAsync<LevelDto>(await GetAsync($"{LevelsUrl}/{primary1Id}", jar));
        primary1AfterRestoration.IsEntryLevel.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_ChangingStatus_WithoutDeactivatePrivilege_Returns403()
    {
        RequireDatabase();

        // level.update only — NOT level.deactivate.
        var jar = await SignInWithGrantsAsync(Privileges.Level.Update, Privileges.Level.View);
        var nursery1Id = (await FindSeededAsync("Nursery 1", "Nursery 2")).FirstId;

        var response = await PatchAsync($"{LevelsUrl}/{nursery1Id}", jar, new { status = "Inactive" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reorder_RewritesProgressionOrderAndInfersAdjacency()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Update, Privileges.Level.View);

        var current = await ReadAsync<CursorPageDto>(await GetAsync($"{LevelsUrl}?pageSize=100", jar));
        var orderedIds = current.Items.Select(item => item.Id).ToArray();

        // Swap the first two (Nursery 1, Nursery 2).
        (orderedIds[0], orderedIds[1]) = (orderedIds[1], orderedIds[0]);

        var response = await PostAsync(LevelsUrl + "/reorder", jar, new ReorderLevelsCommand(orderedIds));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<LevelDto[]>(response);
        body[0].Name.ShouldBe("Nursery 2");
        body[0].ProgressionOrder.ShouldBe(1);
        body[0].NextLevelId.ShouldBe(orderedIds[1]);
        body[1].Name.ShouldBe("Nursery 1");
        body[1].ProgressionOrder.ShouldBe(2);
    }

    // The entry level (Nursery 1) has never been referenced by any other level's nextLevelId —
    // delete succeeds, and Nursery 2 automatically becomes the new entry.
    [Fact]
    public async Task Delete_TheUnreferencedEntryLevel_Succeeds()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Delete, Privileges.Level.View);
        var (nursery1Id, nursery2Id, _) = await FindSeededAsync("Nursery 1", "Nursery 2");

        var response = await DeleteAsync($"{LevelsUrl}/{nursery1Id}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var nursery2 = await ReadAsync<LevelDto>(await GetAsync($"{LevelsUrl}/{nursery2Id}", jar));
        nursery2.IsEntryLevel.ShouldBeTrue();
    }

    // Primary 1 is pointed at by Nursery 3 — delete is refused, naming the reference (the one
    // reference this codebase can check today; arm/enrolment/mapping/result are DEFERRED, TASK-0039+).
    [Fact]
    public async Task Delete_ALevelReferencedByAnotherLevelsNextLevelId_Returns409NamingIt()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Delete);
        var primary1Id = (await FindSeededAsync("Nursery 3", "Primary 1")).SecondId;

        var response = await DeleteAsync($"{LevelsUrl}/{primary1Id}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("level.referenced");
        (json.RootElement.GetProperty("detail").GetString() ?? string.Empty).ShouldContain("Nursery 3");
    }

    // TASK-0039: the arm branch DeleteLevelHandler adds in the same dispatch that creates the arm
    // table (STATE.md's own live trigger for this card).
    [Fact]
    public async Task Delete_ALevelWithAnArmUnderIt_Returns409NamingArms()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Delete);

        // Nursery 1: the entry level, unreferenced by any other level's nextLevelId (see
        // Delete_TheUnreferencedEntryLevel_Succeeds) — so the ONLY reason delete is refused here is
        // the arm this test seeds under it, not the other reference check.
        var nursery1Id = (await FindSeededAsync("Nursery 1", "Nursery 2")).FirstId;

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var session = SchoolManagement.Domain.Sessions.AcademicSession
                .Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25))
                .Value;
            var arm = Arm.Create(Guid.CreateVersion7(), nursery1Id, session.Id, "A", null, null).Value;
            context.Add(session);
            context.Add(arm);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await DeleteAsync($"{LevelsUrl}/{nursery1Id}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("level.referenced_by_arm");
    }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Delete);

        var response = await DeleteAsync($"{LevelsUrl}/{Guid.NewGuid()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Spec 6.4.2: uniqueness of name and of progression_order (across active levels) is enforced by a
    // DATABASE index, not only application validation — proven by reading the schema directly.
    [Fact]
    public async Task Uniqueness_IsEnforcedByADatabaseIndex_NotOnlyApplicationValidation()
    {
        RequireDatabase();

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var indexNames = await context.Database
            .SqlQueryRaw<string>("SELECT indexname FROM pg_indexes WHERE tablename = 'class_levels'")
            .ToListAsync(TestContext.Current.CancellationToken);

        indexNames.ShouldContain("ix_class_levels_name_key_unique");
        indexNames.ShouldContain("ix_class_levels_progression_order_active_unique");
    }

    // Acceptance criterion: renaming a level does not rewrite anything already published. No
    // publication surface exists for levels yet (that needs result sets — Phase 2/3), so the honest
    // proof available today is structural: a level rename never writes to the config-version ledger
    // (spec 6.2.9) at all — unlike a settings save, which always appends one row. There is nothing
    // for a rename to retroactively rewrite, and this test pins that fact rather than assuming it.
    [Fact]
    public async Task Rename_DoesNotWriteAConfigVersionRow()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Update);
        var primary1Id = (await FindSeededAsync("Nursery 3", "Primary 1")).SecondId;

        int countBefore;

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            countBefore = await context.ConfigVersions.CountAsync(TestContext.Current.CancellationToken);
        }

        var response = await PatchAsync($"{LevelsUrl}/{primary1Id}", jar, new { name = "Basic 1" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var countAfter = await context.ConfigVersions.CountAsync(TestContext.Current.CancellationToken);
            countAfter.ShouldBe(countBefore);
        }
    }

    private void RequireDatabase()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            Assert.Skip(_fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    private async Task<(Guid FirstId, Guid SecondId, Guid SectionId)> FindSeededAsync(string firstName, string secondName)
    {
        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var first = await context.ClassLevels.AsNoTracking().SingleAsync(
            level => level.Name == firstName, TestContext.Current.CancellationToken);
        var second = await context.ClassLevels.AsNoTracking().SingleAsync(
            level => level.Name == secondName, TestContext.Current.CancellationToken);

        return (first.Id, second.Id, first.SectionId);
    }

    private static async Task<Guid> IdOfAsync(ApplicationDbContext context, string name) =>
        (await context.ClassLevels.AsNoTracking().SingleAsync(
            level => level.Name == name, TestContext.Current.CancellationToken)).Id;

    /// <summary>
    /// Inserts a standalone, already-INACTIVE level directly through the domain factory and
    /// <c>SaveChangesAsync</c> — never through the guarded HTTP surface, since an inactive level is
    /// excluded from every chain rule and a real admin action would never construct one this way.
    /// </summary>
    private static async Task InsertStandaloneInactiveLevelAsync(ApplicationDbContext context)
    {
        var section = await context.Sections.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken);
        var level = ClassLevel.Create(Guid.CreateVersion7(), "Reserve Level", section.Id, 500, null).Value;
        level.Deactivate();

        context.Add(level);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
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

    private Task<HttpResponseMessage> DeleteAsync(string url, CookieJar jar) => DeleteAsyncCore(_client, url, jar);

    private static async Task<HttpResponseMessage> DeleteAsyncCore(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    /// <summary>Minimal shape for asserting on <c>GET /levels</c>'s cursor page.</summary>
    private sealed record CursorPageDto(IReadOnlyList<LevelDto> Items, string? NextCursor);
}
