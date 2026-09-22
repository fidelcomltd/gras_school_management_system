using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Pins;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>Spec 6.9 portal part 3a: lookup, use counting, resume, the 6.9.4 copies, the spread control and the blocks.</summary>
public sealed class PortalEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static int _nextYear = 9300;
    private static int _nextSerial = 100;

    [Fact]
    public async Task Lookup_WithAValidPinAndAPublishedResult_OpensASessionAndCountsOneUse()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        var stopwatch = Stopwatch.StartNew();
        var lookup = await LookupAsync(client, school.Pupils[0].TypedLoosely, pin.TypedLoosely);
        stopwatch.Stop();

        lookup.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        lookup.Headers.Location!.ToString().ShouldBe("/portal/terms");
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(390));
        var cookie = CookieFrom(lookup);

        var terms = await GetAsync(client, "/portal/terms", cookie);
        var html = await terms.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain(school.Pupils[0].Name);
        html.ShouldContain("Available");
        html.ShouldContain("You have 2 left");
        terms.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("style-src 'sha256-");
        terms.Headers.GetValues("Content-Security-Policy").Single().ShouldNotContain("script-src");

        var stored = await PinAsync(pin.Id);
        stored.UseCount.ShouldBe(1);
        stored.DistinctPupilCount.ShouldBe(1);
        stored.State.ShouldBe(PinState.Active);
    }

    [Fact]
    public async Task Lookup_AWrongPin_OrAnUnknownNumber_GetsTheSameCopy_AndSpendsNothing()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        var wrongPin = await ReadAsync(await LookupAsync(client, school.Pupils[0].Number, "ABCDE-FGHJK"));
        var unknownNumber = await ReadAsync(await LookupAsync(client, "NOPE-0000", pin.Value));

        wrongPin.ShouldContain("We could not find that result");
        unknownNumber.ShouldContain("We could not find that result");
        (await PinAsync(pin.Id)).UseCount.ShouldBe(0);
    }

    [Fact]
    public async Task Lookup_WhenNothingIsPublished_SaysSo_AndSpendsNothing()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: false);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        var html = await ReadAsync(await LookupAsync(client, school.Pupils[0].Number, pin.Value));

        html.ShouldContain("Results have not been released yet");
        (await PinAsync(pin.Id)).UseCount.ShouldBe(0);
    }

    [Fact]
    public async Task Lookup_AgainOnTheSameDeviceInsideTheWindow_ResumesWithoutASecondUse()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        var cookie = CookieFrom(await LookupAsync(client, school.Pupils[0].Number, pin.Value));
        var again = await LookupAsync(client, school.Pupils[0].Number, pin.Value, cookie);

        again.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await PinAsync(pin.Id)).UseCount.ShouldBe(1);
    }

    [Fact]
    public async Task Lookup_APinWithNoUsesLeft_SaysItIsUsedUp()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true, pupils: 2);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 1);
        using var client = NewClient();

        (await LookupAsync(client, school.Pupils[0].Number, pin.Value)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var html = await ReadAsync(await LookupAsync(client, school.Pupils[1].Number, pin.Value));

        html.ShouldContain("This pin has been used up");
        html.ShouldContain("used 1 times");
    }

    [Fact]
    public async Task Lookup_AThirdPupilWithinTenMinutes_SuspendsThePin()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true, pupils: 3);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 20);
        using var client = NewClient();

        (await LookupAsync(client, school.Pupils[0].Number, pin.Value)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await LookupAsync(client, school.Pupils[1].Number, pin.Value)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var html = await ReadAsync(await LookupAsync(client, school.Pupils[2].Number, pin.Value));

        html.ShouldContain("This pin needs to be checked");
        var stored = await PinAsync(pin.Id);
        stored.State.ShouldBe(PinState.Suspended);
        stored.UseCount.ShouldBe(2);
    }

    [Fact]
    public async Task Lookup_FiveMissesOnOneNumber_BlocksThatNumber()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await ReadAsync(await LookupAsync(client, school.Pupils[0].Number, "WRONG-PIN22"))).ShouldContain("We could not find that result");
        }

        var html = await ReadAsync(await LookupAsync(client, school.Pupils[0].Number, pin.Value));

        html.ShouldContain("Too many tries for this pupil");
        (await PinAsync(pin.Id)).UseCount.ShouldBe(0);
    }

    [Fact]
    public async Task Session_EndsAtOnce_WhenItsPinIsRevoked()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();
        var cookie = CookieFrom(await LookupAsync(client, school.Pupils[0].Number, pin.Value));

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await context.Set<Pin>().SingleAsync(candidate => candidate.Id == pin.Id, TestContext.Current.CancellationToken);
            stored.Revoke("Lost slip", null, DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await ReadAsync(await GetAsync(client, "/portal/terms", cookie))).ShouldContain("Your session has ended");
    }

    private HttpClient NewClient() =>
        Fixture.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static async Task<HttpResponseMessage> LookupAsync(HttpClient client, string number, string pin, string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/portal/lookup")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["registrationNumber"] = number, ["pin"] = pin }),
        };
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string CookieFrom(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("gras_portal=", StringComparison.Ordinal)).Split(';')[0];

    private static Task<string> ReadAsync(HttpResponseMessage response) => response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private async Task<Pin> PinAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<Pin>().AsNoTracking()
            .SingleAsync(pin => pin.Id == id, TestContext.Current.CancellationToken);
    }

    private sealed record SeededPupil(Guid Id, string Number, string TypedLoosely, string Name);

    private sealed record SeededSchool(Guid SessionId, IReadOnlyList<SeededPupil> Pupils);

    private sealed record SeededPin(Guid Id, string Value, string TypedLoosely);

    private async Task<SeededSchool> SeedSchoolAsync(bool published, int pupils = 1)
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
        context.Add(term);
        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.Add(arm);
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), arm.Id, term.Id).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, published ? ResultSetState.Published : ResultSetState.Approved);
        context.Add(resultSet);

        var seeded = new List<SeededPupil>();
        for (var index = 0; index < pupils; index++)
        {
            var surname = new[] { "Okafor", "Eze", "Nwosu", "Obi", "Adeyemi" }[index];
            var pupil = Pupil.Create(
                Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female, new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9),
                nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
            context.Add(pupil);
            context.Add(Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, new DateOnly(year, 9, 1)).Value);
            var serial = Interlocked.Increment(ref _nextSerial);
            seeded.Add(new SeededPupil(pupil.Id, $"GRAS/{year}/{serial:D4}", $"gras-{year}-{serial:D4}", $"Chidera {surname}"));
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        foreach (var pupil in seeded)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupils SET status = {nameof(PupilStatus.Active)}, registration_number = {pupil.Number} WHERE id = {pupil.Id}",
                TestContext.Current.CancellationToken);
        }

        return new SeededSchool(session.Id, seeded);
    }

    private async Task<SeededPin> SeedPinAsync(Guid sessionId, int maxUses)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secrets = scope.ServiceProvider.GetRequiredService<IPinSecrets>();

        var batch = PinBatch.Create(Guid.CreateVersion7(), sessionId, $"Batch {Guid.NewGuid():N}", null, 10, maxUses, 1, DateTimeOffset.UtcNow, null);
        var value = PinValue.Generate(10);
        var material = secrets.Protect(value);
        var pin = Pin.Create(Guid.CreateVersion7(), batch.Id, material.PinHash, material.LookupKey, value[..4], material.Ciphertext, maxUses);
        context.Add(batch);
        context.Add(pin);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededPin(pin.Id, value, " " + PinValue.Format(value).ToLowerInvariant() + " ");
    }
}
