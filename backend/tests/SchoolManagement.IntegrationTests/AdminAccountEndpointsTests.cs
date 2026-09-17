using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Auth.AdminAccounts;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0027's seven endpoints against the approved contract delta
/// (`.agent/decisions/2026-Q3-contract-deltas.md`, entry `TASK-0019/0027`, Part 2): the
/// last-active-Super-Admin invariant under a real row lock, 6.1.7 rule 4 with its audit event, the
/// self-edit carve-out, session revocation on every state change, and the idempotency/redaction
/// mechanism's first real caller.
/// </summary>
public sealed class AdminAccountEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string MeUrl = "/api/v1/auth/me";
    private const string AdminsUrl = "/api/v1/admins";

    [Fact]
    public async Task Create_AsSuperAdmin_HappyPath_Returns201WithTemporaryPasswordOnce()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await PostAsync(
            AdminsUrl,
            jar,
            new CreateAdminAccountCommand("Ngozi Adeyemi", $"ngozi-{Guid.NewGuid():N}@example.com", "08012345678"),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var body = await ReadAsync<CreateAdminAccountResponse>(response);
        body.Phone.ShouldBe("+2348012345678");
        body.Status.ShouldBe(AdminAccountStatus.Active);
        body.MustChangePassword.ShouldBeTrue();
        body.TemporaryPassword.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_WithoutAnIdempotencyKey_Returns400KeyMissing()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, AdminsUrl)
        {
            Content = JsonContent.Create(
                new CreateAdminAccountCommand("Ngozi Adeyemi", $"ngozi-{Guid.NewGuid():N}@example.com", "08012345678")),
        };
        jar.ApplyWithCsrf(request);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("idempotency.key_missing");
    }

    [Fact]
    public async Task Create_RetriedWithTheSameIdempotencyKey_CreatesExactlyOneAccount_AndRedactsTheStoredPassword()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var idempotencyKey = $"key-{Guid.NewGuid():N}";
        var email = $"ngozi-{Guid.NewGuid():N}@example.com";
        var command = new CreateAdminAccountCommand("Ngozi Adeyemi", email, "08012345678");

        var first = await PostAsync(AdminsUrl, jar, command, idempotencyKey);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        first.Headers.Contains("Idempotency-Replay").ShouldBeFalse();
        var firstBody = await ReadAsync<CreateAdminAccountResponse>(first);
        firstBody.TemporaryPassword.ShouldNotBeNullOrWhiteSpace();

        var replay = await PostAsync(AdminsUrl, jar, command, idempotencyKey);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        replay.Headers.TryGetValues("Idempotency-Replay", out var values).ShouldBeTrue();
        values!.ShouldContain("true");
        var replayBody = await ReadAsync<CreateAdminAccountResponse>(replay);

        // Orchestrator amendment A2: the replay's LIVE response also shows the redaction — a second
        // display would violate spec 6.1.9/6.1.14's "never displays it again".
        replayBody.TemporaryPassword.ShouldBeNull();
        replayBody.Id.ShouldBe(firstBody.Id);

        // Exactly one row — the retry did not duplicate the account (proven by counting rows, not by
        // asserting the response alone).
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var matching = await context.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM admin_accounts WHERE email = {email}")
                .SingleAsync(TestContext.Current.CancellationToken);
            matching.ShouldBe(1);
        }

        // The real proof per the acceptance criterion: read the STORED idempotency row back directly,
        // not just the second HTTP response (the same redaction code path produced either way).
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var storedJson = await context.IdempotencyRecords
                .AsNoTracking()
                .Where(record => record.KeyHash == HashKey(idempotencyKey))
                .Select(record => record.ResponseBodyJson)
                .SingleAsync(TestContext.Current.CancellationToken);

            storedJson.ShouldNotBeNull();
            using var storedDocument = JsonDocument.Parse(storedJson);
            storedDocument.RootElement.GetProperty("temporaryPassword").ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task Create_DuplicateEmailAgainstAnActiveAccount_Returns409()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        var first = await PostAsync(
            AdminsUrl, jar, new CreateAdminAccountCommand("First Person", email, "08012345678"), $"key-{Guid.NewGuid():N}");
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await PostAsync(
            AdminsUrl, jar, new CreateAdminAccountCommand("Second Person", email, "08023456789"), $"key-{Guid.NewGuid():N}");

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(second);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admin.email_taken");
    }

    [Fact]
    public async Task Create_OnAnEmailThatBelongsToADeactivatedAccount_Succeeds()
    {
        // Spec 6.1.3: uniqueness is enforced across ACTIVE and SUSPENDED accounts only —
        // `EmailExistsActiveOrSuspendedAsync` excludes Deactivated, so a deactivated account's email
        // must be reusable. Proven by actually deactivating one and creating a new account on its
        // email, not by reading the repository method's name.
        RequireDatabase();

        var superAdminJar = await SignInAsSuperAdminAsync();
        var email = $"reusable-{Guid.NewGuid():N}@example.com";
        var (deactivatedId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, email: email);

        await ChangeStatusDirectlyAsync(deactivatedId, AdminAccountStatus.Deactivated);

        var response = await PostAsync(
            AdminsUrl,
            superAdminJar,
            new CreateAdminAccountCommand("New Occupant", email, "08099999999"),
            $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadAsync<CreateAdminAccountResponse>(response);
        body.Email.ShouldBe(email);
    }

    [Fact]
    public async Task Create_WithoutThePrivilege_Returns403()
    {
        RequireDatabase();

        var (_, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var jar = await SignInAsync(email);

        var response = await PostAsync(
            AdminsUrl, jar, new CreateAdminAccountCommand("Somebody Else", $"x-{Guid.NewGuid():N}@example.com", "08012345678"),
            $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_DefaultExcludesDeactivated_ButStatusFilterIncludesThemExplicitly()
    {
        RequireDatabase();

        var superAdminJar = await SignInAsSuperAdminAsync();
        var (deactivatedId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, staffName: "Departed Teacher");

        await ChangeStatusDirectlyAsync(deactivatedId, AdminAccountStatus.Deactivated);

        var defaultList = await ReadAsync<CursorPage<AdminAccountSummaryDto>>(
            await GetAsync($"{AdminsUrl}", superAdminJar));
        defaultList.Items.ShouldNotContain(item => item.Id == deactivatedId.ToString());

        var explicitList = await ReadAsync<CursorPage<AdminAccountSummaryDto>>(
            await GetAsync($"{AdminsUrl}?status=Deactivated", superAdminJar));
        explicitList.Items.ShouldContain(item => item.Id == deactivatedId.ToString());
    }

    [Fact]
    public async Task Get_WhenTheAccountDoesNotExist_Returns404()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await GetAsync($"{AdminsUrl}/{Guid.CreateVersion7()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admin.not_found");
    }

    [Fact]
    public async Task Update_SelfEdit_StaffNameAndPhone_SucceedsWithoutAdminUpdate()
    {
        RequireDatabase();

        var (id, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, staffName: "Original Name");
        var jar = await SignInAsync(email);

        var response = await PatchAsync(
            $"{AdminsUrl}/{id}", jar, new UpdateAdminAccountCommand(id, "Updated Name", email, "08023456789", null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync<AdminAccountDetailDto>(response);
        updated.StaffName.ShouldBe("Updated Name");
        updated.Phone.ShouldBe("+2348023456789");
    }

    [Fact]
    public async Task Update_SelfEdit_AttemptingToChangeOwnEmail_Returns403()
    {
        RequireDatabase();

        var (id, email, phone) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var jar = await SignInAsync(email);

        var response = await PatchAsync(
            $"{AdminsUrl}/{id}",
            jar,
            new UpdateAdminAccountCommand(id, "Updated Name", $"new-{Guid.NewGuid():N}@example.com", phone, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admin.self_edit_restricted");
    }

    [Fact]
    public async Task Update_AnotherAccountWithoutAdminUpdate_Returns403()
    {
        RequireDatabase();

        var (targetId, _, targetPhone) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var (_, callerEmail, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var jar = await SignInAsync(callerEmail);

        var response = await PatchAsync(
            $"{AdminsUrl}/{targetId}",
            jar,
            new UpdateAdminAccountCommand(targetId, "Somebody Else's Name", "x@example.com", targetPhone, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admin.access_denied");
    }

    [Fact]
    public async Task Update_AsSuperAdmin_CanChangeAnotherAccountsEmail_ButNotToADuplicate()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var takenEmail = $"taken-{Guid.NewGuid():N}@example.com";
        await AdminAccountSeeder.SeedRegularAsync(Fixture, email: takenEmail);
        var (targetId, _, targetPhone) = await AdminAccountSeeder.SeedRegularAsync(Fixture);

        var response = await PatchAsync(
            $"{AdminsUrl}/{targetId}",
            jar,
            new UpdateAdminAccountCommand(targetId, "Renamed", takenEmail, targetPhone, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admin.email_taken");
    }

    [Fact]
    public async Task Update_RuleFour_ANonSuperAdminAttemptingToGrantItselfSuperAdmin_IsRejectedAndAudited()
    {
        RequireDatabase();

        var fakeAuditSink = new RecordingSystemAuditSink();

        await using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(fakeAuditSink);
        }));

        using var client = factory.CreateClient();
        var (id, email, phone) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var jar = new CookieJar();
        await GetAsync(client, CsrfUrl, jar);
        var signIn = await PostAsync(client, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await PatchAsync(
            client, $"{AdminsUrl}/{id}", jar, new UpdateAdminAccountCommand(id, "Self", email, phone, true));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admin.super_admin_grant_denied");

        fakeAuditSink.Records.ShouldContain(record =>
            record.Action == "admin.super_admin_grant_denied" && record.EntityId == id.ToString());
    }

    [Fact]
    public async Task Update_ClearingIsSuperAdminOnTheOnlyActiveSuperAdmin_Returns409()
    {
        RequireDatabase();

        var (id, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = await SignInAsync(email);
        var self = await ReadAsync<AdminAccountDetailDto>(await GetAsync($"{AdminsUrl}/{id}", jar));

        var response = await PatchAsync(
            $"{AdminsUrl}/{id}", jar, new UpdateAdminAccountCommand(id, self.StaffName, self.Email, "08012345678", false));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admin.last_active_super_admin");
    }

    [Fact]
    public async Task Update_GrantingSuperAdminToATargetWithALiveSession_RotatesItsSessionSoTheOldCookieIsRejected()
    {
        // Spec 9.1: session tokens rotate on privilege change. Granting is_super_admin (spec 6.1.7) is
        // the first privilege change that ever happens to an account OTHER than the acting caller's
        // own, so it is the only place `UpdateAdminAccountHandler.cs`'s rotation branch is reachable at
        // all — both existing isSuperAdmin tests are REJECTION paths that never execute it. Proven on
        // OBSERVABLE behaviour: the target's own pre-existing cookie stops authenticating afterward,
        // not on the handler having been called.
        RequireDatabase();

        var superAdminJar = await SignInAsSuperAdminAsync();
        var (targetId, targetEmail, targetPhone) = await AdminAccountSeeder.SeedRegularAsync(
            Fixture, staffName: "Target Person");

        var targetJar = new CookieJar();
        await GetAsync(targetJar);
        var targetSignIn = await PostAsync(SignInUrl, targetJar, new SignInCommand(targetEmail, AdminAccountSeeder.Password));
        targetSignIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var meBefore = await GetAsync(MeUrl, targetJar);
        meBefore.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await PatchAsync(
            $"{AdminsUrl}/{targetId}",
            superAdminJar,
            new UpdateAdminAccountCommand(targetId, "Target Person", targetEmail, targetPhone, true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync<AdminAccountDetailDto>(response);
        updated.IsSuperAdmin.ShouldBeTrue();

        // The rotation's whole observable effect: the target's OLD cookie, still held by targetJar and
        // untouched by this test, no longer matches any session row.
        var meAfter = await GetAsync(MeUrl, targetJar);
        meAfter.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ClearingSuperAdminFromATargetWithALiveSession_RotatesItsSessionSoTheOldCookieIsRejected()
    {
        // The GRANT direction above proves rotation fires; this proves the CLEAR direction does too,
        // using a second active Super Admin so the invariant in spec 4.1 is never in play (that path is
        // already covered by Update_ClearingIsSuperAdminOnTheOnlyActiveSuperAdmin_Returns409, which is a
        // rejection and never reaches the rotation line either).
        RequireDatabase();

        var superAdminJar = await SignInAsSuperAdminAsync();
        var (targetId, targetEmail) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        var targetJar = new CookieJar();
        await GetAsync(targetJar);
        var targetSignIn = await PostAsync(SignInUrl, targetJar, new SignInCommand(targetEmail, AdminAccountSeeder.Password));
        targetSignIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var meBefore = await GetAsync(MeUrl, targetJar);
        meBefore.StatusCode.ShouldBe(HttpStatusCode.OK);

        var self = await ReadAsync<AdminAccountDetailDto>(await GetAsync($"{AdminsUrl}/{targetId}", superAdminJar));

        var response = await PatchAsync(
            $"{AdminsUrl}/{targetId}",
            superAdminJar,
            new UpdateAdminAccountCommand(targetId, self.StaffName, self.Email, "08012345678", false));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync<AdminAccountDetailDto>(response);
        updated.IsSuperAdmin.ShouldBeFalse();

        var meAfter = await GetAsync(MeUrl, targetJar);
        meAfter.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeStatus_ACallerChangingItsOwnStatus_Returns403()
    {
        RequireDatabase();

        var (id, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = await SignInAsync(email);

        var response = await PostAsync(
            $"{AdminsUrl}/{id}/status", jar, new { status = "Suspended", reason = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admin.self_status_change_forbidden");
    }

    [Fact]
    public async Task ChangeStatus_Deactivate_WithoutAReason_Returns422()
    {
        RequireDatabase();

        var superAdminJar = await SignInAsSuperAdminAsync();
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/status", superAdminJar, new { status = "Deactivated", reason = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ChangeStatus_Suspend_RevokesTheTargetsSessionsImmediately()
    {
        RequireDatabase();

        var superAdminJar = await SignInAsSuperAdminAsync();
        var (targetId, targetEmail, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);

        var targetJar = new CookieJar();
        await GetAsync(targetJar);
        var targetSignIn = await PostAsync(SignInUrl, targetJar, new SignInCommand(targetEmail, AdminAccountSeeder.Password));
        targetSignIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Give the target's own account SOME privilege to prove with — its OWN admin.view grant
        // would need role assignment (TASK-0028), so instead just prove it was authenticated at all:
        // GET /auth/me succeeds before suspension.
        var meBefore = await GetAsync(MeUrl, targetJar);
        meBefore.StatusCode.ShouldBe(HttpStatusCode.OK);

        var suspend = await PostAsync(
            $"{AdminsUrl}/{targetId}/status", superAdminJar, new { status = "Suspended", reason = (string?)null });
        suspend.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Spec 6.1.13: "The next request from that browser returns 401."
        var meAfter = await GetAsync(MeUrl, targetJar);
        meAfter.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeStatus_Deactivate_RevokesTheTargetsSessionsImmediately()
    {
        RequireDatabase();

        var superAdminJar = await SignInAsSuperAdminAsync();
        var (targetId, targetEmail, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);

        var targetJar = new CookieJar();
        await GetAsync(targetJar);
        var targetSignIn = await PostAsync(SignInUrl, targetJar, new SignInCommand(targetEmail, AdminAccountSeeder.Password));
        targetSignIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deactivate = await PostAsync(
            $"{AdminsUrl}/{targetId}/status",
            superAdminJar,
            new { status = "Deactivated", reason = "The teacher has left the school." });
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);

        var meAfter = await GetAsync(MeUrl, targetJar);
        meAfter.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeStatus_TwoSuperAdminsSuspendEachOtherConcurrently_OnlyOneSucceeds()
    {
        // Spec 6.1.13: "Two Super Admins suspend each other in the same minute: the second operation
        // fails the last-active-Super-Admin check... The check runs inside the transaction with a row
        // lock on the account table, not as a pre-flight read." Proven under REAL concurrency, not
        // sequenced calls — both requests are started before either is awaited.
        RequireDatabase();

        var (idA, emailA) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var (idB, emailB) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        var jarA = await SignInAsync(emailA);
        var jarB = await SignInAsync(emailB);

        var taskA = PostAsync(
            $"{AdminsUrl}/{idB}/status", jarA, new { status = "Suspended", reason = (string?)null });
        var taskB = PostAsync(
            $"{AdminsUrl}/{idA}/status", jarB, new { status = "Suspended", reason = (string?)null });

        var results = await Task.WhenAll(taskA, taskB);

        var statusCodes = results.Select(response => response.StatusCode).OrderBy(code => code).ToArray();
        statusCodes.ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict]);

        var conflictResponse = results.Single(response => response.StatusCode == HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(conflictResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admin.last_active_super_admin");
    }

    [Fact]
    public async Task ResetPassword_HappyPath_RevokesSessionsAndReturnsTheTemporaryPasswordOnce()
    {
        RequireDatabase();

        var superAdminJar = await SignInAsSuperAdminAsync();
        var (targetId, targetEmail, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);

        var targetJar = new CookieJar();
        await GetAsync(targetJar);
        var targetSignIn = await PostAsync(SignInUrl, targetJar, new SignInCommand(targetEmail, AdminAccountSeeder.Password));
        targetSignIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/password-reset", superAdminJar, new { }, $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ResetAdminAccountPasswordResponse>(response);
        body.TemporaryPassword.ShouldNotBeNullOrWhiteSpace();

        var meAfter = await GetAsync(MeUrl, targetJar);
        meAfter.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeSessions_HappyPath_Returns204_AndTheOldSessionIsRejectedAfterward()
    {
        RequireDatabase();

        var superAdminJar = await SignInAsSuperAdminAsync();
        var (targetId, targetEmail, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);

        var targetJar = new CookieJar();
        await GetAsync(targetJar);
        var targetSignIn = await PostAsync(SignInUrl, targetJar, new SignInCommand(targetEmail, AdminAccountSeeder.Password));
        targetSignIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await DeleteAsync($"{AdminsUrl}/{targetId}/sessions", superAdminJar);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var meAfter = await GetAsync(MeUrl, targetJar);
        meAfter.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task ChangeStatusDirectlyAsync(Guid accountId, AdminAccountStatus status)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE admin_accounts SET status = {status.ToString()} WHERE id = {accountId}",
            TestContext.Current.CancellationToken);
    }

    private static string HashKey(string rawKey)
    {
        var digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexStringLower(digest);
    }

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        return await SignInAsync(email);
    }

    private async Task<CookieJar> SignInAsync(string email)
    {
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> GetAsync(string url, CookieJar jar) => GetAsync(Client, url, jar);

    private Task<HttpResponseMessage> GetAsync(CookieJar jar) => GetAsync(Client, CsrfUrl, jar);

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PostAsync<T>(
        string url, CookieJar jar, T payload, string? idempotencyKey = null) =>
        PostAsync(Client, url, jar, payload, idempotencyKey);

    private static async Task<HttpResponseMessage> PostAsync<T>(
        HttpClient client, string url, CookieJar jar, T payload, string? idempotencyKey = null)
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
        PatchAsync(Client, url, jar, payload);

    private static async Task<HttpResponseMessage> PatchAsync<T>(HttpClient client, string url, CookieJar jar, T payload)
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

    private Task<HttpResponseMessage> DeleteAsync(string url, CookieJar jar)
    {
        return DeleteAsyncCore(Client, url, jar);
    }

    private static async Task<HttpResponseMessage> DeleteAsyncCore(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}

/// <summary>
/// Records every call for the various rule/rejection audit-event proofs across this assembly —
/// used wherever a test needs to assert WHICH action code was recorded without depending on real
/// persistence (TASK-0048's own Postgres-backed durability proofs live alongside the real
/// endpoints instead — see <c>AssignmentEndpointsTests</c>).
/// </summary>
internal sealed class RecordingSystemAuditSink : ISystemAuditSink
{
    public List<(string Action, string? EntityType, string? EntityId, string? ActorAdminId, AuditOutcome Outcome,
        IReadOnlyDictionary<string, object?>? Metadata, IReadOnlyDictionary<string, object?>? BeforeMetadata)> Records
    { get; } = [];

    public Task RecordAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null,
        IReadOnlyDictionary<string, object?>? beforeMetadata = null)
    {
        Records.Add((action, entityType, entityId, actorAdminId, AuditOutcome.Success, metadata, beforeMetadata));
        return Task.CompletedTask;
    }

    public Task RecordRejectionAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        Records.Add((action, entityType, entityId, actorAdminId, AuditOutcome.Rejected, metadata, null));
        return Task.CompletedTask;
    }
}
