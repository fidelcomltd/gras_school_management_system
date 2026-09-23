using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Spec 6.5.5 to 6.5.8 and 6.5.12: contacts, the pickup and barred lists, health, the document checklist and the
/// completeness report, against a PENDING admission (which has no arm, so every privilege is checked in the handler).
/// Signs in as a seeded Super Admin, who holds every privilege school-wide.
/// </summary>
public sealed class PupilRecordEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    private static int _nextYear = 7400;

    [Fact]
    public async Task Contacts_SaveAndRead_StoreCanonicalPhones_AndRejectTwoContactsForOneRole()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();

        var saved = await PutAsync($"/api/v1/pupils/{pupilId}/contacts", jar, new
        {
            contacts = new object[]
            {
                Contact("Father", "Emeka Okafor", null, "08031234567", primary: true),
                Contact("EmergencyPrimary", "Ngozi Okafor", "Aunt", "+2348059876543", primary: false),
            },
        });
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);

        var read = await ReadAsync<PupilContactListDto>(await GetAsync($"/api/v1/pupils/{pupilId}/contacts", jar));
        read.Items.Select(contact => contact.Role).ShouldBe([ContactRole.Father, ContactRole.EmergencyPrimary]);
        read.Items[0].Phone.ShouldBe("+2348031234567");
        read.Items[0].IsPrimaryContact.ShouldBeTrue();

        var duplicate = await PutAsync($"/api/v1/pupils/{pupilId}/contacts", jar, new
        {
            contacts = new object[] { Contact("Father", "Emeka Okafor", null, "08031234567", true), Contact("Father", "Obi Okafor", null, "08031234568", false) },
        });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Contacts_AnActivePupilCannotLoseItsLastResponsibleAdult()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();
        await PutAsync($"/api/v1/pupils/{pupilId}/contacts", jar, new { contacts = new object[] { Contact("Mother", "Ada Okafor", null, "08031234567", true) } });
        await using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = {pupilId}", TestContext.Current.CancellationToken);
        }

        var response = await PutAsync($"/api/v1/pupils/{pupilId}/contacts", jar, new
        {
            contacts = new object[] { Contact("EmergencyPrimary", "Ngozi Okafor", "Aunt", "08059876543", false) },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadAsStringAsync(response)).ShouldContain("contact.last_responsible_adult");
    }

    [Fact]
    public async Task Contacts_ANullList_Is422_AndAnActivePupilWithNoAdultYetCanStillGainAnEmergencyContact()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();

        (await PutAsync($"/api/v1/pupils/{pupilId}/contacts", jar, new { contacts = (object[]?)null })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await PutAsync($"/api/v1/pupils/{pupilId}/pickup-persons", jar, new { persons = (object[]?)null })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        await using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = {pupilId}", TestContext.Current.CancellationToken);
        }

        (await PutAsync($"/api/v1/pupils/{pupilId}/contacts", jar, new { contacts = new object[] { Contact("EmergencyPrimary", "Ngozi Okafor", "Aunt", "08059876543", false) } }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_AYesNeedsItsDetails_AndEveryReadIsAudited()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();

        var unanswered = await ReadAsync<PupilHealthDto>(await GetAsync($"/api/v1/pupils/{pupilId}/health", jar));
        unanswered.HasAllergy.ShouldBeNull();

        (await PutAsync($"/api/v1/pupils/{pupilId}/health", jar, Health(hasAllergy: true, details: null))).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await PutAsync($"/api/v1/pupils/{pupilId}/health", jar, Health(hasAllergy: true, details: "Peanuts"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PupilHealthDto>(await GetAsync($"/api/v1/pupils/{pupilId}/health", jar))).AllergyDetails.ShouldBe("Peanuts");

        await using var scope = Fixture.CreateScope();
        var reads = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AuditEvents.AsNoTracking()
            .CountAsync(audit => audit.Action == "pupil.safeguarding.read" && audit.EntityId == pupilId.ToString(), TestContext.Current.CancellationToken);
        reads.ShouldBe(2);
    }

    [Fact]
    public async Task BarredPersons_AYesNeedsAName_AndANoClearsThem()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();

        (await PutAsync($"/api/v1/pupils/{pupilId}/barred-persons", jar, new { hasBarredPersons = true, persons = Array.Empty<object>() }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await PutAsync($"/api/v1/pupils/{pupilId}/barred-persons", jar, new { hasBarredPersons = true, persons = new[] { new { fullName = "John Doe", details = "Court order" } } }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutAsync($"/api/v1/pupils/{pupilId}/barred-persons", jar, new { hasBarredPersons = false, persons = Array.Empty<object>() }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var read = await ReadAsync<BarredPersonsDto>(await GetAsync($"/api/v1/pupils/{pupilId}/barred-persons", jar));
        read.HasBarredPersons.ShouldBe(false);
        read.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Completeness_ListsTheBlockingSteps_UntilEachIsAnswered_AndDocumentsAreOnlyChased()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();

        var before = await ReadAsync<AdmissionCompletenessDto>(await GetAsync($"/api/v1/admissions/{pupilId}/completeness", jar));
        before.Blocking.Select(item => item.Step).Distinct().Order().ShouldBe([3, 4, 5, 8]);

        await PutAsync($"/api/v1/pupils/{pupilId}/contacts", jar, new
        {
            contacts = new object[] { Contact("Father", "Emeka Okafor", null, "08031234567", true), Contact("EmergencyPrimary", "Ngozi Okafor", "Aunt", "08059876543", false) },
        });
        await PutAsync($"/api/v1/pupils/{pupilId}/barred-persons", jar, new { hasBarredPersons = false, persons = Array.Empty<object>() });
        await PutAsync($"/api/v1/pupils/{pupilId}/health", jar, Health(hasAllergy: false, details: null));
        var ticked = await PutAsync($"/api/v1/pupils/{pupilId}/documents/BirthCertificate", jar, new { received = true, receivedDate = (string?)null, remarks = "Photocopy", otherLabel = (string?)null });
        ticked.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PupilDocumentListDto>(ticked)).Items[0].Received.ShouldBeTrue();

        var after = await ReadAsync<AdmissionCompletenessDto>(await GetAsync($"/api/v1/admissions/{pupilId}/completeness", jar));
        after.Blocking.Select(item => item.Code).ShouldBe(["declaration.unsigned"]); // Steps 3 to 5 done; the declaration is not.
        after.Chased.ShouldNotContain(item => item.Code == "documents.BirthCertificate");
        after.ChasedPercent.ShouldBeGreaterThan(before.ChasedPercent);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();

        (await GetAsync($"/api/v1/pupils/{pupilId}/health", new CookieJar())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Seeding and HTTP helpers ----------------------------------------------------------------

    private static object Contact(string role, string name, string? relationship, string phone, bool primary) => new
    {
        role,
        fullName = name,
        relationship,
        phone,
        whatsappNumber = (string?)null,
        occupation = (string?)null,
        email = (string?)null,
        isPrimaryContact = primary,
    };

    private static object Health(bool hasAllergy, string? details) => new
    {
        hasAllergy,
        allergyDetails = details,
        hasMedicalCondition = false,
        medicalConditionDetails = (string?)null,
        takesRegularMedication = false,
        medicationDetails = (string?)null,
        specialInstructions = (string?)null,
        preferredHospital = (string?)null,
        hospitalPhone = (string?)null,
        bloodGroup = (string?)null,
        genotype = (string?)null,
    };

    /// <summary>A pending pupil with its admission record: no enrolment, no arm, exactly as step 1 leaves it.</summary>
    private async Task<Guid> SeedPendingAdmissionAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;
        var year = Interlocked.Increment(ref _nextYear);
        var session = SchoolManagement.Domain.Sessions.AcademicSession.Create(
            Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 30)).Value;
        context.Add(session);
        var admitted = new DateOnly(2026, 9, 10);
        var pupil = Pupil.Create(
            Guid.CreateVersion7(), "Okafor", "Chidera", middleName: null, PupilSex.Female, new DateOnly(2018, 1, 1), asOfDate: admitted,
            nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
        var record = AdmissionRecord.Create(
            Guid.CreateVersion7(), pupil.Id, session.Id, dateApplicationReceived: null, admitted, asOfDate: admitted, levelId, AdmissionType.New,
            admissionTypeNote: null, assessmentRequired: false).Value;
        context.AddRange(pupil, record);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return pupil.Id;
    }

    private async Task<CookieJar> SignInAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        using var request = new HttpRequestMessage(HttpMethod.Post, SignInUrl) { Content = JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)) };
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

    private async Task<HttpResponseMessage> PutAsync<T>(string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private static Task<string> ReadAsStringAsync(HttpResponseMessage response) => response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
