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
/// End-to-end proof of TASK-0083 stage 1's trait-rating endpoints (spec §6.7.7, §6.7.12 amendment):
/// ruling R1's section gate, Q1-A's omitted-vs-null cell semantics, the first-save state transition,
/// staleness, the lock states, and that removing a rated trait via <c>PUT /settings/traits</c> now
/// 409s (<c>ITraitUsageGate</c> made real). Signs in as a real seeded Super Admin throughout — every
/// route's privilege is arm-scoped, and a Super Admin holds every privilege school-wide.
/// </summary>
public sealed class TraitRatingEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SettingsUrl = "/api/v1/settings";
    private const string TraitsUrl = "/api/v1/settings/traits";

    private static readonly Guid PunctualityTraitId = TraitConfiguration.AllSeededIds[1];
    private static readonly Guid NeedsImprovementPointId = RatingScalePointConfiguration.AllSeededIds[4];
    private static readonly Guid ImprovingPointId = RatingScalePointConfiguration.AllSeededIds[5];
    private static readonly Guid ExcellentPointId = RatingScalePointConfiguration.AllSeededIds[6];
    private static readonly Guid NurseryDevelopmentPointId = RatingScalePointConfiguration.AllSeededIds[0];

    // ---- GET ---------------------------------------------------------------------------------

    [Fact]
    public async Task Get_WithNoRatingsEnteredAtAll_ReturnsEveryActivePupilBlank()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        await SeedPupilOnRosterAsync(armId, "Adeyemi");
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetGridAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<TraitRatingSheetDto>(response);
        body.Version.ShouldBeNull();
        body.ResultSet.ShouldBeNull();
        body.Blocks.ShouldContain(block => block.Domain == TraitDomain.Affective);
        body.Rows.Count.ShouldBe(1);
        body.Rows[0].Ratings[PunctualityTraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)].ShouldBeNull();
    }

    [Fact]
    public async Task Get_ForANurseryArm_Returns422SectionNotRated()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetGridAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("trait_ratings.section_not_rated");
    }

    // ---- Save: first-save transition, Q1-A cell semantics -------------------------------------

    [Fact]
    public async Task Save_FirstSaveForTheArm_CreatesTheResultSetInDraftWithNeedsRecomputeTrue()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Bello");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (PunctualityTraitId, ExcellentPointId)));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<TraitRatingSheetDto>(response);
        body.ResultSet.ShouldNotBeNull();
        body.ResultSet!.State.ShouldBe(ResultSetState.Draft);
        body.ResultSet!.NeedsRecompute.ShouldBeTrue();
        body.Version.ShouldNotBeNull();
    }

    [Fact]
    public async Task Save_AnOmittedTraitKey_LeavesAPreviouslySavedRatingUntouched()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Chukwu");
        var jar = await SignInAsSuperAdminAsync();
        var conductTraitId = TraitConfiguration.AllSeededIds[0];

        var first = await SaveGridAsync(
            armId, jar, termId, version: null,
            Row(pupilId, (PunctualityTraitId, ExcellentPointId), (conductTraitId, NeedsImprovementPointId)));
        var firstBody = await ReadAsync<TraitRatingSheetDto>(first);

        // Second save touches ONLY Punctuality — Conduct's key is entirely absent from the payload.
        var second = await SaveGridAsync(
            armId, jar, termId, firstBody.Version, Row(pupilId, (PunctualityTraitId, ImprovingPointId)));

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await ReadAsync<TraitRatingSheetDto>(second);
        var row = secondBody.Rows.Single();
        row.Ratings[PunctualityTraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)]
            .ShouldBe(ImprovingPointId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        row.Ratings[conductTraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)]
            .ShouldBe(NeedsImprovementPointId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Save_AnExplicitNull_ClearsAPreviouslySavedRatingAndDeletesTheRow()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Danladi");
        var jar = await SignInAsSuperAdminAsync();

        var first = await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (PunctualityTraitId, ExcellentPointId)));
        var firstBody = await ReadAsync<TraitRatingSheetDto>(first);

        var second = await SaveGridAsync(
            armId, jar, termId, firstBody.Version,
            new { pupilId = pupilId.ToString(), ratings = new Dictionary<string, string?> { [PunctualityTraitId.ToString()] = null } });

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await ReadAsync<TraitRatingSheetDto>(second);
        secondBody.Version.ShouldBeNull();
        secondBody.Rows.Single().Ratings[PunctualityTraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)].ShouldBeNull();

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Set<TraitRating>().AsNoTracking().AnyAsync(
            rating => rating.PupilId == pupilId, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Save_WithAStaleVersion_Returns409()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Femi");
        var jar = await SignInAsSuperAdminAsync();

        await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (PunctualityTraitId, ExcellentPointId)));

        var retry = await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (PunctualityTraitId, ImprovingPointId)));

        retry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(retry);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("trait_ratings.stale_version");
    }

    [Fact]
    public async Task Save_OnceTheResultSetIsApproved_Returns409Locked()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Grace");
        var jar = await SignInAsSuperAdminAsync();
        await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (PunctualityTraitId, ExcellentPointId)));
        await SetResultSetStateAsync(armId, termId, ResultSetState.Approved);

        var response = await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (PunctualityTraitId, ImprovingPointId)));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("trait_ratings.result_set_locked");
    }

    // ---- Save: per-cell validation --------------------------------------------------------------

    [Fact]
    public async Task Save_APointFromTheWrongScale_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Halima");
        var jar = await SignInAsSuperAdminAsync();

        // NurseryDevelopmentPointId belongs to the Nursery development scale, not Primary trait.
        var response = await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (PunctualityTraitId, NurseryDevelopmentPointId)));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("is not on Primary trait");
    }

    [Fact]
    public async Task Save_APupilNotOnTheRoster_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveGridAsync(
            armId, jar, termId, version: null, Row(Guid.CreateVersion7(), (PunctualityTraitId, ExcellentPointId)));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("not on the arm's active roster");
    }

    // ---- Usage gate: ITraitUsageGate made real -------------------------------------------------

    [Fact]
    public async Task RemovingARatedTrait_ViaSettingsTraits_Returns409()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Ibrahim");
        var jar = await SignInAsSuperAdminAsync();
        await SaveGridAsync(armId, jar, termId, version: null, Row(pupilId, (PunctualityTraitId, ExcellentPointId)));

        var settings = await ReadAsync<SettingsDto>(await GetSettingsAsync(jar));
        var withoutPunctuality = settings.Traits.Traits
            .Where(trait => trait.Id != PunctualityTraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture))
            .Select(trait => new TraitInput(trait.Domain, trait.Name, trait.DisplayOrder, trait.Status, Guid.Parse(trait.Id)))
            .ToList();
        var command = new UpdateTraitsCommand(
            Guid.Parse(settings.Traits.AffectiveRatingScaleId), Guid.Parse(settings.Traits.PsychomotorRatingScaleId),
            withoutPunctuality, settings.Traits.VersionNumber, Reason: null);

        var response = await PutAsync(TraitsUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.traits.trait_rated");
    }

    // ---- Seeding and HTTP helpers ----------------------------------------------------------------

    private static int _nextSessionStartYear = 5000;

    private static object Row(Guid pupilId, params (Guid TraitId, Guid PointId)[] ratings) => new
    {
        pupilId = pupilId.ToString(),
        ratings = ratings.ToDictionary(entry => entry.TraitId.ToString(), entry => (string?)entry.PointId.ToString()),
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
            new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9), nationality: null,
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
        GetAsyncCore($"/api/v1/arms/{armId}/trait-ratings?termId={termId}", jar);

    private Task<HttpResponseMessage> GetSettingsAsync(CookieJar jar) => GetAsyncCore(SettingsUrl, jar);

    private Task<HttpResponseMessage> SaveGridAsync(Guid armId, CookieJar jar, Guid termId, string? version, params object[] rows) =>
        Client.SendAsync(
            BuildPutRequest($"/api/v1/arms/{armId}/trait-ratings", jar, new { termId = termId.ToString(), version, rows }),
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
