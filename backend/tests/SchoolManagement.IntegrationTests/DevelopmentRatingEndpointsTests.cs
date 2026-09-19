using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Results;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Configurations;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0083 stage 2's development-rating endpoints (spec §6.7.7, §6.7.12
/// amendment, Appendix E.3): ruling R1's section gate (a PRIMARY arm gets refused, mirroring stage
/// 1's nursery-arm proof of the trait sheet's own gate), Q1-A's omitted-vs-null cell semantics, Q3-A's
/// comment rules, the first-save state transition, staleness, the lock states, and that removing a
/// rated indicator via <c>PUT /settings/development-domains</c> now 409s
/// (<c>IDevelopmentIndicatorUsageGate</c> made real). Signs in as a real seeded Super Admin throughout
/// — every route's privilege is arm-scoped, and a Super Admin holds every privilege school-wide.
/// </summary>
public sealed class DevelopmentRatingEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SettingsUrl = "/api/v1/settings";
    private const string DevelopmentDomainsUrl = "/api/v1/settings/development-domains";

    // Domain 1 (Maths Readiness), the first two of its four seeded indicators, on the Nursery section.
    private static readonly Guid AbilityToCountId = DevelopmentIndicatorConfiguration.AllSeededIds[0];
    private static readonly Guid WriteNumberClearlyId = DevelopmentIndicatorConfiguration.AllSeededIds[1];

    // The Nursery development scale's four points, order 1..4 (N, I, S, E).
    private static readonly Guid ImprovingPointId = RatingScalePointConfiguration.AllSeededIds[1];
    private static readonly Guid SatisfiedPointId = RatingScalePointConfiguration.AllSeededIds[2];
    private static readonly Guid ExcellentPointId = RatingScalePointConfiguration.AllSeededIds[3];
    private static readonly Guid PrimaryTraitPointId = RatingScalePointConfiguration.AllSeededIds[4];

    // ---- GET ---------------------------------------------------------------------------------

    [Fact]
    public async Task Get_WithNoRatingsEnteredAtAll_ReturnsEveryActivePupilBlank()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        await SeedPupilOnRosterAsync(armId, "Adeyemi");
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetGridAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<DevelopmentRatingSheetDto>(response);
        body.Version.ShouldBeNull();
        body.ResultSet.ShouldBeNull();
        body.Domains.ShouldContain(domain => domain.Name == DevelopmentDomainSeed.MathsReadinessName);
        body.ActiveIndicatorTotal.ShouldBe(45);
        body.Rows.Count.ShouldBe(1);
        var cell = body.Rows[0].Ratings[AbilityToCountId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)];
        cell.PointId.ShouldBeNull();
        cell.Comment.ShouldBeNull();
    }

    [Fact]
    public async Task Get_ForAPrimaryArm_Returns422SectionNotRated()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetGridAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("development_ratings.section_not_rated");
    }

    // ---- Save: first-save transition, Q1-A cell semantics -------------------------------------

    [Fact]
    public async Task Save_FirstSaveForTheArm_CreatesTheResultSetInDraftWithNeedsRecomputeTrue()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Bello");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveGridAsync(
            armId, jar, termId, version: null, Row(pupilId, (AbilityToCountId, Cell(ExcellentPointId, "Counts to twenty."))));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<DevelopmentRatingSheetDto>(response);
        body.ResultSet.ShouldNotBeNull();
        body.ResultSet!.State.ShouldBe(ResultSetState.Draft);
        body.ResultSet!.NeedsRecompute.ShouldBeTrue();
        body.Version.ShouldNotBeNull();
        var cell = body.Rows.Single().Ratings[AbilityToCountId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)];
        cell.PointId.ShouldBe(ExcellentPointId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        cell.Comment.ShouldBe("Counts to twenty.");
    }

    [Fact]
    public async Task Save_AnOmittedIndicatorKey_LeavesAPreviouslySavedRatingUntouched()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Chukwu");
        var jar = await SignInAsSuperAdminAsync();

        var first = await SaveGridAsync(
            armId, jar, termId, version: null,
            Row(pupilId, (AbilityToCountId, Cell(ExcellentPointId)), (WriteNumberClearlyId, Cell(SatisfiedPointId))));
        var firstBody = await ReadAsync<DevelopmentRatingSheetDto>(first);

        // Second save touches ONLY AbilityToCount — WriteNumberClearly's key is entirely absent.
        var second = await SaveGridAsync(
            armId, jar, termId, firstBody.Version, Row(pupilId, (AbilityToCountId, Cell(ImprovingPointId))));

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await ReadAsync<DevelopmentRatingSheetDto>(second);
        var row = secondBody.Rows.Single();
        row.Ratings[AbilityToCountId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)].PointId
            .ShouldBe(ImprovingPointId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        row.Ratings[WriteNumberClearlyId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)].PointId
            .ShouldBe(SatisfiedPointId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Save_AnExplicitNull_ClearsAPreviouslySavedRatingAndDeletesTheRow()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Danladi");
        var jar = await SignInAsSuperAdminAsync();

        var first = await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (AbilityToCountId, Cell(ExcellentPointId, "Good."))));
        var firstBody = await ReadAsync<DevelopmentRatingSheetDto>(first);

        var second = await SaveGridAsync(
            armId, jar, termId, firstBody.Version,
            new { pupilId = pupilId.ToString(), ratings = new Dictionary<string, object?> { [AbilityToCountId.ToString()] = null } });

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await ReadAsync<DevelopmentRatingSheetDto>(second);
        secondBody.Version.ShouldBeNull();
        var cell = secondBody.Rows.Single().Ratings[AbilityToCountId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)];
        cell.PointId.ShouldBeNull();
        cell.Comment.ShouldBeNull();

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Set<DevelopmentRating>().AsNoTracking().AnyAsync(
            rating => rating.PupilId == pupilId, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Save_WithAStaleVersion_Returns409()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Femi");
        var jar = await SignInAsSuperAdminAsync();

        await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (AbilityToCountId, Cell(ExcellentPointId))));

        var retry = await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (AbilityToCountId, Cell(ImprovingPointId))));

        retry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(retry);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("development_ratings.stale_version");
    }

    [Fact]
    public async Task Save_OnceTheResultSetIsApproved_Returns409Locked()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Grace");
        var jar = await SignInAsSuperAdminAsync();
        await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (AbilityToCountId, Cell(ExcellentPointId))));
        await SetResultSetStateAsync(armId, termId, ResultSetState.Approved);

        var response = await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (AbilityToCountId, Cell(ImprovingPointId))));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("development_ratings.result_set_locked");
    }

    // ---- Save: per-cell validation --------------------------------------------------------------

    [Fact]
    public async Task Save_ACommentWithoutAPoint_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Comfort");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveGridAsync(
            armId, jar, termId, version: null,
            new
            {
                pupilId = pupilId.ToString(),
                ratings = new Dictionary<string, object?>
                {
                    [AbilityToCountId.ToString()] = new { pointId = (string?)null, comment = "Needs help counting." },
                },
            });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("A comment requires a rating");
    }

    [Fact]
    public async Task Save_APointFromTheWrongScale_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Halima");
        var jar = await SignInAsSuperAdminAsync();

        // PrimaryTraitPointId belongs to the Primary trait scale, not Nursery development.
        var response = await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (AbilityToCountId, Cell(PrimaryTraitPointId))));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("is not on Nursery development");
    }

    [Fact]
    public async Task Save_APupilNotOnTheRoster_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveGridAsync(
            armId, jar, termId, version: null, Row(Guid.CreateVersion7(), (AbilityToCountId, Cell(ExcellentPointId))));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("not on the arm's active roster");
    }

    // ---- Usage gate: IDevelopmentIndicatorUsageGate made real ----------------------------------

    [Fact]
    public async Task RemovingARatedIndicator_ViaSettingsDevelopmentDomains_Returns409()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Ibrahim");
        var jar = await SignInAsSuperAdminAsync();
        await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (AbilityToCountId, Cell(ExcellentPointId))));

        var settings = await ReadAsync<SettingsDto>(await GetSettingsAsync(jar));
        var withoutAbilityToCount = settings.DevelopmentDomains.Domains
            .Select(domain => domain.Name == DevelopmentDomainSeed.MathsReadinessName
                ? domain with { Indicators = domain.Indicators.Where(i => i.Id != AbilityToCountId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)).ToList() }
                : domain)
            .Select(ToInput)
            .ToList();
        var command = new UpdateDevelopmentDomainsCommand(withoutAbilityToCount, settings.DevelopmentDomains.VersionNumber, Reason: null);

        var response = await PutAsync(DevelopmentDomainsUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.developmentdomains.indicator_rated");
    }

    private static DevelopmentDomainInput ToInput(DevelopmentDomainDto domain) => new(
        Guid.Parse(domain.SectionId),
        domain.Name,
        domain.DisplayOrder,
        Guid.Parse(domain.RatingScaleId),
        domain.AllowsIndicatorComment,
        domain.Status,
        domain.Indicators.Select(ToInput).ToList(),
        Guid.Parse(domain.Id));

    private static DevelopmentIndicatorInput ToInput(DevelopmentIndicatorDto indicator) =>
        new(indicator.Name, indicator.DisplayOrder, indicator.Status, Guid.Parse(indicator.Id));

    // ---- Seeding and HTTP helpers ----------------------------------------------------------------

    private static int _nextSessionStartYear = 6000;

    private static object Cell(Guid pointId, string? comment = null) => new { pointId = pointId.ToString(), comment };

    private static object Row(Guid pupilId, params (Guid IndicatorId, object Cell)[] cells) => new
    {
        pupilId = pupilId.ToString(),
        ratings = cells.ToDictionary(entry => entry.IndicatorId.ToString(), entry => entry.Cell),
    };

    /// <summary>Seeds an active session, an open (Upcoming) term, and one arm under a level belonging to <paramref name="sectionId"/>.</summary>
    private async Task<(Guid ArmId, Guid TermId, Guid SessionId)> SeedArmAsync(Guid sectionId)
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
        context.Add(term);

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.Add(arm);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (arm.Id, term.Id, session.Id);
    }

    private async Task<Guid> SeedPupilOnRosterAsync(Guid armId, string surname)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female,
            new DateOnly(2022, 1, 1), asOfDate: new DateOnly(2026, 9, 9), nationality: null,
            "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;
        context.Add(pupil);

        var enrolment = SchoolManagement.Domain.Enrolments.Enrolment.Open(
            Guid.CreateVersion7(), pupil.Id, armId, new DateOnly(2026, 9, 14)).Value;
        context.Add(enrolment);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = {pupil.Id}",
            TestContext.Current.CancellationToken);
        return pupil.Id;
    }

    /// <summary>Sets the result set's state directly — no state-machine endpoint exists yet (later cards).</summary>
    private async Task SetResultSetStateAsync(Guid armId, Guid termId, ResultSetState state)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.SingleAsync(
            r => r.ArmId == armId && r.TermId == termId, TestContext.Current.CancellationToken);
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsyncCore(CsrfUrl, jar);
        var signIn = await PostAsyncCore(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> GetGridAsync(Guid armId, Guid termId, CookieJar jar) =>
        GetAsyncCore($"/api/v1/arms/{armId}/development-ratings?termId={termId}", jar);

    private Task<HttpResponseMessage> GetSettingsAsync(CookieJar jar) => GetAsyncCore(SettingsUrl, jar);

    private Task<HttpResponseMessage> SaveGridAsync(Guid armId, CookieJar jar, Guid termId, string? version, params object[] rows) =>
        Client.SendAsync(
            BuildPutRequest($"/api/v1/arms/{armId}/development-ratings", jar, new { termId = termId.ToString(), version, rows }),
            TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> PutAsync<T>(string url, CookieJar jar, T payload) =>
        Client.SendAsync(BuildPutRequest(url, jar, payload), TestContext.Current.CancellationToken);

    private static HttpRequestMessage BuildPutRequest<T>(string url, CookieJar jar, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(payload),
        };
        jar.ApplyWithCsrf(request);
        return request;
    }

    private Task<HttpResponseMessage> GetAsyncCore(string url, CookieJar jar) => SendAsyncCore(Client, HttpMethod.Get, url, jar, jar.Apply);

    private Task<HttpResponseMessage> PostAsyncCore<T>(string url, CookieJar jar, T payload) =>
        SendWithCsrfAsync(Client, HttpMethod.Post, url, jar, payload);

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
