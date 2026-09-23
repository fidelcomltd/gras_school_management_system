using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Weekly;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Spec 6.10 weekly reports: sparse save and the grid read (phrases, illness marker, week list), the length and roster
/// rules, publication (refusing an empty week, a later report inheriting the published week), and the two reports.
/// Signs in as a seeded Super Admin, who holds every privilege school-wide.
/// </summary>
public sealed class WeeklyReportEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    private static int _nextSessionStartYear = 7000;

    [Fact]
    public async Task Save_ThenGet_ReturnsTheNotes_TheIllnessMarker_ThePhrases_AndTheWeekList()
    {
        RequireDatabase();
        var (armId, termId, termStart) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Adeyemi");
        await SeedPupilOnRosterAsync(armId, "Bello");
        var jar = await SignInAsSuperAdminAsync();

        var save = await SaveAsync(armId, jar, termId, 2,
            Cell(pupilId, "Monday", "Eating", "Ate everything"),
            Cell(pupilId, "Tuesday", "Eating", "Ate half"),
            Cell(pupilId, "Monday", "SymptomsOfIllness", "Runny nose"),
            Cell(pupilId, "Wednesday", "SymptomsOfIllness", "Coughing"));
        save.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var grid = await ReadAsync<WeeklyGridDto>(await GetAsyncCore($"/api/v1/arms/{armId}/weekly?termId={termId}&weekNumber=2", jar));

        grid.WeekStartDate.ShouldBe(termStart.AddDays(7));
        grid.Published.ShouldBeFalse();
        grid.Rows.Count.ShouldBe(2);
        var row = grid.Rows.Single(candidate => candidate.PupilId == pupilId.ToString());
        row.IllnessDays.ShouldBe(2);
        row.Days.Count.ShouldBe(5);
        row.Days[0].Eating.ShouldBe("Ate everything");
        row.Days[0].LastEditedBy.ShouldNotBeNull();
        row.Days[1].Date.ShouldBe(termStart.AddDays(8));
        grid.Rows.Single(candidate => candidate.PupilId != pupilId.ToString()).Days.ShouldAllBe(day => day.Eating == null);
        grid.Phrases.Eating.ShouldBe(["Ate half", "Ate everything"], ignoreOrder: true);
        grid.Weeks.Count.ShouldBe(13);
        grid.Weeks[1].PupilsWithNotes.ShouldBe(1);

        // A blank value clears the line.
        (await SaveAsync(armId, jar, termId, 2, Cell(pupilId, "Monday", "Eating", "  "))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var cleared = await ReadAsync<WeeklyGridDto>(await GetAsyncCore($"/api/v1/arms/{armId}/weekly?termId={termId}&weekNumber=2", jar));
        cleared.Rows.Single(candidate => candidate.PupilId == pupilId.ToString()).Days[0].Eating.ShouldBeNull();
    }

    [Fact]
    public async Task Save_ALineOverItsLimit_OrAPupilOffTheRoster_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Chukwu");
        var jar = await SignInAsSuperAdminAsync();

        (await SaveAsync(armId, jar, termId, 1, Cell(pupilId, "Monday", "Eating", new string('a', 301))))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var offRoster = await SaveAsync(armId, jar, termId, 1, Cell(Guid.CreateVersion7(), "Monday", "Eating", "Fine"));
        offRoster.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(offRoster);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("not on the arm's active roster");
    }

    [Fact]
    public async Task Publish_RefusesAnEmptyWeek_ThenPublishes_AndALaterReportInheritsIt()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var first = await SeedPupilOnRosterAsync(armId, "Danladi");
        var second = await SeedPupilOnRosterAsync(armId, "Efe");
        var jar = await SignInAsSuperAdminAsync();

        var empty = await PostAsyncCore($"/api/v1/arms/{armId}/weekly/3/publish", jar, new { termId = termId.ToString() });
        empty.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadAsStringAsync(empty)).ShouldContain("Nothing has been written for Week 3 yet");

        await SaveAsync(armId, jar, termId, 3, Cell(first, "Friday", "TeacherComment", "A good week."));
        var publish = await PostAsyncCore($"/api/v1/arms/{armId}/weekly/3/publish", jar, new { termId = termId.ToString() });
        publish.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<WeeklyWeekSummaryDto>(publish)).Published.ShouldBeTrue();

        await SaveAsync(armId, jar, termId, 3, Cell(second, "Friday", "Eating", "Ate everything"));
        var pupilView = await ReadAsync<PupilWeeklyTermDto>(await GetAsyncCore($"/api/v1/pupils/{second}/weekly?termId={termId}", jar));
        pupilView.Weeks[2].Published.ShouldBeTrue();
        pupilView.Weeks[2].Days![4].Eating.ShouldBe("Ate everything");
        pupilView.Weeks[0].Days.ShouldBeNull();

        (await PostAsyncCore($"/api/v1/arms/{armId}/weekly/3/unpublish", jar, new { termId = termId.ToString() })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var grid = await ReadAsync<WeeklyGridDto>(await GetAsyncCore($"/api/v1/arms/{armId}/weekly?termId={termId}&weekNumber=3", jar));
        grid.Published.ShouldBeFalse();
    }

    [Fact]
    public async Task Reports_CountCompletion_AndListPupilsIllOnTwoOrMoreDays()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var ill = await SeedPupilOnRosterAsync(armId, "Femi");
        var once = await SeedPupilOnRosterAsync(armId, "Gbenga");
        var jar = await SignInAsSuperAdminAsync();
        await SaveAsync(armId, jar, termId, 1,
            Cell(ill, "Monday", "SymptomsOfIllness", "Fever"),
            Cell(ill, "Tuesday", "SymptomsOfIllness", "Still feverish"),
            Cell(once, "Monday", "SymptomsOfIllness", "Headache"));

        var completion = await ReadAsync<WeeklyCompletionReportDto>(
            await GetAsyncCore($"/api/v1/reports/weekly-completion?termId={termId}&weekNumber=1", jar));
        var row = completion.Items.Single(item => item.ArmId == armId.ToString());
        row.PupilsWithNotes.ShouldBe(2);
        row.CellsFilled.ShouldBe(3);
        row.CellsAvailable.ShouldBe(2 * 5 * 8);

        var illness = await ReadAsync<WeeklyIllnessReportDto>(await GetAsyncCore($"/api/v1/reports/weekly-illness?termId={termId}", jar));
        illness.Items.Single().PupilId.ShouldBe(ill.ToString());
        illness.Items.Single().Observations.Select(observation => observation.Text).ShouldBe(["Fever", "Still feverish"]);
    }

    [Fact]
    public async Task Settings_TurnAutoPublishOn_AndTheGridReflectsIt()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var jar = await SignInAsSuperAdminAsync();

        var response = await SendWithCsrfAsync(Client, HttpMethod.Put, $"/api/v1/arms/{armId}/weekly/settings", jar, new { autoPublish = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<WeeklyGridDto>(await GetAsyncCore($"/api/v1/arms/{armId}/weekly?termId={termId}&weekNumber=1", jar))).AutoPublish.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401_AndAnUnknownArm_Returns404()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();

        (await GetAsyncCore($"/api/v1/arms/{armId}/weekly?termId={termId}", new CookieJar())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var jar = await SignInAsSuperAdminAsync();
        (await GetAsyncCore($"/api/v1/arms/{Guid.CreateVersion7()}/weekly?termId={termId}", jar)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Seeding and HTTP helpers ----------------------------------------------------------------

    private static object Cell(Guid pupilId, string day, string field, string? value) =>
        new { pupilId = pupilId.ToString(), dayOfWeek = day, field, value };

    /// <summary>An active session, a thirteen-week First Term starting on a Monday in early January, and one arm.</summary>
    private async Task<(Guid ArmId, Guid TermId, DateOnly TermStart)> SeedArmAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;

        var year = Interlocked.Increment(ref _nextSessionStartYear);
        var session = AcademicSession.Create(Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 30)).Value;
        session.Activate();
        context.Add(session);
        var start = SchoolManagement.Domain.Weekly.TermWeeks.MondayOf(new DateOnly(year + 1, 1, 7));
        var term = Term.Create(Guid.CreateVersion7(), session.Id, 1, "First Term", start, start.AddDays(88)).Value;
        context.Add(term);
        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (arm.Id, term.Id, start);
    }

    private async Task<Guid> SeedPupilOnRosterAsync(Guid armId, string surname)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pupil = Pupil.Create(
            Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female, new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9),
            nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
        context.Add(pupil);
        context.Add(SchoolManagement.Domain.Enrolments.Enrolment.Open(Guid.CreateVersion7(), pupil.Id, armId, new DateOnly(2026, 9, 1)).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = {pupil.Id}", TestContext.Current.CancellationToken);
        return pupil.Id;
    }

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsyncCore(CsrfUrl, jar);
        (await PostAsyncCore(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password))).StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private async Task<HttpResponseMessage> SaveAsync(Guid armId, CookieJar jar, Guid termId, int weekNumber, params object[] cells)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/arms/{armId}/weekly")
        {
            Content = JsonContent.Create(new { termId = termId.ToString(), weekNumber, cells }),
        };
        jar.ApplyWithCsrf(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<string> ReadAsStringAsync(HttpResponseMessage response) => response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private async Task<HttpResponseMessage> GetAsyncCore(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PostAsyncCore<T>(string url, CookieJar jar, T payload) => SendWithCsrfAsync(Client, HttpMethod.Post, url, jar, payload);

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(HttpClient client, HttpMethod method, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
