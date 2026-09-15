using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0063: registration-number correction and the permanent history alias (spec 6.5.10,
/// "Immutability and correction"). Every test seeds an already-active pupil holding a number
/// directly through the DbContext — no earlier endpoint in this system can produce that state, the
/// same accepted technique <c>AdmissionApprovalEndpointsTests</c> already uses.
/// </summary>
public sealed class PupilRegistrationNumberCorrectionEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string PupilsUrl = "/api/v1/pupils";

    private static readonly DateOnly DefaultDateOfBirth = new(2020, 5, 3);

    [Fact]
    public async Task Correct_HappyPath_UpdatesTheNumberAndWritesAnAuditedHistoryRow()
    {
        RequireDatabase();

        var pupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);

        var response = await CorrectAsync(
            pupilId, jar, "GRAS/2026/0041", "Wrong admission year was entered at approval.", key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilDto>(response);
        body.RegistrationNumber.ShouldBe("GRAS/2026/0041");

        var history = await GetHistoryRowsAsync(pupilId);
        history.ShouldHaveSingleItem();
        history[0].OldRegistrationNumber.ShouldBe("GRAS/2025/0041");
        history[0].Reason.ShouldBe("Wrong admission year was entered at approval.");
        history[0].CorrectedBy.ShouldNotBeNull();
    }

    // The endpoint requires Idempotency-Key (spec 6.5.10's history write is a standing obligation
    // per CLAUDE.md §8/rules/wire.md — a retry-duplicable mutation that is not naturally idempotent:
    // a replayed correction with the SAME key must not append a SECOND history row for the same
    // event, the same proof AdmissionApprovalEndpointsTests already gives its own route-specific
    // side effect (the counter) on top of the generic substrate TASK-0019 proved once.
    [Fact]
    public async Task Correct_IdempotencyKeyReplay_ReturnsTheFirstResponseAndWritesTheHistoryRowExactlyOnce()
    {
        RequireDatabase();

        var pupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);
        var key = $"key-{Guid.NewGuid():N}";

        var first = await CorrectAsync(pupilId, jar, "GRAS/2026/0041", "Wrong admission year was entered at approval.", key);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await ReadAsync<PupilDto>(first);

        var second = await CorrectAsync(pupilId, jar, "GRAS/2026/0041", "Wrong admission year was entered at approval.", key);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.Headers.TryGetValues("Idempotency-Replay", out var replayHeader).ShouldBeTrue();
        replayHeader!.ShouldContain("true");
        var secondBody = await ReadAsync<PupilDto>(second);

        secondBody.RegistrationNumber.ShouldBe(firstBody.RegistrationNumber);

        var history = await GetHistoryRowsAsync(pupilId);
        history.ShouldHaveSingleItem();
        history[0].OldRegistrationNumber.ShouldBe("GRAS/2025/0041");
    }

    [Fact]
    public async Task Correct_TwiceInARow_BothOldNumbersRemainAsPermanentAliases()
    {
        RequireDatabase();

        var pupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);

        var first = await CorrectAsync(
            pupilId, jar, "GRAS/2026/0041", "Wrong admission year was entered at approval.", key: $"key-{Guid.NewGuid():N}");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await CorrectAsync(
            pupilId, jar, "GRAS/2026/0099", "Serial was also transposed on re-entry.", key: $"key-{Guid.NewGuid():N}");
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var history = await GetHistoryRowsAsync(pupilId);
        history.Count.ShouldBe(2);
        history.ShouldContain(row => row.OldRegistrationNumber == "GRAS/2025/0041");
        history.ShouldContain(row => row.OldRegistrationNumber == "GRAS/2026/0041");

        var current = await GetPupilRegistrationNumberAsync(pupilId);
        current.ShouldBe("GRAS/2026/0099");
    }

    [Fact]
    public async Task Correct_TheCounterIsNotTouched()
    {
        RequireDatabase();

        await SeedCounterAsync("2026", 41);
        var pupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);

        var response = await CorrectAsync(
            pupilId, jar, "GRAS/2026/0041", "Wrong admission year was entered at approval.", key: $"key-{Guid.NewGuid():N}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var lastSerial = await GetLastSerialAsync("2026");
        lastSerial.ShouldBe(41);
    }

    [Fact]
    public async Task Correct_NewNumberAlreadyLiveOnAnotherPupil_Returns409()
    {
        RequireDatabase();

        var holderId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2026/0099");
        var pupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);

        var response = await CorrectAsync(
            pupilId, jar, "GRAS/2026/0099", "Wrong admission year was entered at approval.", key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.registration_number_duplicate");

        // Untouched — the failed attempt did not consume the holder's own number nor this pupil's.
        (await GetPupilRegistrationNumberAsync(holderId)).ShouldBe("GRAS/2026/0099");
        (await GetPupilRegistrationNumberAsync(pupilId)).ShouldBe("GRAS/2025/0041");
    }

    [Fact]
    public async Task Correct_NewNumberAlreadyAliasedInHistory_Returns409()
    {
        RequireDatabase();

        // A first pupil's OWN earlier correction leaves "GRAS/2020/0001" as a historical alias.
        var firstPupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2020/0001");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);
        var priorCorrection = await CorrectAsync(
            firstPupilId, jar, "GRAS/2020/0002", "First correction, unrelated to this test's assertion.", key: $"key-{Guid.NewGuid():N}");
        priorCorrection.StatusCode.ShouldBe(HttpStatusCode.OK);

        // A second, unrelated pupil now tries to correct ONTO that aliased (never live) number.
        var secondPupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");
        var response = await CorrectAsync(
            secondPupilId, jar, "GRAS/2020/0001", "Attempting to reuse a retired alias.", key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.registration_number_duplicate");
    }

    [Fact]
    public async Task Correct_ReasonUnderTenCharacters_Returns422()
    {
        RequireDatabase();

        var pupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);

        var response = await CorrectAsync(
            pupilId, jar, "GRAS/2026/0041", "too short", key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await GetPupilRegistrationNumberAsync(pupilId)).ShouldBe("GRAS/2025/0041");
    }

    [Fact]
    public async Task Correct_MalformedRegistrationNumber_Returns422()
    {
        RequireDatabase();

        var pupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);

        var response = await CorrectAsync(
            pupilId, jar, "not-a-number", "Wrong admission year was entered at approval.", key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Correct_PupilStillPendingWithNoNumberYet_Returns409()
    {
        RequireDatabase();

        var pupilId = await SeedPendingPupilAsync("Pending", "Nonumber");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);

        var response = await CorrectAsync(
            pupilId, jar, "GRAS/2026/0041", "Wrong admission year was entered at approval.", key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.registration_number_not_issued");
    }

    [Fact]
    public async Task Correct_UnknownPupil_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.RegNumberCorrect]);

        var response = await CorrectAsync(
            Guid.CreateVersion7(), jar, "GRAS/2026/0041", "Wrong admission year was entered at approval.", key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Correct_CallerWithoutThePrivilege_Returns403()
    {
        RequireDatabase();

        var pupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");
        // pupil.update is a real, seeded privilege that is NOT pupil.regnumber.correct — a caller
        // holding ordinary pupil-management access still may not correct a number (spec 6.5.10:
        // "Super Admin only").
        var jar = await SignInWithGrantAsync([Privileges.Pupil.Update]);

        var response = await CorrectAsync(
            pupilId, jar, "GRAS/2026/0041", "Wrong admission year was entered at approval.", key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetPupilRegistrationNumberAsync(pupilId)).ShouldBe("GRAS/2025/0041");
    }

    [Fact]
    public async Task Correct_Unauthenticated_Returns401()
    {
        RequireDatabase();

        var pupilId = await SeedPupilHoldingRegistrationNumberAsync("GRAS/2025/0041");

        var command = new CorrectRegistrationNumberCommand(Guid.Empty, "GRAS/2026/0041", "Wrong admission year was entered at approval.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{PupilsUrl}/{pupilId}/registration-number")
        {
            Content = JsonContent.Create(command),
        };
        request.Headers.Add("Idempotency-Key", $"key-{Guid.NewGuid():N}");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- seeding ----------------------------------------------------------------------------------

    private async Task<Guid> SeedPupilHoldingRegistrationNumberAsync(string registrationNumber)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), "Holder", "OfANumber", middleName: null, PupilSex.Female, DefaultDateOfBirth,
            asOfDate: new DateOnly(2026, 1, 1), nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;

        context.Add(pupil);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET registration_number = {registrationNumber}, status = 'Active' WHERE id = {pupil.Id}",
            TestContext.Current.CancellationToken);

        return pupil.Id;
    }

    private async Task<Guid> SeedPendingPupilAsync(string surname, string firstName)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), surname, firstName, middleName: null, PupilSex.Female, DefaultDateOfBirth,
            asOfDate: new DateOnly(2026, 1, 1), nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;

        context.Add(pupil);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return pupil.Id;
    }

    private async Task SeedCounterAsync(string counterKey, int lastSerial)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO registration_counter (counter_key, last_serial) VALUES ({counterKey}, {lastSerial})
            ON CONFLICT (counter_key) DO UPDATE SET last_serial = {lastSerial}
            """,
            TestContext.Current.CancellationToken);
    }

    private async Task<int> GetLastSerialAsync(string counterKey)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.RegistrationCounters.AsNoTracking()
            .Where(counter => counter.Id == counterKey)
            .Select(counter => (int?)counter.LastSerial)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken) ?? 0;
    }

    private async Task<string?> GetPupilRegistrationNumberAsync(Guid pupilId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Pupils.IgnoreQueryFilters().AsNoTracking()
            .Where(pupil => pupil.Id == pupilId)
            .Select(pupil => pupil.RegistrationNumber)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<PupilRegNumberHistory>> GetHistoryRowsAsync(Guid pupilId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.PupilRegNumberHistory.AsNoTracking()
            .Where(row => row.PupilId == pupilId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedRoleAsync(string name, IReadOnlyCollection<string> privileges)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var role = Role.Create(Guid.CreateVersion7(), name, null, privileges).Value;

        context.Add(role);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return role.Id;
    }

    private async Task<Guid> SeedSessionAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var session = AcademicSession.Create(
            Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;

        context.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session.Id;
    }

    private async Task<CookieJar> SignInWithGrantAsync(IReadOnlyCollection<string> privileges)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync($"Role-{Guid.NewGuid():N}", privileges);
        var sessionId = await SeedSessionAsync();

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var assignment = RoleAssignment.Create(
                Guid.CreateVersion7(), accountId, roleId, sessionId, ScopeType.SchoolWide, [], accountId).Value;

            context.Add(assignment);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    // ---- HTTP helpers ----------------------------------------------------------------------------

    private Task<HttpResponseMessage> CorrectAsync(
        Guid pupilId, CookieJar jar, string registrationNumber, string reason, string key)
    {
        var command = new CorrectRegistrationNumberCommand(Guid.Empty, registrationNumber, reason);

        return PostAsync($"{PupilsUrl}/{pupilId}/registration-number", jar, command, key);
    }

    private async Task<HttpResponseMessage> GetAsync(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload, string? idempotencyKey = null)
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

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

}
