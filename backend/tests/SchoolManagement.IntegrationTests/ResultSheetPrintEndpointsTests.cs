using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>Staff result printing: <c>GET /arms/{armId}/result-sheets/pdf</c>, published sets only, whole arm or one pupil.</summary>
public sealed class ResultSheetPrintEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static int _nextYear = 9500;

    [Fact]
    public async Task Print_RefusesUntilPublished_ThenPrintsTheArmAndOnePupil_AndRefusesAPupilWithNoResult()
    {
        RequireDatabase();
        var admin = await SignInAdminAsync();
        var seeded = await SeedAsync(admin.AccountId);
        var armUrl = $"/api/v1/arms/{seeded.ArmId}/result-sheets/pdf?termId={seeded.TermId}";

        using (var early = await GetAsync(admin.Jar, armUrl))
        {
            early.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await early.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("result_set.not_published");
        }

        using (var publish = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/result-sets/{seeded.ResultSetId}/publish"))
        {
            publish.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
            admin.Jar.ApplyWithCsrf(publish);
            var published = await Client.SendAsync(publish, TestContext.Current.CancellationToken);
            published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        using (var whole = await GetAsync(admin.Jar, armUrl))
        {
            whole.StatusCode.ShouldBe(HttpStatusCode.OK, await whole.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            whole.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
            var bytes = await whole.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
        }

        using (var one = await GetAsync(admin.Jar, $"{armUrl}&pupilId={seeded.PupilIds[0]}"))
        {
            one.StatusCode.ShouldBe(HttpStatusCode.OK);
            one.Content.Headers.ContentDisposition!.FileNameStar.ShouldStartWith("GRAS-");
        }

        using var none = await GetAsync(admin.Jar, $"{armUrl}&pupilId={seeded.PupilIds[2]}");
        none.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await none.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("result_sheet.not_found");
    }

    private sealed record Seeded(Guid ArmId, Guid TermId, Guid ResultSetId, IReadOnlyList<Guid> PupilIds);

    /// <summary>An Approved, computed set with three pupils: the first two have a subject line, the third has none.</summary>
    private async Task<Seeded> SeedAsync(Guid adminId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;

        var year = Interlocked.Increment(ref _nextYear);
        var session = AcademicSession.Create(Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 31)).Value;
        session.Activate();
        context.Add(session);
        var term = Term.Create(Guid.CreateVersion7(), session.Id, 1, "First Term", new DateOnly(year, 9, 1), new DateOnly(year, 12, 15)).Value;
        term.SetTimesSchoolOpened(60).IsSuccess.ShouldBeTrue();
        term.UpdateSchedule(term.Name, term.StartDate, term.EndDate, term.EndDate.AddDays(21)).IsSuccess.ShouldBeTrue();
        context.Add(term);
        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.Add(arm);
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), arm.Id, term.Id).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.Approved);
        context.Add(resultSet);

        var subject = SchoolManagement.Domain.Subjects.Subject.Create(Guid.CreateVersion7(), $"Maths {Guid.NewGuid():N}"[..20], null, null).Value;
        context.Add(subject);
        context.Add(SchoolManagement.Domain.Subjects.SubjectMapping.Create(Guid.CreateVersion7(), subject.Id, arm.ClassLevelId, arm.SessionId, term.Id, 1).Value);
        var components = await context.AssessmentComponents.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        var marks = System.Text.Json.JsonSerializer.Serialize(components.Where(component => !component.IsExamination).ToDictionary(component => component.Id.ToString("D"), _ => 17));

        var pupilIds = new List<Guid>();
        foreach (var (surname, index) in new[] { "Okafor", "Eze", "Nwosu" }.Select((surname, index) => (surname, index)))
        {
            var pupil = Pupil.Create(
                Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female, new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9),
                nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
            context.Add(pupil);
            context.Add(Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, new DateOnly(year, 9, 1)).Value);
            context.Add(PupilRemark.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, RemarkKind.HeadTeacher, "Keep working hard.", adminId, "Mr Bello", DateTimeOffset.UtcNow).Value);
            if (index < 2)
            {
                context.Add(SubjectScore.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, subject.Id, term.Id, marks, null, examAbsent: true).Value);
                context.Add(SubjectResultLine.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, subject.Id, 34, null, 34, "E", "Not Now", 1, false, false));
                context.Add(PupilTermResult.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, 1, 100, 34, 34.00m, "E", 1, true, 2, 1, true, 2));
            }

            pupilIds.Add(pupil.Id);
        }

        resultSet.MarkComputed(null, DateTimeOffset.UtcNow, 2);
        var profile = await context.Set<SchoolManagement.Domain.Settings.SchoolProfile>().SingleAsync(TestContext.Current.CancellationToken);
        profile.SetCurrentLogo(Guid.CreateVersion7());
        profile.SetCurrentSignature(Guid.CreateVersion7());
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        foreach (var pupilId in pupilIds)
        {
            var number = $"GRAS/{year}/{pupilIds.IndexOf(pupilId) + 1:D4}";
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupils SET status = {nameof(PupilStatus.Active)}, registration_number = {number} WHERE id = {pupilId}",
                TestContext.Current.CancellationToken);
        }

        return new Seeded(arm.Id, term.Id, resultSet.Id, pupilIds);
    }

    private async Task<(CookieJar Jar, Guid AccountId)> SignInAdminAsync()
    {
        var (accountId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false, email: $"admin-{Guid.NewGuid():N}@example.com");
        var jar = new CookieJar();
        using (var csrf = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf"))
        {
            jar.Capture(await Client.SendAsync(csrf, TestContext.Current.CancellationToken));
        }

        using var signIn = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new SchoolManagement.Application.Auth.SignIn.SignInCommand(email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(signIn);
        var response = await Client.SendAsync(signIn, TestContext.Current.CancellationToken);
        jar.Capture(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (jar, accountId);
    }

    private async Task<HttpResponseMessage> GetAsync(CookieJar jar, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
