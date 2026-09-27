using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Fees;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Fees;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Spec 6.2.13 end to end: the section grid (seeded defaults, save, rename, reorder, remove), the per-arm outstanding figures
/// with their published lock, and <c>fee.manage</c> gating.
/// </summary>
public sealed class FeeNoticeEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private static readonly Guid Primary1Id = new("00000000-0000-0000-0000-000000000314");
    private static readonly Guid Primary2Id = new("00000000-0000-0000-0000-000000000315");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Grid_OffersTheSeededLines_SavesThem_ThenRenamesReordersAndRemoves()
    {
        RequireDatabase();
        var seeded = await SeedAsync();
        var jar = await SignInAsync(superAdmin: true);
        var url = $"/api/v1/fee-notices?sectionId={SeededClassLevels.PrimarySectionId}&termId={seeded.TermId}";

        var fresh = await ReadAsync<FeeNoticeGridDto>(await SendAsync(HttpMethod.Get, url, jar, null));
        fresh.IsDefault.ShouldBeTrue();
        fresh.Lines.Select(line => line.Label).ShouldBe(["Tuition Fee", "Exam & PTA", "Books", "Toiletries", "Party Fee", "Outstanding Fee"]);
        fresh.Levels.Select(level => level.Name).ShouldContain("Primary 1");

        var first = new SaveFeeNoticeGridCommand(SeededClassLevels.PrimarySectionId, seeded.TermId,
        [
            .. fresh.Lines.Select(line => new FeeGridLineInput(
                null, line.Label, line.Kind, false,
                line.Kind == FeeLabelKind.Amount ? [new(Primary1Id, line.Label == "Tuition Fee" ? 45000 : 0), new(Primary2Id, null)] : [])),
        ]);
        var saved = await ReadAsync<FeeNoticeGridDto>(await SendAsync(HttpMethod.Put, "/api/v1/fee-notices", jar, first));
        saved.IsDefault.ShouldBeFalse();
        saved.Lines.Single(line => line.Label == "Tuition Fee").Amounts.Single().ShouldBe(new FeeGridAmountDto(Primary1Id, 45000));

        // Rename Books, drop Party Fee, move Outstanding to the top and switch it on for the portal; clear Tuition's cell.
        var lines = saved.Lines.ToDictionary(line => line.Label);
        var second = new SaveFeeNoticeGridCommand(SeededClassLevels.PrimarySectionId, seeded.TermId,
        [
            new(lines["Outstanding Fee"].Id, "Outstanding Fee", FeeLabelKind.Outstanding, true, []),
            new(lines["Tuition Fee"].Id, "Tuition Fee", FeeLabelKind.Amount, false, [new(Primary1Id, null)]),
            new(lines["Books"].Id, "Books & Stationery", FeeLabelKind.Amount, false, [new(Primary1Id, 8000)]),
            new(lines["Exam & PTA"].Id, "Exam & PTA", FeeLabelKind.Amount, false, []),
            new(lines["Toiletries"].Id, "Toiletries", FeeLabelKind.Amount, false, []),
        ]);
        (await SendAsync(HttpMethod.Put, "/api/v1/fee-notices", jar, second)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var reread = await ReadAsync<FeeNoticeGridDto>(await SendAsync(HttpMethod.Get, url, jar, null));
        reread.Lines.Select(line => line.Label).ShouldBe(["Outstanding Fee", "Tuition Fee", "Books & Stationery", "Exam & PTA", "Toiletries"]);
        reread.Lines[0].ShowOnPortal.ShouldBeTrue();
        reread.Lines[1].Amounts.ShouldBeEmpty();
        reread.Lines[2].Amounts.Single().Amount.ShouldBe(8000);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.FeeAmounts.AnyAsync(amount => amount.FeeLabelId == lines["Party Fee"].Id!.Value, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Grid_RefusesAmountsOnTheOutstandingLine_AndASecondOutstandingLine()
    {
        RequireDatabase();
        var seeded = await SeedAsync();
        var jar = await SignInAsync(superAdmin: true);

        var amountOnOutstanding = new SaveFeeNoticeGridCommand(SeededClassLevels.PrimarySectionId, seeded.TermId,
            [new(null, "Outstanding Fee", FeeLabelKind.Outstanding, false, [new(Primary1Id, 1000)])]);
        (await SendAsync(HttpMethod.Put, "/api/v1/fee-notices", jar, amountOnOutstanding)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var twoOutstanding = new SaveFeeNoticeGridCommand(SeededClassLevels.PrimarySectionId, seeded.TermId,
            [new(null, "Outstanding Fee", FeeLabelKind.Outstanding, false, []), new(null, "Arrears", FeeLabelKind.Outstanding, false, [])]);
        (await SendAsync(HttpMethod.Put, "/api/v1/fee-notices", jar, twoOutstanding)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Outstanding_SavesFigures_ThenLocksOncePublished()
    {
        RequireDatabase();
        var seeded = await SeedAsync();
        var jar = await SignInAsync(superAdmin: true);
        var url = $"/api/v1/arms/{seeded.ArmId}/outstanding-fees";

        var empty = await ReadAsync<OutstandingFeeSheetDto>(await SendAsync(HttpMethod.Get, $"{url}?termId={seeded.TermId}", jar, null));
        empty.Rows.Count.ShouldBe(2);
        empty.Rows.ShouldAllBe(row => row.Amount == null);
        empty.Locked.ShouldBeFalse();

        var save = new SaveOutstandingFeesCommand(seeded.ArmId, seeded.TermId, [new(seeded.PupilIds[0], 12500), new(seeded.PupilIds[1], null)]);
        var saved = await ReadAsync<OutstandingFeeSheetDto>(await SendAsync(HttpMethod.Put, url, jar, save));
        saved.Rows.Single(row => row.PupilId == seeded.PupilIds[0]).Amount.ShouldBe(12500);
        saved.Rows.Single(row => row.PupilId == seeded.PupilIds[1]).Amount.ShouldBeNull();

        // A pupil not on the roster is refused.
        var stranger = new SaveOutstandingFeesCommand(seeded.ArmId, seeded.TermId, [new(Guid.CreateVersion7(), 100)]);
        (await SendAsync(HttpMethod.Put, url, jar, stranger)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var set = await context.ResultSets.SingleAsync(r => r.ArmId == seeded.ArmId && r.TermId == seeded.TermId, TestContext.Current.CancellationToken);
            typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(set, ResultSetState.Published);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await ReadAsync<OutstandingFeeSheetDto>(await SendAsync(HttpMethod.Get, $"{url}?termId={seeded.TermId}", jar, null))).Locked.ShouldBeTrue();
        var refused = await SendAsync(HttpMethod.Put, url, jar, save with { Rows = [new(seeded.PupilIds[0], 10000)] });
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain(SaveOutstandingFeesHandlerCode);
    }

    [Fact]
    public async Task WithoutFeeManage_BothScreensAre403()
    {
        RequireDatabase();
        var seeded = await SeedAsync();
        var jar = await SignInAsync(superAdmin: false);

        (await SendAsync(HttpMethod.Get, $"/api/v1/fee-notices?sectionId={SeededClassLevels.PrimarySectionId}&termId={seeded.TermId}", jar, null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"/api/v1/arms/{seeded.ArmId}/outstanding-fees?termId={seeded.TermId}", jar, null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private const string SaveOutstandingFeesHandlerCode = "outstanding_fee.result_set_published";

    private sealed record Seeded(Guid TermId, Guid ArmId, IReadOnlyList<Guid> PupilIds);

    private async Task<Seeded> SeedAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 23)).Value;
        session.Activate();
        var term = Term.Create(Guid.CreateVersion7(), session.Id, 1, "First Term", session.StartDate, session.StartDate.AddMonths(3)).Value;
        var arm = Arm.Create(Guid.CreateVersion7(), Primary1Id, session.Id, "A", null, null).Value;
        context.AddRange(session, term, arm);
        var pupilIds = new List<Guid>();
        foreach (var surname in new[] { "Okafor", "Bello" })
        {
            var pupil = Pupil.Create(
                Guid.CreateVersion7(), surname, "Ada", middleName: null, PupilSex.Female, new DateOnly(2019, 3, 4), asOfDate: new DateOnly(2026, 9, 9),
                nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
            typeof(Pupil).GetProperty(nameof(Pupil.Status))!.SetValue(pupil, PupilStatus.Active);
            context.Add(pupil);
            context.Add(Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, session.StartDate).Value);
            pupilIds.Add(pupil.Id);
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Seeded(term.Id, arm.Id, pupilIds);
    }

    private async Task<CookieJar> SignInAsync(bool superAdmin)
    {
        var email = $"fees-{Guid.NewGuid():N}@example.com";
        if (superAdmin)
        {
            await AdminAccountSeeder.SeedAsync(Fixture, email: email);
        }
        else
        {
            await AdminAccountSeeder.SeedRegularAsync(Fixture, email: email);
        }

        var jar = new CookieJar();
        await SendAsync(HttpMethod.Get, CsrfUrl, jar, null);
        (await SendAsync(HttpMethod.Post, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password))).StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private static new async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue(body);
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, CookieJar jar, object? payload)
    {
        using var request = new HttpRequestMessage(method, url) { Content = payload is null ? null : JsonContent.Create(payload, options: Json) };
        if (method == HttpMethod.Get)
        {
            jar.Apply(request);
        }
        else
        {
            jar.ApplyWithCsrf(request);
        }

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
