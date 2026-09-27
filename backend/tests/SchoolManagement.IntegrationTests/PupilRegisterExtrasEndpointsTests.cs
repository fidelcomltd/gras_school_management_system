using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// The register's finishing items: the admission slip (spec 6.5.11), the list's completeness column (6.5.15) and duplicate
/// detection by surname plus a contact phone (6.5.11 step 1). Signs in as a seeded Super Admin.
/// </summary>
public sealed class PupilRegisterExtrasEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static int _nextYear = 8100;

    [Fact]
    public async Task AdmissionSlip_IsAPdfForAnAdmittedPupil_And409BeforeApproval()
    {
        RequireDatabase();
        var (admitted, pending, _) = await SeedAsync();
        var jar = await SignInAsync();

        var slip = await GetAsync($"/api/v1/pupils/{admitted}/admission-slip", jar);
        slip.StatusCode.ShouldBe(HttpStatusCode.OK);
        slip.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        slip.Content.Headers.ContentDisposition!.ToString().ShouldBe("attachment; filename=admission-slip.pdf");
        (await slip.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();

        var early = await GetAsync($"/api/v1/pupils/{pending}/admission-slip", jar);
        early.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Duplicates_MatchSurnamePlusAContactPhone_InEitherForm_OnlyWhenAPhoneIsGiven()
    {
        RequireDatabase();
        var (admitted, _, surname) = await SeedAsync();
        var jar = await SignInAsync();

        // A different first name and birthday: only the phone half of 6.5.11's detection can find it.
        var byPhone = await ReadAsync<List<PupilDto>>(await GetAsync(
            $"/api/v1/pupils/duplicates?surname={surname}&firstName=Emeka&dateOfBirth=2019-02-02&contactPhone=08031234567", jar));
        byPhone.ShouldContain(pupil => pupil.Id == admitted.ToString());

        var withoutPhone = await ReadAsync<List<PupilDto>>(await GetAsync(
            $"/api/v1/pupils/duplicates?surname={surname}&firstName=Emeka&dateOfBirth=2019-02-02", jar));
        withoutPhone.ShouldNotContain(pupil => pupil.Id == admitted.ToString());

        (await GetAsync($"/api/v1/pupils/duplicates?surname={surname}&firstName=Emeka&dateOfBirth=2019-02-02&contactPhone=12", jar))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // "%" is a literal, never a wildcard: the phone half stays a same-surname check, not a reverse phone lookup.
        var wildcard = await ReadAsync<List<PupilDto>>(await GetAsync(
            "/api/v1/pupils/duplicates?surname=%25&firstName=%25&dateOfBirth=2019-02-02&contactPhone=08031234567", jar));
        wildcard.ShouldNotContain(pupil => pupil.Id == admitted.ToString());
    }

    [Fact]
    public async Task List_CarriesEachRowsCompletenessPercentage()
    {
        RequireDatabase();
        var (admitted, _, surname) = await SeedAsync();
        var jar = await SignInAsync();

        var page = await ReadAsync<CursorPage<PupilDto>>(await GetAsync($"/api/v1/pupils?search={surname}", jar));

        var row = page.Items.ShouldHaveSingleItem();
        row.Id.ShouldBe(admitted.ToString());
        row.ChasedPercent.ShouldNotBeNull();
        row.ChasedPercent!.Value.ShouldBeInRange(0, 99); // a bare record: most of the chased set is missing
    }

    /// <summary>An admitted pupil (with a father's phone and an enrolment) and a pending admission, sharing a unique surname.</summary>
    private async Task<(Guid Admitted, Guid Pending, string Surname)> SeedAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;
        var year = Interlocked.Increment(ref _nextYear);
        // Names take letters only: the counter is spelt as letters so each test run gets its own surname.
        var surname = "Adeyemi" + string.Concat(year.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => (char)('a' + (digit - '0'))));
        var session = AcademicSession.Create(Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 14), new DateOnly(year + 1, 7, 25)).Value;
        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.AddRange(session, arm);

        var admitted = NewPupil(surname, "Tolu");
        var pending = NewPupil(surname, "Kemi");
        admitted.IssueRegistrationNumber($"GRA/{year}/0001");
        context.AddRange(admitted, pending);
        context.Add(SchoolManagement.Domain.Enrolments.Enrolment.Open(Guid.CreateVersion7(), admitted.Id, arm.Id, new DateOnly(year, 9, 14)).Value);
        var admittedOn = new DateOnly(2026, 9, 10);
        foreach (var pupil in new[] { admitted, pending })
        {
            context.Add(AdmissionRecord.Create(
                Guid.CreateVersion7(), pupil.Id, session.Id, dateApplicationReceived: null, admittedOn, asOfDate: admittedOn, levelId,
                AdmissionType.New, admissionTypeNote: null, assessmentRequired: false).Value);
        }

        var father = PupilContact.Create(Guid.CreateVersion7(), admitted.Id, ContactRole.Father);
        father.Apply("Bayo Adeyemi", null, "08031234567", null, null, null, isPrimary: true).IsSuccess.ShouldBeTrue();
        context.Add(father);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = {admitted.Id}", TestContext.Current.CancellationToken);
        return (admitted.Id, pending.Id, surname);
    }

    private static Pupil NewPupil(string surname, string firstName) => Pupil.Create(
        Guid.CreateVersion7(), surname, firstName, middleName: null, PupilSex.Female, new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9),
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
