using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0050's two named criteria: the pending-exclusion invariant (spec 6.5.14)
/// enforced as a structural default rather than a per-caller filter, and arm-scoped <c>pupil.view</c>/
/// <c>pupil.update</c> proven against the REAL, DI-registered
/// <c>RoleAssignmentEffectivePrivilegeProvider</c> — never a fake, the same discipline
/// <c>AssignmentEndpointsTests</c> established. Plus the entity's own read/write surface: create,
/// list, get, biographical update, the 409 on a <c>registrationNumber</c> in the payload, the
/// admissions queue, duplicate detection and search.
/// </summary>
public sealed class PupilEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string PupilsUrl = "/api/v1/pupils";
    private const string AdmissionsUrl = "/api/v1/admissions";

    private static readonly DateOnly DefaultDateOfBirth = new(2020, 5, 3);

    [Fact]
    public async Task Create_HappyPath_Returns201WithPendingStatusAndNullRegistrationNumber()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);

        var response = await PostAsync(PupilsUrl, jar, CreateCommand("Okafor", "Chidera"), $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadAsync<PupilDto>(response);
        body.Status.ShouldBe(PupilStatus.Pending);
        body.RegistrationNumber.ShouldBeNull();
        body.Surname.ShouldBe("Okafor");
    }

    [Fact]
    public async Task Create_WithAnOutOfRangeDateOfBirth_Returns422WithTheVerbatimMessageShape()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var command = CreateCommand("Okafor", "Chidera") with { DateOfBirth = new DateOnly(2025, 1, 1) };

        var response = await PostAsync(PupilsUrl, jar, command, $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.date_of_birth_out_of_range");
        var detail = document.RootElement.GetProperty("detail").GetString();
        detail.ShouldNotBeNull();
        detail.ShouldContain("makes this pupil");
        detail.ShouldContain("Check the date.");
    }

    [Fact]
    public async Task Create_WithFreeTextStateOfOrigin_Returns422()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var command = CreateCommand("Okafor", "Chidera") with { StateOfOrigin = "Not A Real State" };

        var response = await PostAsync(PupilsUrl, jar, command, $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.state_of_origin_invalid");
    }

    [Fact]
    public async Task Create_WithoutSignIn_Returns401()
    {
        RequireDatabase();

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);

        var response = await PostAsync(PupilsUrl, jar, CreateCommand("Okafor", "Chidera"), $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // The card's own named criterion: "GET /pupils without status returns no pending record even when
    // one matches every other filter, and GET /admissions returns it."
    [Fact]
    public async Task List_WithoutStatus_ExcludesAPendingPupil_ButAdmissionsQueueShowsIt()
    {
        RequireDatabase();

        var surname = "Excludedsurname";
        var pupilId = await SeedPupilDirectlyAsync(surname, "Chidera");

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var listResponse = await GetAsync($"{PupilsUrl}?search={surname}", jar);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var listPage = await ReadAsync<CursorPage<PupilDto>>(listResponse);
        listPage.Items.ShouldNotContain(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));

        var pendingResponse = await GetAsync($"{PupilsUrl}?status=Pending&search={surname}", jar);
        pendingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var pendingPage = await ReadAsync<CursorPage<PupilDto>>(pendingResponse);
        pendingPage.Items.ShouldContain(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));

        var admissionsResponse = await GetAsync(AdmissionsUrl, jar);
        admissionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var admissionsPage = await ReadAsync<CursorPage<PupilDto>>(admissionsResponse);
        admissionsPage.Items.ShouldContain(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Get_APendingPupilById_IsReachable_UnlikeTheList()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Reachable", "ById");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{PupilsUrl}/{pupilId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilDto>(response);
        body.Status.ShouldBe(PupilStatus.Pending);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{PupilsUrl}/{Guid.CreateVersion7()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // The other named criterion: arm-scoped pupil.view proven BOTH directions against the REAL
    // RoleAssignmentEffectivePrivilegeProvider — a class-teacher-shaped arm-scoped grant sees nothing
    // (no pupil carries an arm reference yet, see PupilAccessGuard's remarks), a school-wide grant
    // sees the record.
    [Fact]
    public async Task List_ArmScopedCaller_SeesAnEmptyPage_SchoolWideCallerSeesTheRecord()
    {
        RequireDatabase();

        var surname = "Scopeprobesurname";
        await SeedPupilDirectlyAsync(surname, "Chidera");

        var sessionId = await SeedSessionAsync();
        var armId = await SeedArmAsync(sessionId, "1A");

        var armScopedJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.ArmList, sessionId, [armId]);
        var schoolWideJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var armScopedResponse = await GetAsync($"{PupilsUrl}?status=Pending&search={surname}", armScopedJar);
        armScopedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var armScopedPage = await ReadAsync<CursorPage<PupilDto>>(armScopedResponse);
        armScopedPage.Items.ShouldBeEmpty();

        var schoolWideResponse = await GetAsync($"{PupilsUrl}?status=Pending&search={surname}", schoolWideJar);
        schoolWideResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var schoolWidePage = await ReadAsync<CursorPage<PupilDto>>(schoolWideResponse);
        schoolWidePage.Items.ShouldContain(item => item.Surname == surname);
    }

    [Fact]
    public async Task Get_ArmScopedCaller_Returns403_SchoolWideCallerReturns200()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Scoped", "Target");

        var sessionId = await SeedSessionAsync();
        var armId = await SeedArmAsync(sessionId, "1B");

        var armScopedJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.ArmList, sessionId, [armId]);
        var schoolWideJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var armScopedResponse = await GetAsync($"{PupilsUrl}/{pupilId}", armScopedJar);
        armScopedResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var schoolWideResponse = await GetAsync($"{PupilsUrl}/{pupilId}", schoolWideJar);
        schoolWideResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_WithNoPupilViewGrantAtAll_Returns403()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Admin.View], ScopeType.SchoolWide);

        var response = await GetAsync(PupilsUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_SettingRegistrationNumber_Returns409()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Immutable", "Number");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.Update], ScopeType.SchoolWide);

        var response = await PatchAsync(
            $"{PupilsUrl}/{pupilId}",
            jar,
            new
            {
                registrationNumber = "GRAS/2026/0099",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.registration_number_immutable");
    }

    [Fact]
    public async Task Update_HappyPath_ChangesHomeAddressAndIsAudited()
    {
        RequireDatabase();

        var auditSink = new RecordingSystemAuditSink();
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(auditSink);
        }));
        using var client = factory.CreateClient();

        var pupilId = await SeedPupilDirectlyAsync("Editable", "Pupil");
        var jar = await SignInWithGrantAsync(client, [Privileges.Pupil.Update], ScopeType.SchoolWide);

        var response = await PatchAsync(client, $"{PupilsUrl}/{pupilId}", jar, new { homeAddress = "9 New Road" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilDto>(response);
        body.HomeAddress.ShouldBe("9 New Road");

        auditSink.Records.ShouldContain(record =>
            record.Action == Privileges.Pupil.Update &&
            record.EntityId == pupilId.ToString("D", CultureInfo.InvariantCulture));
    }

    // 6.5.3: "Ordinary pupil reads are NOT audited" — the card's own explicit instruction.
    [Fact]
    public async Task List_And_Get_AreNotAudited()
    {
        RequireDatabase();

        var auditSink = new RecordingSystemAuditSink();
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(auditSink);
        }));
        using var client = factory.CreateClient();

        var pupilId = await SeedPupilDirectlyAsync("Unaudited", "Read");
        var jar = await SignInWithGrantAsync(client, [Privileges.Pupil.View], ScopeType.SchoolWide);

        await GetAsync(client, PupilsUrl, jar);
        await GetAsync(client, $"{PupilsUrl}/{pupilId}", jar);
        await GetAsync(client, AdmissionsUrl, jar);

        // Filtered to this card's own entity type — a background system job (idempotency-record
        // purge) may legitimately write its own, unrelated audit event during the same test run.
        auditSink.Records.ShouldNotContain(record => record.EntityType == "pupil");
    }

    // Spec 6.5.15's own worked example: typing the serial alone finds the full registration number.
    // The number is seeded directly (no issuance endpoint exists in this card — TASK-0051) purely to
    // prove the search MECHANISM, and the pupil is made ACTIVE so the ordinary list (no status filter)
    // finds it without needing the pending opt-out too.
    [Fact]
    public async Task Search_BySerialAlone_FindsTheFullRegistrationNumber()
    {
        RequireDatabase();

        var surname = "Serialsurname";
        var pupilId = await SeedPupilDirectlyAsync(surname, "Chidera");
        await SetRegistrationNumberAndStatusAsync(pupilId, "GRAS/2026/0041", PupilStatus.Active);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{PupilsUrl}?search=41", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<CursorPage<PupilDto>>(response);
        var match = page.Items.Single(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));
        match.MatchedField.ShouldBe("RegistrationNumber");
    }

    [Fact]
    public async Task Duplicates_MatchesBySurnameFirstNameAndDateOfBirth_IncludingAPendingRecord()
    {
        RequireDatabase();

        var surname = "Duplicatesurname";
        var pupilId = await SeedPupilDirectlyAsync(surname, "Chidera", DefaultDateOfBirth);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);

        var response = await GetAsync(
            $"{PupilsUrl}/duplicates?surname={surname}&firstName=Chidera&dateOfBirth={DefaultDateOfBirth:yyyy-MM-dd}",
            jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var candidates = await ReadAsync<List<PupilDto>>(response);
        candidates.ShouldContain(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));
    }

    private static CreatePupilCommand CreateCommand(string surname, string firstName) => new(
        surname,
        firstName,
        MiddleName: null,
        PupilSex.Female,
        DefaultDateOfBirth,
        Nationality: null,
        StateOfOrigin: "Anambra",
        Lga: "Awka South",
        HomeAddress: "14 Zik Avenue, Awka",
        PreviousSchool: null,
        PreviousClass: null,
        OtherInformation: null);

    private async Task<Guid> SeedPupilDirectlyAsync(
        string surname, string firstName, DateOnly? dateOfBirth = null)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Pupil.Create(
            Guid.CreateVersion7(),
            surname,
            firstName,
            middleName: null,
            PupilSex.Female,
            dateOfBirth ?? DefaultDateOfBirth,
            asOfDate: new DateOnly(2026, 9, 9),
            nationality: null,
            "Anambra",
            "Awka South",
            "14 Zik Avenue, Awka",
            previousSchool: null,
            previousClass: null,
            otherInformation: null);

        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        context.Add(creation.Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return creation.Value.Id;
    }

    /// <summary>
    /// Sets fields <see cref="Pupil"/> exposes no ordinary write path for — TEST SETUP standing in for
    /// TASK-0051's not-yet-built issuance, the same accepted direct-DbContext technique
    /// <c>AssignmentEndpointsTests.SeedAssignmentAsync</c> uses for its own otherwise-unreachable state.
    /// </summary>
    private async Task SetRegistrationNumberAndStatusAsync(Guid pupilId, string registrationNumber, PupilStatus status)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET registration_number = {registrationNumber}, status = {status.ToString()} WHERE id = {pupilId}",
            TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedRoleAsync(string name, IReadOnlyCollection<string> privileges)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Role.Create(Guid.CreateVersion7(), name, null, privileges);
        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        context.Add(creation.Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return creation.Value.Id;
    }

    private static int _nextSessionStartYear = 2000;

    /// <summary>Seeds a session with a fresh, valid <c>YYYY/YYYY</c> name — a distinct start year each call, so several calls within one test never collide.</summary>
    private async Task<Guid> SeedSessionAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var name = $"{startYear}/{startYear + 1}";
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), name, new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;

        context.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session.Id;
    }

    private async Task<Guid> SeedArmAsync(Guid sessionId, string label)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(TestContext.Current.CancellationToken)).Id;

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, sessionId, label, null, null).Value;

        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return arm.Id;
    }

    /// <summary>
    /// Seeds a regular admin account, a role carrying exactly <paramref name="privileges"/>, and an
    /// ACTIVE <c>role_assignment</c> row — seeded directly through the DbContext (the same accepted
    /// technique <c>AssignmentEndpointsTests</c> uses), so the caller's grants are resolved by the
    /// REAL <c>RoleAssignmentEffectivePrivilegeProvider</c> from genuine database rows, never a fake.
    /// Signs in as that account and returns its cookie jar.
    /// </summary>
    private Task<CookieJar> SignInWithGrantAsync(
        IReadOnlyCollection<string> privileges,
        ScopeType scopeType,
        Guid? sessionId = null,
        IReadOnlyCollection<Guid>? armIds = null) =>
        SignInWithGrantAsync(Client, privileges, scopeType, sessionId, armIds);

    private async Task<CookieJar> SignInWithGrantAsync(
        HttpClient client,
        IReadOnlyCollection<string> privileges,
        ScopeType scopeType,
        Guid? sessionId = null,
        IReadOnlyCollection<Guid>? armIds = null)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync($"Role-{Guid.NewGuid():N}", privileges);
        var resolvedSessionId = sessionId ?? await SeedSessionAsync();

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var creation = Domain.Security.RoleAssignment.Create(
                Guid.CreateVersion7(), accountId, roleId, resolvedSessionId, scopeType, armIds ?? [], accountId);
            creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

            context.Add(creation.Value);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var jar = new CookieJar();
        await GetAsync(client, CsrfUrl, jar);
        var signIn = await PostAsync(client, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
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

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload, string? idempotencyKey = null) =>
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
}
