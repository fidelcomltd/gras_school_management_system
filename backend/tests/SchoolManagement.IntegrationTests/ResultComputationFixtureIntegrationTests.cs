using System.Globalization;
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
using SchoolManagement.IntegrationTests.Fixtures;
using SchoolManagement.IntegrationTests.Infrastructure;
using static SchoolManagement.IntegrationTests.Fixtures.ResultComputationFixtureData;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0071 stage 3: the §8.4 worked example, loaded as a real fixture (<see cref="ResultComputationFixtureData"/>)
/// and run through the real <c>POST /result-sets/{resultSetId}/compute</c> endpoint end to end, asserting
/// every figure §8.5 names against Primary 3A except the annual cumulative (out of scope — TASK-0071's own
/// card names §6.7.10 as its own card). <c>ResultComputationFixtureSelfCheckTests</c> (unit project,
/// no engine) proves the fixture's own arithmetic; THIS class proves the real engine agrees with it end to
/// end. If the two ever disagree, the self-check is the tie-breaker — see that class's own remarks.
/// </summary>
public sealed class ResultComputationFixtureIntegrationTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private static readonly Guid Ca1Id = AssessmentComponentConfiguration.SeededIds[0];
    private static readonly Guid Ca2Id = AssessmentComponentConfiguration.SeededIds[1];
    private static int _nextSessionStartYear = 9500;

    [Fact]
    public async Task Compute_OnThe84Fixture_MatchesEverySpec85Assertion()
    {
        RequireDatabase();

        var (arm3AId, arm3BId, termId, subjectIds) = await SeedFixtureStructureAsync();
        var jar = await SignInAsSuperAdminAsync();
        var pupilIds = await SeedFixturePupilsAsync(arm3AId, arm3BId);
        await SaveAllScoresAsync(arm3AId, arm3BId, termId, subjectIds, pupilIds, jar);

        var resultSetA = await GetResultSetIdAsync(arm3AId, termId);
        var resultSetB = await GetResultSetIdAsync(arm3BId, termId);

        // Both arms are computed — Primary 3's level position depends on BOTH (spec 8.4.7).
        var computeA = await ComputeAsync(resultSetA, jar);
        computeA.StatusCode.ShouldBe(HttpStatusCode.OK);
        var responseA = await ReadAsync<ComputeResultSetResponse>(computeA);
        responseA.PupilCount.ShouldBe(28);
        responseA.SubjectCount.ShouldBe(4);
        // Nobody in 3A is absent for every examination, and every subject has a counted pupil, so no
        // computation flag should fire here — TASK-0071's own no_examination_sat/absent_all_examinations
        // scenarios are exercised separately in ComputeResultSetEndpointTests.
        responseA.Flags.ShouldBeEmpty();

        var computeB = await ComputeAsync(resultSetB, jar);
        computeB.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var linesA = await context.Set<SubjectResultLine>().AsNoTracking()
            .Where(line => line.ResultSetId == resultSetA).ToListAsync(TestContext.Current.CancellationToken);
        var statsA = await context.Set<SubjectArmStatistic>().AsNoTracking()
            .Where(stat => stat.ResultSetId == resultSetA).ToListAsync(TestContext.Current.CancellationToken);
        var pupilResultsA = await context.Set<PupilTermResult>().AsNoTracking()
            .Where(result => result.ResultSetId == resultSetA).ToListAsync(TestContext.Current.CancellationToken);

        var adaezeId = pupilIds[Adaeze];
        var musaId = pupilIds[Musa];
        var chidiId = pupilIds[Chidi];
        var rank8Id = pupilIds[MathsRank8Pupil];

        SubjectResultLine Line(Guid pupilId, string subject) =>
            linesA.Single(line => line.PupilId == pupilId && line.SubjectId == subjectIds[subject]);

        // ---- Adaeze: four subject totals, four grades and four subject positions, spec 8.4.2/8.4.5 ----
        var adaezeEnglish = Line(adaezeId, English);
        adaezeEnglish.SubjectTotal.ShouldBe(86);
        adaezeEnglish.Grade.ShouldBe("A");
        adaezeEnglish.SubjectPosition.ShouldBe(3);
        adaezeEnglish.SubjectPositionTied.ShouldBeFalse();

        var adaezeMaths = Line(adaezeId, Mathematics);
        adaezeMaths.SubjectTotal.ShouldBe(78);
        adaezeMaths.Grade.ShouldBe("B");

        var adaezeScience = Line(adaezeId, BasicScienceAndTechnology);
        adaezeScience.SubjectTotal.ShouldBe(64);
        adaezeScience.Grade.ShouldBe("C+");
        adaezeScience.SubjectPosition.ShouldBe(12);

        var adaezeCca = Line(adaezeId, CulturalAndCreativeArts);
        adaezeCca.SubjectTotal.ShouldBe(92);
        adaezeCca.Grade.ShouldBe("A+");
        adaezeCca.SubjectPosition.ShouldBe(1);
        adaezeCca.SubjectPositionTied.ShouldBeFalse();

        // ---- The Mathematics tie: Adaeze and Chidi share 6th, the next pupil (on 74) is 8th, spec 8.4.5 --
        adaezeMaths.SubjectPosition.ShouldBe(6);
        adaezeMaths.SubjectPositionTied.ShouldBeTrue();

        var chidiMaths = Line(chidiId, Mathematics);
        chidiMaths.SubjectTotal.ShouldBe(78);
        chidiMaths.SubjectPosition.ShouldBe(6);
        chidiMaths.SubjectPositionTied.ShouldBeTrue();

        var rank8Maths = Line(rank8Id, Mathematics);
        rank8Maths.SubjectTotal.ShouldBe(74);
        rank8Maths.SubjectPosition.ShouldBe(8);
        rank8Maths.SubjectPositionTied.ShouldBeFalse();

        // ---- Mathematics: counted population 27 against ranked 28, lowest 31 (not Musa's 24), spec 8.4.3/8.4.4 --
        var mathsStats = statsA.Single(stat => stat.SubjectId == subjectIds[Mathematics]);
        mathsStats.CountedPupils.ShouldBe(27);
        mathsStats.RankedPupils.ShouldBe(28);
        mathsStats.HighestScore.ShouldBe(88);
        mathsStats.LowestScore.ShouldBe(31);
        mathsStats.ClassAverage.ShouldBe(59.7m);

        // ---- The four class averages to one decimal place, spec 8.4.4 ------------------------------------
        statsA.Single(stat => stat.SubjectId == subjectIds[English]).ClassAverage.ShouldBe(68.4m);
        statsA.Single(stat => stat.SubjectId == subjectIds[BasicScienceAndTechnology]).ClassAverage.ShouldBe(60.1m);
        statsA.Single(stat => stat.SubjectId == subjectIds[CulturalAndCreativeArts]).ClassAverage.ShouldBe(71.8m);

        // ---- Adaeze's overall result: total, average, grade, arm position, level position, spec 8.4.6/8.4.7/8.4.8
        var adaezeResult = pupilResultsA.Single(result => result.PupilId == adaezeId);
        adaezeResult.TotalObtained.ShouldBe(320);
        adaezeResult.Average.ShouldBe(80.00m);
        adaezeResult.OverallGrade.ShouldBe("B");
        adaezeResult.ArmPosition.ShouldBe(3);
        adaezeResult.ArmPositionTied.ShouldBeFalse();
        adaezeResult.ArmPupilCount.ShouldBe(28);
        adaezeResult.LevelPosition.ShouldBe(5);
        adaezeResult.LevelPupilCount.ShouldBe(54);

        // ---- Musa: exam-absent Mathematics — total 24, grade E, spec 8.4.3 -------------------------------
        var musaMaths = Line(musaId, Mathematics);
        musaMaths.SubjectTotal.ShouldBe(24);
        musaMaths.Grade.ShouldBe("E");
        musaMaths.ExamMark.ShouldBeNull();
        musaMaths.SubjectPosition.ShouldBe(28); // ranked population includes him; he is last.
        musaMaths.SubjectPositionTied.ShouldBeFalse();
    }

    // ---- Structure, pupil and score seeding ------------------------------------------------------------

    /// <summary>
    /// One class level, session and term; Primary 3A and 3B; the four fixture subjects mapped to that
    /// level for that term. <see cref="Mathematics"/> reuses the SEEDED subject of that exact name
    /// (<c>SeededSubjects.All</c>) rather than creating a second one — <c>Subject.NameKey</c> is a
    /// case-insensitive UNIQUE index, and "mathematics" is already taken. The other three fixture subject
    /// names ("English Studies", "Basic Science and Technology", "Cultural and Creative Arts") do not
    /// collide with any of the school's 28 seeded names and are created fresh.
    /// </summary>
    private async Task<(Guid Arm3AId, Guid Arm3BId, Guid TermId, IReadOnlyDictionary<string, Guid> SubjectIds)> SeedFixtureStructureAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken)).Id;

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), $"{startYear}/{startYear + 1}",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;
        session.Activate();
        context.Add(session);

        var term = Term.Create(
            Guid.CreateVersion7(), session.Id, 1, "First Term",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear, 12, 12)).Value;
        term.Open().IsSuccess.ShouldBeTrue();
        context.Add(term);

        var arm3A = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "3A", null, null).Value;
        var arm3B = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "3B", null, null).Value;
        context.Add(arm3A);
        context.Add(arm3B);

        var mathematicsId = SeededSubjects.All.Single(subject => subject.Name == Mathematics).Id;
        var subjectIds = new Dictionary<string, Guid>(StringComparer.Ordinal) { [Mathematics] = mathematicsId };

        var order = 1;
        foreach (var subjectName in Subjects)
        {
            if (!subjectIds.TryGetValue(subjectName, out var subjectId))
            {
                var subject = Subject.Create(Guid.CreateVersion7(), subjectName, null, null).Value;
                context.Add(subject);
                subjectId = subject.Id;
                subjectIds[subjectName] = subjectId;
            }

            var mapping = SubjectMapping.Create(Guid.CreateVersion7(), subjectId, levelId, session.Id, term.Id, order).Value;
            context.Add(mapping);
            order++;
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (arm3A.Id, arm3B.Id, term.Id, subjectIds);
    }

    /// <summary>
    /// Creates all 54 fixture pupils and opens their enrolment in the arm the fixture names, then
    /// activates them in one batched update. Returns the fixture pupil name (e.g. <see cref="Adaeze"/>
    /// or "Pupil3A_02") mapped to the real, generated <c>Guid</c>.
    /// </summary>
    private async Task<Dictionary<string, Guid>> SeedFixturePupilsAsync(Guid arm3AId, Guid arm3BId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupilIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var dateOfBirth = new DateOnly(2018, 1, 1);
        var asOfDate = new DateOnly(2026, 9, 9);
        var enrolledFrom = new DateOnly(2026, 9, 14);

        foreach (var row in Pupils)
        {
            var (firstName, surname) = NameFor(row);
            var pupil = Pupil.Create(
                Guid.CreateVersion7(), surname, firstName, middleName: null, PupilSex.Female,
                dateOfBirth, asOfDate, nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka",
                previousSchool: null, previousClass: null, otherInformation: null).Value;
            context.Add(pupil);

            var armId = row.Arm == Arm3A ? arm3AId : arm3BId;
            var enrolment = Enrolment.Open(Guid.CreateVersion7(), pupil.Id, armId, enrolledFrom).Value;
            context.Add(enrolment);

            pupilIds[row.Pupil] = pupil.Id;
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ids = pupilIds.Values.ToArray();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = ANY({ids})",
            TestContext.Current.CancellationToken);

        return pupilIds;
    }

    /// <summary>
    /// A fixture pupil key ("Adaeze Okafor") splits into first/surname directly. A filler key
    /// ("Pupil3A_07") has digits and an underscore, which <c>Pupil.Create</c>'s name pattern (letters,
    /// spaces, hyphens and apostrophes only — spec 6.5.4) rejects, so its numeric suffix is translated
    /// into a letter (A, B, C…) instead. The mapping is 1:1 and deterministic; nothing asserts on it.
    /// </summary>
    private static (string FirstName, string Surname) NameFor(PupilRow row)
    {
        var spaceIndex = row.Pupil.IndexOf(' ', StringComparison.Ordinal);
        if (spaceIndex >= 0)
        {
            return (row.Pupil[..spaceIndex], row.Pupil[(spaceIndex + 1)..]);
        }

        var surname = row.Arm == Arm3A ? "FillerThreeA" : "FillerThreeB";
        var index = int.Parse(row.Pupil[^2..], CultureInfo.InvariantCulture);
        var firstName = ((char)('A' + index - 1)).ToString();
        return (firstName, surname);
    }

    /// <summary>Saves every subject's score sheet for both arms, straight from the fixture's raw marks.</summary>
    private async Task SaveAllScoresAsync(
        Guid arm3AId, Guid arm3BId, Guid termId, IReadOnlyDictionary<string, Guid> subjectIds,
        Dictionary<string, Guid> pupilIds, CookieJar jar)
    {
        foreach (var subjectName in Subjects)
        {
            var subjectId = subjectIds[subjectName];

            var rowsA = Pupils.Where(pupil => pupil.Arm == Arm3A)
                .Select(pupil => BuildRow(pupilIds[pupil.Pupil], pupil[subjectName])).ToArray();
            (await SaveSheetAsync(arm3AId, jar, subjectId, termId, rowsA)).StatusCode.ShouldBe(HttpStatusCode.OK);

            var rowsB = Pupils.Where(pupil => pupil.Arm == Arm3B)
                .Select(pupil => BuildRow(pupilIds[pupil.Pupil], pupil[subjectName])).ToArray();
            (await SaveSheetAsync(arm3BId, jar, subjectId, termId, rowsB)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    private static object BuildRow(Guid pupilId, Mark mark) => new
    {
        pupilId = pupilId.ToString(),
        componentMarks = new Dictionary<string, int?> { [Ca1Id.ToString()] = mark.Ca1, [Ca2Id.ToString()] = mark.Ca2 },
        examMark = mark.ExamAbsent ? null : mark.Exam,
        examAbsent = mark.ExamAbsent,
    };

    // ---- HTTP helpers (same shape as ComputeResultSetEndpointTests) -------------------------------------

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await SendAsyncCore(HttpMethod.Get, CsrfUrl, jar, jar.Apply);
        var signIn = await SendWithCsrfAsync(HttpMethod.Post, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private async Task<Guid> GetResultSetIdAsync(Guid armId, Guid termId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.ArmId == armId && r.TermId == termId, TestContext.Current.CancellationToken);
        return resultSet.Id;
    }

    private Task<HttpResponseMessage> SaveSheetAsync(
        Guid armId, CookieJar jar, Guid subjectId, Guid termId, IReadOnlyList<object> rows) =>
        SendWithCsrfAsync(
            HttpMethod.Put, $"/api/v1/arms/{armId}/score-sheets", jar,
            new { subjectId = subjectId.ToString(), termId = termId.ToString(), version = (string?)null, rows });

    private Task<HttpResponseMessage> ComputeAsync(Guid resultSetId, CookieJar jar) =>
        SendWithCsrfNoBodyAsync(HttpMethod.Post, $"/api/v1/result-sets/{resultSetId}/compute", jar);

    private async Task<HttpResponseMessage> SendAsyncCore(HttpMethod method, string url, CookieJar jar, Action<HttpRequestMessage> apply)
    {
        using var request = new HttpRequestMessage(method, url);
        apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync<T>(HttpMethod method, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> SendWithCsrfNoBodyAsync(HttpMethod method, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(method, url);
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
