using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Spec 15 section 10.2: the class safeguarding sheet, on screen and as a PDF, for a seeded arm with one pupil whose health,
/// pickup and barred answers are recorded. Signs in as a seeded Super Admin, who holds the privilege school-wide.
/// </summary>
public sealed class PupilSafeguardingSheetEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static int _nextYear = 7900;

    [Fact]
    public async Task Sheet_ListsTheArmsPupils_WithHealthPickupAndAMarkerOnly_AndEachGenerationIsAudited()
    {
        RequireDatabase();
        var (armId, otherPupilId) = await SeedArmWithPupilAsync();
        var jar = await SignInAsync();

        var screen = await GetAsync($"/api/v1/reports/safeguarding?armId={armId}", jar);

        screen.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sheet = await ReadAsync<SafeguardingSheetDto>(screen);
        sheet.ArmName.ShouldEndWith("A");
        var row = sheet.Pupils.ShouldHaveSingleItem();
        row.Name.ShouldBe("OKAFOR Chidera");
        row.Allergies.ShouldBe("Groundnuts");
        row.MedicalConditions.ShouldBe("None");
        row.Medication.ShouldBe("Not asked");
        row.Hospital.ShouldBe("St. Charles Borromeo, 08037776666");
        row.PickupPersons.ShouldBe(["Ngozi Okafor (Aunt) 08059876543"]); // stored +234, printed as staff dial it
        row.BarredMarker.ShouldBe("Yes: see office");
        (await screen.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("Emeka"); // barred name
        sheet.Pupils.ShouldNotContain(pupil => pupil.PupilId == otherPupilId.ToString("D", CultureInfo.InvariantCulture));

        var pdf = await GetAsync($"/api/v1/reports/safeguarding/pdf?armId={armId}", jar);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = armId.ToString("D", CultureInfo.InvariantCulture);
        var audits = await context.AuditEvents.AsNoTracking().Where(audit => audit.EntityId == id && audit.Action == "pupil.safeguarding.sheet")
            .Select(audit => audit.AfterJson).ToListAsync(TestContext.Current.CancellationToken);
        audits.Count.ShouldBe(2);
        audits.ShouldAllBe(json => json != null && !json.Contains("Groundnuts") && !json.Contains("Okafor"));
    }

    [Fact]
    public async Task Sheet_ForAnUnknownArm_Is404_AndUnauthenticatedIs401()
    {
        RequireDatabase();
        var jar = await SignInAsync();

        (await GetAsync($"/api/v1/reports/safeguarding?armId={Guid.NewGuid()}", jar)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetAsync($"/api/v1/reports/safeguarding?armId={Guid.NewGuid()}", new CookieJar())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>An arm with one fully recorded active pupil, plus a pupil in another arm who must not appear.</summary>
    private async Task<(Guid ArmId, Guid OtherPupilId)> SeedArmWithPupilAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;
        var year = Interlocked.Increment(ref _nextYear);
        var session = AcademicSession.Create(Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 14), new DateOnly(year + 1, 7, 25)).Value;
        context.Add(session);
        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        var otherArm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "B", null, null).Value;
        context.AddRange(arm, otherArm);

        var pupil = NewPupil("Okafor");
        var other = NewPupil("Bello");
        context.AddRange(pupil, other);
        context.AddRange(
            SchoolManagement.Domain.Enrolments.Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, new DateOnly(year, 9, 14)).Value,
            SchoolManagement.Domain.Enrolments.Enrolment.Open(Guid.CreateVersion7(), other.Id, otherArm.Id, new DateOnly(year, 9, 14)).Value);

        var health = PupilHealth.Create(pupil.Id);
        health.Apply(true, "Groundnuts", false, null, null, null, null, "St. Charles Borromeo", "08037776666", null, null).IsSuccess.ShouldBeTrue();
        context.Add(health);
        context.Add(AuthorisedPickupPerson.Create(Guid.CreateVersion7(), pupil.Id, "Ngozi Okafor", "Aunt", "08059876543", 0).Value);
        context.Add(BarredPersonAnswer.Create(pupil.Id, hasBarredPersons: true));
        context.Add(BarredPerson.Create(Guid.CreateVersion7(), pupil.Id, "Emeka Obi", null, 0).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id IN ({pupil.Id}, {other.Id})", TestContext.Current.CancellationToken);
        return (arm.Id, other.Id);
    }

    private static Pupil NewPupil(string surname) => Pupil.Create(
        Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female, new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9),
        nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;

    private async Task<CookieJar> SignInAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync("/api/v1/auth/csrf", jar);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
}
