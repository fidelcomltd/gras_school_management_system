using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Pupils.Movement;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Status changes and arm transfers (spec 6.5.14, 6.5.16, 6.5.17, 06 §6.4.4). The fixture runs on the real clock and
/// effective dates may not be in the future, so every session is seeded around today.
/// </summary>
public sealed class PupilStatusAndTransferEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    // The server's date, which is Lagos (UTC+1): the UTC date lags it by a day from 23:00 UTC.
    private static readonly DateOnly Today = Application.Weekly.WeeklyProjection.LagosToday(DateTimeOffset.UtcNow);
    private static readonly DateOnly SessionStart = Today.AddDays(-60);

    // ---- Transfer ------------------------------------------------------------------------------

    [Fact]
    public async Task Transfer_MovesTheEnrolmentAndSendsBothArmsResultSetsBackForRecompute()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var sourceSet = await SeedResultSetAsync(world.ArmA, world.TermId, ResultSetState.AwaitingApproval);
        var destinationSet = await SeedResultSetAsync(world.ArmB, world.TermId, ResultSetState.Draft);
        var jar = await SignInAsSuperAdminAsync();
        var effective = Today.AddDays(-5);

        var response = await PostAsync($"/api/v1/pupils/{pupilId}/transfer", jar, new { armId = world.ArmB.ToString(), effectiveDate = effective });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilMovementOutcomeDto>(response);
        body.DryRun.ShouldBeFalse();
        body.EnrolmentClosesOn.ShouldBe(effective.AddDays(-1));
        body.ResultSets.Single(set => set.ResultSetId == sourceSet.ToString()).Effect.ShouldBe(PupilMovementEffect.RevertsToDraft);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var enrolments = await context.Enrolments.AsNoTracking().Where(e => e.PupilId == pupilId).OrderBy(e => e.EffectiveFrom)
            .ToListAsync(TestContext.Current.CancellationToken);
        enrolments.Count.ShouldBe(2);
        enrolments[0].ArmId.ShouldBe(world.ArmA);
        enrolments[0].EffectiveTo.ShouldBe(effective.AddDays(-1)); // the day before (spec 6.4.4 step 3)
        enrolments[1].ArmId.ShouldBe(world.ArmB);
        enrolments[1].EffectiveFrom.ShouldBe(effective);
        enrolments[1].EffectiveTo.ShouldBeNull();

        var source = await context.ResultSets.AsNoTracking().SingleAsync(r => r.Id == sourceSet, TestContext.Current.CancellationToken);
        source.State.ShouldBe(ResultSetState.Draft);
        source.NeedsRecompute.ShouldBeTrue();
        source.ReturnReason.ShouldBe($"Cohort changed by pupil transfer on {effective:dd/MM/yyyy}.");

        var destination = await context.ResultSets.AsNoTracking().SingleAsync(r => r.Id == destinationSet, TestContext.Current.CancellationToken);
        destination.NeedsRecompute.ShouldBeTrue();
    }

    [Fact]
    public async Task Transfer_OutOfAnArmWithPublishedResults_IsRefused_AndTheDryRunSaysSoWithoutWriting()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        await SeedResultSetAsync(world.ArmA, world.TermId, ResultSetState.Published);
        var jar = await SignInAsSuperAdminAsync();
        var payload = new { armId = world.ArmB.ToString(), effectiveDate = Today, dryRun = true };

        var dryRun = await PostAsync($"/api/v1/pupils/{pupilId}/transfer", jar, payload);

        dryRun.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PupilMovementOutcomeDto>(dryRun)).ResultSets.Single().Effect.ShouldBe(PupilMovementEffect.Blocks);
        (await CountEnrolmentsAsync(pupilId)).ShouldBe(1);

        var real = await PostAsync($"/api/v1/pupils/{pupilId}/transfer", jar, payload with { dryRun = false });

        real.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await real.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("results for First Term are published. Withdraw them before moving pupils out of the arm.");
        (await CountEnrolmentsAsync(pupilId)).ShouldBe(1);
    }

    [Fact]
    public async Task Transfer_IntoAnArmWithPublishedResults_IsRefused()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        await SeedResultSetAsync(world.ArmB, world.TermId, ResultSetState.Published);
        var jar = await SignInAsSuperAdminAsync();

        var response = await PostAsync($"/api/v1/pupils/{pupilId}/transfer", jar, new { armId = world.ArmB.ToString(), effectiveDate = Today });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("before moving pupils into the arm");
    }

    [Fact]
    public async Task Transfer_WithAFutureDate_IsRefused()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var jar = await SignInAsSuperAdminAsync();

        var response = await PostAsync(
            $"/api/v1/pupils/{pupilId}/transfer", jar, new { armId = world.ArmB.ToString(), effectiveDate = Today.AddDays(1) });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await CountEnrolmentsAsync(pupilId)).ShouldBe(1);
    }

    [Fact]
    public async Task Transfer_WithoutThePrivilege_IsForbidden()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var jar = await SignInWithNoGrantsAsync();

        var response = await PostAsync($"/api/v1/pupils/{pupilId}/transfer", jar, new { armId = world.ArmB.ToString(), effectiveDate = Today });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Transfer_ByAnArmScopedHolder_IntoAnArmOutsideTheirScope_IsForbidden()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var jar = await SignInWithArmGrantAsync(Privileges.Pupil.Transfer, world.SessionId, world.ArmA);

        var response = await PostAsync($"/api/v1/pupils/{pupilId}/transfer", jar, new { armId = world.ArmB.ToString(), effectiveDate = Today });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        // The source arm is in scope; it is the destination that is refused.
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("destination class");
        (await CountEnrolmentsAsync(pupilId)).ShouldBe(1);
    }

    // ---- Status: leaving and coming back ---------------------------------------------------------

    [Fact]
    public async Task Withdraw_ThenReactivate_ClosesAndReopensTheEnrolment_AndKeepsTheNumber()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var publishedSet = await SeedResultSetAsync(world.ArmA, world.TermId, ResultSetState.Published);
        var jar = await SignInAsSuperAdminAsync();
        var leftOn = Today.AddDays(-10);

        var withdraw = await PostAsync(
            $"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Withdrawn", effectiveDate = leftOn, reason = "Family relocated." });

        withdraw.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PupilMovementOutcomeDto>(withdraw)).Pupil.Status.ShouldBe(PupilStatus.Withdrawn);

        // Back on the same day is an overlap; the day after is fine (marks keyed to the pupil reappear on recompute).
        var overlapping = await PostAsync(
            $"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Active", effectiveDate = leftOn, armId = world.ArmA.ToString() });
        overlapping.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var reactivate = await PostAsync(
            $"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Active", effectiveDate = leftOn.AddDays(1), armId = world.ArmA.ToString() });

        reactivate.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilMovementOutcomeDto>(reactivate);
        body.Pupil.Status.ShouldBe(PupilStatus.Active);
        body.Pupil.RegistrationNumber.ShouldBe("GRAS/2026/0001");

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var enrolments = await context.Enrolments.AsNoTracking().Where(e => e.PupilId == pupilId).OrderBy(e => e.EffectiveFrom)
            .ToListAsync(TestContext.Current.CancellationToken);
        enrolments.Select(e => (e.EffectiveFrom, e.EffectiveTo))
            .ShouldBe([(SessionStart, (DateOnly?)leftOn), (leftOn.AddDays(1), null)]);

        var changes = await context.PupilStatusChanges.AsNoTracking().Where(c => c.PupilId == pupilId).OrderBy(c => c.ChangedAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);
        changes.Select(c => c.ToStatus).ShouldBe([PupilStatus.Withdrawn, PupilStatus.Active]);
        changes[0].Reason.ShouldBe("Family relocated.");

        // A status change leaves published results published and unflagged (spec 6.5.14).
        var published = await context.ResultSets.AsNoTracking().SingleAsync(r => r.Id == publishedSet, TestContext.Current.CancellationToken);
        published.State.ShouldBe(ResultSetState.Published);
        published.NeedsRecompute.ShouldBeFalse();

        var history = await GetAsync($"/api/v1/pupils/{pupilId}/enrolments", jar);
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
        var historyBody = await ReadAsync<PupilEnrolmentHistoryDto>(history);
        historyBody.CurrentArmId.ShouldBe(world.ArmA.ToString());
        historyBody.Enrolments.Count.ShouldBe(2);
        historyBody.StatusChanges.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Withdraw_DropsAnAwaitingApprovalSetToDraft()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var set = await SeedResultSetAsync(world.ArmA, world.TermId, ResultSetState.AwaitingApproval);
        var jar = await SignInAsSuperAdminAsync();

        var response = await PostAsync(
            $"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Transferred", effectiveDate = Today, reason = "Moved to another school." });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking().SingleAsync(r => r.Id == set, TestContext.Current.CancellationToken);
        resultSet.State.ShouldBe(ResultSetState.Draft);
        resultSet.NeedsRecompute.ShouldBeTrue();
    }

    [Fact]
    public async Task Graduate_ClosesOnTheEffectiveDate_AndReactivatingNeedsAReason()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var jar = await SignInAsSuperAdminAsync();
        var graduatedOn = Today.AddDays(-3);

        var graduate = await PostAsync(
            $"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Graduated", effectiveDate = graduatedOn, reason = "Completed Primary 6." });

        graduate.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PupilMovementOutcomeDto>(graduate)).EnrolmentClosesOn.ShouldBe(graduatedOn);

        var withoutReason = await PostAsync(
            $"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Active", effectiveDate = Today, armId = world.ArmA.ToString() });
        withoutReason.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // Reversible from the day after, rather than only after the session ends.
        var reactivate = await PostAsync(
            $"/api/v1/pupils/{pupilId}/status", jar,
            new { targetStatus = "Active", effectiveDate = graduatedOn.AddDays(1), armId = world.ArmA.ToString(), reason = "Repeating Primary 6." });
        reactivate.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Undo_OnTheSameDay_ReopensTheEnrolment_AndCannotBeRepeated()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var set = await SeedResultSetAsync(world.ArmA, world.TermId, ResultSetState.Draft);
        var jar = await SignInAsSuperAdminAsync();
        await PostAsync($"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Withdrawn", effectiveDate = Today, reason = "Wrong pupil." });

        var undo = await PostAsync($"/api/v1/pupils/{pupilId}/status/undo", jar, new { reason = "Withdrew the wrong child." });

        undo.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PupilMovementOutcomeDto>(undo)).Pupil.Status.ShouldBe(PupilStatus.Active);

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var enrolments = await context.Enrolments.AsNoTracking().Where(e => e.PupilId == pupilId)
                .ToListAsync(TestContext.Current.CancellationToken);
            enrolments.Count.ShouldBe(1);
            enrolments[0].EffectiveTo.ShouldBeNull();
            var changes = await context.PupilStatusChanges.AsNoTracking().Where(c => c.PupilId == pupilId)
                .OrderBy(c => c.ChangedAtUtc).ToListAsync(TestContext.Current.CancellationToken);
            changes.Select(c => c.ToStatus).ShouldBe([PupilStatus.Withdrawn, PupilStatus.Active]);
            changes[1].Reason.ShouldBe("Undone: Withdrew the wrong child.");
            var resultSet = await context.ResultSets.AsNoTracking().SingleAsync(r => r.Id == set, TestContext.Current.CancellationToken);
            resultSet.NeedsRecompute.ShouldBeTrue();
        }

        var again = await PostAsync($"/api/v1/pupils/{pupilId}/status/undo", jar, new { reason = (string?)null });
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("pupil.status_undo_unavailable");
    }

    [Fact]
    public async Task Undo_ByAnArmScopedHolder_OfTheirOwnWithdrawal_Succeeds()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var jar = await SignInWithArmGrantAsync(Privileges.Pupil.StatusUpdate, world.SessionId, world.ArmA);

        var withdraw = await PostAsync(
            $"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Withdrawn", effectiveDate = Today, reason = "Wrong pupil." });
        withdraw.StatusCode.ShouldBe(HttpStatusCode.OK);

        // A leaver has no open enrolment, so scope is judged on the arm being reopened.
        var undo = await PostAsync($"/api/v1/pupils/{pupilId}/status/undo", jar, new { reason = (string?)null });

        undo.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Undo_OfAChangeRecordedOnAnEarlierDay_IsRefused()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor");
        var jar = await SignInAsSuperAdminAsync();
        await PostAsync($"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Withdrawn", effectiveDate = Today, reason = "Left." });

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupil_status_changes SET changed_at_utc = changed_at_utc - interval '2 days' WHERE pupil_id = {pupilId}",
                TestContext.Current.CancellationToken);
        }

        var undo = await PostAsync($"/api/v1/pupils/{pupilId}/status/undo", jar, new { reason = (string?)null });

        undo.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task StatusChange_OnAPendingAdmission_PointsToTheAdmissionsQueue()
    {
        RequireDatabase();
        var world = await SeedWorldAsync();
        var pupilId = await SeedActivePupilAsync(world.ArmA, "Okafor", pending: true);
        var jar = await SignInAsSuperAdminAsync();

        var response = await PostAsync(
            $"/api/v1/pupils/{pupilId}/status", jar, new { targetStatus = "Withdrawn", effectiveDate = Today, reason = "Lapsed." });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("pupil.status_pending");
    }

    // ---- Seeding --------------------------------------------------------------------------------

    private sealed record World(Guid SessionId, Guid TermId, Guid ArmA, Guid ArmB, DateOnly SessionEnd);

    private async Task<World> SeedWorldAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking().OrderBy(l => l.ProgressionOrder)
            .FirstAsync(TestContext.Current.CancellationToken)).Id;

        var sessionEnd = SessionStart.AddDays(300);
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), $"{SessionStart.Year}/{SessionStart.Year + 1}", SessionStart, sessionEnd).Value;
        session.Activate();
        context.Add(session);

        var term = Term.Create(Guid.CreateVersion7(), session.Id, 1, "First Term", SessionStart, Today.AddDays(30)).Value;
        term.Open().IsSuccess.ShouldBeTrue();
        context.Add(term);

        var armA = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", 30, null).Value;
        var armB = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "B", 30, null).Value;
        context.AddRange(armA, armB);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new World(session.Id, term.Id, armA.Id, armB.Id, sessionEnd);
    }

    private async Task<Guid> SeedActivePupilAsync(Guid armId, string surname, bool pending = false)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female,
            new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9), nationality: null,
            "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;
        context.Add(pupil);

        if (!pending)
        {
            context.Add(Enrolment.Open(Guid.CreateVersion7(), pupil.Id, armId, SessionStart).Value);
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (!pending)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupils SET status = {nameof(PupilStatus.Active)}, registration_number = {"GRAS/2026/0001"} WHERE id = {pupil.Id}",
                TestContext.Current.CancellationToken);
        }

        return pupil.Id;
    }

    private async Task<Guid> SeedResultSetAsync(Guid armId, Guid termId, ResultSetState state)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var resultSet = ResultSet.Create(Guid.CreateVersion7(), armId, termId).Value;
        context.Add(resultSet);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE result_set SET state = {state.ToString()}, needs_recompute = {false} WHERE id = {resultSet.Id}",
            TestContext.Current.CancellationToken);

        return resultSet.Id;
    }

    private async Task<int> CountEnrolmentsAsync(Guid pupilId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Enrolments.CountAsync(e => e.PupilId == pupilId, TestContext.Current.CancellationToken);
    }

    // ---- HTTP -----------------------------------------------------------------------------------

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        return await SignInAsync(email);
    }

    private async Task<CookieJar> SignInWithNoGrantsAsync()
    {
        var (_, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);
        return await SignInAsync(email);
    }

    private async Task<CookieJar> SignInWithArmGrantAsync(string privilege, Guid sessionId, Guid armId)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = Role.Create(Guid.CreateVersion7(), $"Role-{Guid.NewGuid():N}", null, [privilege]).Value;
            context.Add(role);
            context.Add(RoleAssignment.Create(Guid.CreateVersion7(), accountId, role.Id, sessionId, ScopeType.ArmList, [armId], accountId).Value);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return await SignInAsync(email);
    }

    private async Task<CookieJar> SignInAsync(string email)
    {
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await SendAsync(HttpMethod.Post, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password), withKey: false);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private async Task<HttpResponseMessage> GetAsync(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload) =>
        SendAsync(HttpMethod.Post, url, jar, payload, withKey: true);

    private async Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string url, CookieJar jar, T payload, bool withKey)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        if (withKey)
        {
            request.Headers.Add("Idempotency-Key", $"key-{Guid.NewGuid():N}");
        }

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
