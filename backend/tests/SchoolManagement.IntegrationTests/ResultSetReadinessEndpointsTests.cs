using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Configurations;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0088 stage B's readiness endpoint (spec §6.7.5, §6.7.11): mark
/// completeness (AC B1), the ratings/attendance rules (AC B2), blocker codes (AC B3), the
/// left-during-term exclusion (AC B4), and a nursery and a primary arm each passing and failing the
/// gate in their own terms (AC B7). Signs in as a real seeded Super Admin throughout.
/// </summary>
public sealed class ResultSetReadinessEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    private static readonly Guid Ca1Id = AssessmentComponentConfiguration.SeededIds[0];
    private static readonly Guid Ca2Id = AssessmentComponentConfiguration.SeededIds[1];

    // Primary's shared three-point trait scale (see TraitRatingEndpointsTests's own remarks).
    private static readonly Guid PrimaryExcellentPointId = RatingScalePointConfiguration.AllSeededIds[6];

    // Nursery's shared four-point development scale (see DevelopmentRatingEndpointsTests's own remarks).
    private static readonly Guid NurseryExcellentPointId = RatingScalePointConfiguration.AllSeededIds[3];

    private static int _nextSessionStartYear = 9600;

    // ---- AC B4: left during the term ------------------------------------------------------------

    [Fact]
    public async Task Get_APupilWhoLeftDuringTheTerm_IsExcludedFromCountersAndListedSeparately()
    {
        RequireDatabase();
        var (armId, termId, _, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Adeyemi");
        var termStartDate = await GetTermStartDateAsync(termId);
        await CloseEnrolmentAsync(pupilId, termStartDate.AddDays(14)); // inside the term, well clear of any concurrent test's own year
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetReadinessAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ResultSetReadinessDto>(response);
        body.Pupils.ShouldBeEmpty();
        body.LeftDuringTerm.Count.ShouldBe(1);
        body.LeftDuringTerm[0].PupilId.ShouldBe(pupilId.ToString());
        body.Counters.Marks.Total.ShouldBe(0);
        body.Counters.Ratings.Total.ShouldBe(0);
        body.Counters.Attendance.Total.ShouldBe(0);
    }

    // ---- "Not started" -----------------------------------------------------------------------

    [Fact]
    public async Task Get_NotStartedArm_ReturnsNullResultSetAndEverythingMissing()
    {
        RequireDatabase();
        var (armId, termId, _, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        await SeedPupilOnRosterAsync(armId, "Bello");
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetReadinessAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ResultSetReadinessDto>(response);
        body.ResultSet.ShouldBeNull();
        body.CanSubmit.ShouldBeFalse();
        body.Blockers.ShouldContain(blocker => blocker.Code == "not_computed");
        body.Blockers.ShouldContain(blocker => blocker.Code == "times_school_opened_missing");
        body.Blockers.ShouldContain(blocker => blocker.Code == "form_teacher_missing");
    }

    // ---- AC B1: mark completeness ---------------------------------------------------------------

    [Fact]
    public async Task Get_APartiallyFilledMarkCell_ReportsPartialStatusAndFilledPartsCount()
    {
        RequireDatabase();
        var (armId, termId, subjectId, _) = await SeedArmWithSubjectAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Chukwu");
        var jar = await SignInAsSuperAdminAsync();

        // Only CA1 filled — CA2 and the exam are both blank, so 1 of 3 parts (componentCount 3).
        await SaveScoreSheetAsync(armId, jar, subjectId, termId, null, ScoreRow(pupilId, ca1: 15, ca2: null, exam: null));

        var response = await GetReadinessAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ResultSetReadinessDto>(response);
        body.ComponentCount.ShouldBe(3);
        var cell = body.Pupils.Single().Marks.Single(m => m.SubjectId == subjectId.ToString());
        cell.Status.ShouldBe(MarkCompletionStatus.Partial);
        cell.FilledParts.ShouldBe(1);
        body.Blockers.ShouldContain(blocker => blocker.Code == "marks_incomplete");
    }

    [Fact]
    public async Task Get_AVoidedMark_ReadsAsAnEmptyCellNotComplete()
    {
        RequireDatabase();
        var (armId, termId, subjectId, _) = await SeedArmWithSubjectAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Danladi");
        var jar = await SignInAsSuperAdminAsync();

        await SaveScoreSheetAsync(armId, jar, subjectId, termId, null, ScoreRow(pupilId, 15, 15, 55));
        var voidResponse = await Client.SendAsync(
            BuildPostRequest(
                $"/api/v1/arms/{armId}/score-sheets/void", jar,
                new { subjectId = subjectId.ToString(), termId = termId.ToString(), reason = "Whole class re-marked after an error." }),
            TestContext.Current.CancellationToken);
        voidResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await GetReadinessAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ResultSetReadinessDto>(response);
        var cell = body.Pupils.Single().Marks.Single(m => m.SubjectId == subjectId.ToString());
        cell.Status.ShouldBe(MarkCompletionStatus.Empty);
        cell.FilledParts.ShouldBe(0);
    }

    // ---- AC B2: attendance race drift (present > opened counts incomplete) ----------------------

    [Fact]
    public async Task Get_AttendancePresentAboveTimesSchoolOpened_ReadsAsIncomplete()
    {
        RequireDatabase();
        var (armId, termId, _, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId, timesSchoolOpened: 60);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Eze");
        var jar = await SignInAsSuperAdminAsync();

        // 65 present against 60 opened — the term's own value was lowered after this pupil's
        // attendance was recorded against a higher figure (2026-09-19 race drift), simulated directly
        // by inserting the domain rows rather than through the endpoint, which enforces the same
        // upper-bound rule this scenario needs to bypass to exist at all.
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var resultSet = ResultSet.Create(Guid.CreateVersion7(), armId, termId).Value;
            context.Add(resultSet);
            var entry = SchoolManagement.Domain.Results.AttendanceEntry.Create(Guid.CreateVersion7(), resultSet.Id, pupilId, 65).Value;
            context.Add(entry);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await GetReadinessAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ResultSetReadinessDto>(response);
        body.Pupils.Single().AttendanceComplete.ShouldBeFalse();
        body.Blockers.ShouldContain(blocker => blocker.Code == "attendance_incomplete");
    }

    // ---- AC B7: nursery and primary each pass and fail the gate in their own terms ---------------

    [Fact]
    public async Task Get_PrimaryArm_FullyComplete_CanSubmitIsTrueWithNoBlockers()
    {
        RequireDatabase();
        var adminId = await SeedActiveAdminAsync();
        var (armId, termId, subjectId, resultSetId) = await SeedFullyReadyPrimaryArmAsync(adminId);
        var jar = await SignInAsAsync(adminId);

        var response = await GetReadinessAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ResultSetReadinessDto>(response);
        body.ResultSet!.Id.ShouldBe(resultSetId.ToString());
        body.Blockers.ShouldBeEmpty();
        body.CanSubmit.ShouldBeTrue();
        body.Subjects.ShouldContain(subject => subject.SubjectId == subjectId.ToString());
    }

    [Fact]
    public async Task Get_PrimaryArm_MissingTraitRatings_CannotSubmit()
    {
        RequireDatabase();
        var adminId = await SeedActiveAdminAsync();
        var (armId, termId, _, _) = await SeedFullyReadyPrimaryArmAsync(adminId, skipTraitRatings: true);
        var jar = await SignInAsAsync(adminId);

        var response = await GetReadinessAsync(armId, termId, jar);

        var body = await ReadAsync<ResultSetReadinessDto>(response);
        body.CanSubmit.ShouldBeFalse();
        body.Blockers.ShouldContain(blocker => blocker.Code == "ratings_incomplete");
        body.Pupils.Single().RatingsComplete.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_NurseryArm_FullyComplete_CanSubmitIsTrueWithNoBlockers()
    {
        RequireDatabase();
        var adminId = await SeedActiveAdminAsync();
        var (armId, termId, _, resultSetId) = await SeedFullyReadyNurseryArmAsync(adminId);
        var jar = await SignInAsAsync(adminId);

        var response = await GetReadinessAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ResultSetReadinessDto>(response);
        body.ResultSet!.Id.ShouldBe(resultSetId.ToString());
        body.Blockers.ShouldBeEmpty();
        body.CanSubmit.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_NurseryArm_MissingDevelopmentRatings_CannotSubmit()
    {
        RequireDatabase();
        var adminId = await SeedActiveAdminAsync();
        var (armId, termId, _, _) = await SeedFullyReadyNurseryArmAsync(adminId, skipDevelopmentRatings: true);
        var jar = await SignInAsAsync(adminId);

        var response = await GetReadinessAsync(armId, termId, jar);

        var body = await ReadAsync<ResultSetReadinessDto>(response);
        body.CanSubmit.ShouldBeFalse();
        body.Blockers.ShouldContain(blocker => blocker.Code == "ratings_incomplete");
    }

    // ---- Fully-ready arm builders -----------------------------------------------------------------

    private async Task<(Guid ArmId, Guid TermId, Guid SubjectId, Guid ResultSetId)> SeedFullyReadyPrimaryArmAsync(
        Guid adminId, bool skipTraitRatings = false)
    {
        var (armId, termId, subjectId, jar) = await SeedReadyArmCommonAsync(SeededClassLevels.PrimarySectionId, adminId);

        var pupilId = await SeedPupilOnRosterAsync(armId, "Okafor");

        var scoreResponse = await SaveScoreSheetAsync(armId, jar, subjectId, termId, null, ScoreRow(pupilId, 15, 15, 55));
        scoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        if (!skipTraitRatings)
        {
            var ratings = TraitConfiguration.AllSeededIds
                .Select(traitId => (traitId, PrimaryExcellentPointId))
                .ToArray();
            var traitResponse = await PutAsync(
                $"/api/v1/arms/{armId}/trait-ratings", jar,
                new { termId = termId.ToString(), version = (string?)null, rows = new[] { TraitRow(pupilId, ratings) } });
            traitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await CompleteCommonSheetsAsync(armId, termId, pupilId, jar);
        var resultSetId = await ComputeAsync(armId, termId, jar);

        return (armId, termId, subjectId, resultSetId);
    }

    private async Task<(Guid ArmId, Guid TermId, Guid SubjectId, Guid ResultSetId)> SeedFullyReadyNurseryArmAsync(
        Guid adminId, bool skipDevelopmentRatings = false)
    {
        var (armId, termId, subjectId, jar) = await SeedReadyArmCommonAsync(SeededClassLevels.NurserySectionId, adminId);

        var pupilId = await SeedPupilOnRosterAsync(armId, "Nwosu");

        var scoreResponse = await SaveScoreSheetAsync(armId, jar, subjectId, termId, null, ScoreRow(pupilId, 15, 15, 55));
        scoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        if (!skipDevelopmentRatings)
        {
            var cells = DevelopmentIndicatorConfiguration.AllSeededIds
                .Select(indicatorId => (indicatorId, (object)new { pointId = NurseryExcellentPointId.ToString(), comment = (string?)null }))
                .ToArray();
            var developmentResponse = await PutAsync(
                $"/api/v1/arms/{armId}/development-ratings", jar,
                new
                {
                    termId = termId.ToString(),
                    version = (string?)null,
                    rows = new[]
                    {
                        new { pupilId = pupilId.ToString(), ratings = cells.ToDictionary(entry => entry.indicatorId.ToString(), entry => entry.Item2) },
                    },
                });
            developmentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await CompleteCommonSheetsAsync(armId, termId, pupilId, jar);
        var resultSetId = await ComputeAsync(armId, termId, jar);

        return (armId, termId, subjectId, resultSetId);
    }

    /// <summary>Attendance and the class-teacher remark — identical for both sections.</summary>
    private async Task CompleteCommonSheetsAsync(Guid armId, Guid termId, Guid pupilId, CookieJar jar)
    {
        var attendanceResponse = await PutAsync(
            $"/api/v1/arms/{armId}/attendance", jar,
            new { termId = termId.ToString(), version = (string?)null, rows = new[] { new { pupilId = pupilId.ToString(), timesPresent = 58 } } });
        attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var remarkResponse = await PutAsync(
            $"/api/v1/arms/{armId}/class-teacher-remarks", jar,
            new { termId = termId.ToString(), version = (string?)null, rows = new[] { new { pupilId = pupilId.ToString(), remark = "A pleasure to teach." } } });
        remarkResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<Guid> ComputeAsync(Guid armId, Guid termId, CookieJar jar)
    {
        var resultSetId = await GetResultSetIdAsync(armId, termId);
        var computeResponse = await Client.SendAsync(
            BuildPostRequestNoBody($"/api/v1/result-sets/{resultSetId}/compute", jar),
            TestContext.Current.CancellationToken);
        computeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        return resultSetId;
    }

    /// <summary>Session, term (times school opened set), arm (form teacher assigned) and its one subject.</summary>
    private async Task<(Guid ArmId, Guid TermId, Guid SubjectId, CookieJar Jar)> SeedReadyArmCommonAsync(Guid sectionId, Guid adminId)
    {
        var (armId, termId, subjectId, _) = await SeedArmWithSubjectAsync(sectionId, timesSchoolOpened: 60, formTeacherAdminId: adminId);
        var jar = await SignInAsAsync(adminId);
        return (armId, termId, subjectId, jar);
    }

    private static object ScoreRow(Guid pupilId, int? ca1, int? ca2, int? exam) => new
    {
        pupilId = pupilId.ToString(),
        componentMarks = new Dictionary<string, int?> { [Ca1Id.ToString()] = ca1, [Ca2Id.ToString()] = ca2 },
        examMark = exam,
        examAbsent = false,
    };

    private static object TraitRow(Guid pupilId, (Guid TraitId, Guid PointId)[] ratings) => new
    {
        pupilId = pupilId.ToString(),
        ratings = ratings.ToDictionary(entry => entry.TraitId.ToString(), entry => (string?)entry.PointId.ToString()),
    };

    private Task<HttpResponseMessage> SaveScoreSheetAsync(
        Guid armId, CookieJar jar, Guid subjectId, Guid termId, string? version, params object[] rows) =>
        Client.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{armId}/score-sheets", jar,
                new { subjectId = subjectId.ToString(), termId = termId.ToString(), version, rows }),
            TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> GetReadinessAsync(Guid armId, Guid termId, CookieJar jar) =>
        GetAsyncCore($"/api/v1/arms/{armId}/readiness?termId={termId}", jar);

    private async Task<Guid> GetResultSetIdAsync(Guid armId, Guid termId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.ArmId == armId && r.TermId == termId, TestContext.Current.CancellationToken);
        return resultSet.Id;
    }

    private async Task<DateOnly> GetTermStartDateAsync(Guid termId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await context.Terms.AsNoTracking().SingleAsync(t => t.Id == termId, TestContext.Current.CancellationToken)).StartDate;
    }

    private async Task CloseEnrolmentAsync(Guid pupilId, DateOnly effectiveTo)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var enrolment = await context.Enrolments.SingleAsync(
            e => e.PupilId == pupilId && e.EffectiveTo == null, TestContext.Current.CancellationToken);
        enrolment.Close(effectiveTo).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedActiveAdminAsync()
    {
        var (accountId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false, email: $"admin-{Guid.NewGuid():N}@example.com");
        return accountId;
    }

    private async Task<CookieJar> SignInAsAsync(Guid adminId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = await context.AdminAccounts.AsNoTracking()
            .Where(account => account.Id == adminId)
            .Select(account => account.Email)
            .SingleAsync(TestContext.Current.CancellationToken);

        var jar = new CookieJar();
        await GetAsyncCore(CsrfUrl, jar);
        var signIn = await PostAsyncCore(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    /// <summary>Seeds an active session, a term (optionally with <c>timesSchoolOpened</c> set), and one arm under <paramref name="sectionId"/>.</summary>
    private async Task<(Guid ArmId, Guid TermId, Guid SessionId, Guid LevelId)> SeedArmAsync(
        Guid sectionId, int? timesSchoolOpened = null, Guid? formTeacherAdminId = null)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == sectionId, TestContext.Current.CancellationToken)).Id;

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = SchoolManagement.Domain.Sessions.AcademicSession.Create(
            Guid.CreateVersion7(), $"{startYear}/{startYear + 1}",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;
        session.Activate();
        context.Add(session);

        var term = Term.Create(
            Guid.CreateVersion7(), session.Id, 1, "First Term",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear, 12, 12)).Value;

        if (timesSchoolOpened is { } opened)
        {
            term.SetTimesSchoolOpened(opened);
        }

        context.Add(term);

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, formTeacherAdminId).Value;
        context.Add(arm);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (arm.Id, term.Id, session.Id, levelId);
    }

    private async Task<(Guid ArmId, Guid TermId, Guid SubjectId, Guid LevelId)> SeedArmWithSubjectAsync(
        Guid sectionId, int? timesSchoolOpened = null, Guid? formTeacherAdminId = null)
    {
        var (armId, termId, sessionId, levelId) = await SeedArmAsync(sectionId, timesSchoolOpened, formTeacherAdminId);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var subject = Subject.Create(Guid.CreateVersion7(), $"Subject {Guid.NewGuid():N}", null, null).Value;
        context.Add(subject);

        var mapping = SubjectMapping.Create(Guid.CreateVersion7(), subject.Id, levelId, sessionId, termId, 1).Value;
        context.Add(mapping);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (armId, termId, subject.Id, levelId);
    }

    private async Task<Guid> SeedPupilOnRosterAsync(Guid armId, string surname)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female,
            new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9), nationality: null,
            "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;
        context.Add(pupil);

        var enrolment = Enrolment.Open(Guid.CreateVersion7(), pupil.Id, armId, new DateOnly(2026, 9, 14)).Value;
        context.Add(enrolment);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = {pupil.Id}",
            TestContext.Current.CancellationToken);
        return pupil.Id;
    }

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (accountId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        _ = accountId;
        var jar = new CookieJar();
        await GetAsyncCore(CsrfUrl, jar);
        var signIn = await PostAsyncCore(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> PutAsync<T>(string url, CookieJar jar, T payload) =>
        Client.SendAsync(BuildPutRequest(url, jar, payload), TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> GetAsyncCore(string url, CookieJar jar) => SendAsyncCore(Client, HttpMethod.Get, url, jar, jar.Apply);

    private Task<HttpResponseMessage> PostAsyncCore<T>(string url, CookieJar jar, T payload) =>
        SendWithCsrfAsync(Client, HttpMethod.Post, url, jar, payload);

    private static HttpRequestMessage BuildPutRequest<T>(string url, CookieJar jar, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        return request;
    }

    private static HttpRequestMessage BuildPostRequest<T>(string url, CookieJar jar, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        return request;
    }

    private static HttpRequestMessage BuildPostRequestNoBody(string url, CookieJar jar)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        jar.ApplyWithCsrf(request);
        return request;
    }

    private static async Task<HttpResponseMessage> SendAsyncCore(
        HttpClient client, HttpMethod method, string url, CookieJar jar, Action<HttpRequestMessage> apply)
    {
        using var request = new HttpRequestMessage(method, url);
        apply(request);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(
        HttpClient client, HttpMethod method, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
