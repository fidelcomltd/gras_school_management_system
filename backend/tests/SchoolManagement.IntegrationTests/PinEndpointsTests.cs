using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Pins;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pins;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>Spec 6.8: pin batch generation, the stored forms of a pin, and the batch and pin lifecycle.</summary>
public sealed class PinEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BatchesUrl = "/api/v1/pin-batches";

    private static int _nextSessionStartYear = 9500;

    [Fact]
    public async Task Generate_StoresEachPinAsHashLookupKeyAndCiphertext_AndNeverReturnsAValue()
    {
        RequireDatabase();
        var (sessionId, jar) = await SeedAsync();

        var response = await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 20 });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var batch = await ReadAsync<PinBatchDto>(response);
        batch.PinCount.ShouldBe(20);
        batch.PinLength.ShouldBe(10);
        batch.MaxUses.ShouldBe(3);
        batch.State.ShouldBe(PinBatchState.Generated);
        batch.Name.ShouldEndWith("batch 1");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("ciphertext", Case.Insensitive);

        await using var scope = Fixture.CreateScope();
        var secrets = scope.ServiceProvider.GetRequiredService<IPinSecrets>();
        var pins = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<Pin>().AsNoTracking()
            .Where(pin => pin.BatchId == Guid.Parse(batch.Id)).ToListAsync(TestContext.Current.CancellationToken);
        pins.Count.ShouldBe(20);
        pins.Select(pin => pin.LookupKey).Distinct().Count().ShouldBe(20);
        foreach (var pin in pins)
        {
            var value = secrets.Reveal(pin.Ciphertext!);
            value.Length.ShouldBe(10);
            value.ShouldAllBe(character => PinValue.Alphabet.Contains(character));
            value[..4].ShouldBe(pin.Prefix);
            secrets.Verify(value, pin.PinHash).ShouldBeTrue();
            secrets.LookupKeyFor(value).ShouldBe(pin.LookupKey);
            pin.PinHash.ShouldNotContain(value);
        }
    }

    [Fact]
    public async Task Generate_TheFullTwoThousand_CompletesInReasonableTime()
    {
        RequireDatabase();
        var (sessionId, jar) = await SeedAsync();

        var stopwatch = Stopwatch.StartNew();
        var response = await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 2000 });
        stopwatch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        TestContext.Current.SendDiagnosticMessage($"2000 pins generated in {stopwatch.Elapsed.TotalSeconds:F1}s");
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public async Task Generate_WithoutACount_Returns422WithTheSpecMessage()
    {
        RequireDatabase();
        var (sessionId, jar) = await SeedAsync();

        var response = await PostAsync(BatchesUrl, jar, new { sessionId });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("Enter how many pins to generate.");
    }

    [Fact]
    public async Task Generate_AboveTenUses_NeedsTheNumberTypedBack()
    {
        RequireDatabase();
        var (sessionId, jar) = await SeedAsync();

        (await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 2, maxUses = 20 })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 2, maxUses = 20, confirmMaxUses = 20 })).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Generate_ADuplicateNameInTheSession_Returns409()
    {
        RequireDatabase();
        var (sessionId, jar) = await SeedAsync();

        (await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 1, name = "Primary 3 parents" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        var duplicate = await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 1, name = "primary 3 PARENTS" });

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var body = await ReadJsonAsync(duplicate);
        body.RootElement.GetProperty("errorCode").GetString().ShouldBe("pin_batch.name_taken");
    }

    [Fact]
    public async Task Generate_WithoutAnIdempotencyKey_Returns400()
    {
        RequireDatabase();
        var (sessionId, jar) = await SeedAsync();

        var response = await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 1 }, idempotencyKey: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Batch_Distribute_ThenRevoke_RevokesEveryPin()
    {
        RequireDatabase();
        var (sessionId, jar) = await SeedAsync();
        var batch = await ReadAsync<PinBatchDto>(await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 5 }));

        var distributed = await PostAsync($"{BatchesUrl}/{batch.Id}/mark-distributed", jar, payload: null);
        (await ReadAsync<PinBatchDto>(distributed)).State.ShouldBe(PinBatchState.Active);
        (await PostAsync($"{BatchesUrl}/{batch.Id}/mark-distributed", jar, payload: null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var revoked = await PostAsync($"{BatchesUrl}/{batch.Id}/revoke", jar, new { reason = "A sheet of slips went missing." });
        revoked.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await ReadAsync<PinBatchDetailDto>(await GetAsync($"{BatchesUrl}/{batch.Id}", jar));
        detail.Batch.State.ShouldBe(PinBatchState.Revoked);
        detail.Pins.ShouldAllBe(pin => pin.State == PinState.Revoked);
        detail.Pins.ShouldAllBe(pin => pin.Prefix.Length == 4);
    }

    [Fact]
    public async Task Pin_Reinstate_OnlyClearsASuspension()
    {
        RequireDatabase();
        var (sessionId, jar) = await SeedAsync();
        var batch = await ReadAsync<PinBatchDto>(await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 1 }));
        var pinId = (await ReadAsync<PinBatchDetailDto>(await GetAsync($"{BatchesUrl}/{batch.Id}", jar))).Pins[0].Id;

        (await PostAsync($"/api/v1/pins/{pinId}/reinstate", jar, new { reason = "Parent confirmed." })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pin = await context.Set<Pin>().SingleAsync(candidate => candidate.Id == Guid.Parse(pinId), TestContext.Current.CancellationToken);
            typeof(Pin).GetProperty(nameof(Pin.State))!.SetValue(pin, PinState.Suspended);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var reinstated = await PostAsync($"/api/v1/pins/{pinId}/reinstate", jar, new { reason = "Parent has four children; confirmed by phone." });
        reinstated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PinSummaryDto>(reinstated)).State.ShouldBe(PinState.Unused);
    }

    [Fact]
    public async Task List_PagesNewestFirst()
    {
        RequireDatabase();
        var (sessionId, jar) = await SeedAsync();
        await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 1, name = "First" });
        await PostAsync(BatchesUrl, jar, new { sessionId, pinCount = 1, name = "Second" });

        var first = await ReadAsync<CursorPage<PinBatchDto>>(await GetAsync($"{BatchesUrl}?sessionId={sessionId}&pageSize=1", jar));
        first.Items.Single().Name.ShouldBe("Second");
        first.NextCursor.ShouldNotBeNull();

        var second = await ReadAsync<CursorPage<PinBatchDto>>(await GetAsync($"{BatchesUrl}?sessionId={sessionId}&pageSize=1&cursor={first.NextCursor}", jar));
        second.Items.Single().Name.ShouldBe("First");
        second.Items.Single().PinCount.ShouldBe(1);
    }

    private async Task<(Guid SessionId, CookieJar Jar)> SeedAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false, email: $"admin-{Guid.NewGuid():N}@example.com");
        Guid sessionId;
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var year = Interlocked.Increment(ref _nextSessionStartYear);
            var session = AcademicSession.Create(Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 31)).Value;
            session.Activate();
            context.Add(session);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            sessionId = session.Id;
        }

        var jar = new CookieJar();
        await GetAsync("/api/v1/auth/csrf", jar);
        var signIn = await SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", jar, new SignInCommand(email, AdminAccountSeeder.Password), idempotencyKey: null);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (sessionId, jar);
    }

    private Task<HttpResponseMessage> PostAsync(string url, CookieJar jar, object? payload, string? idempotencyKey = "generate") =>
        SendAsync(HttpMethod.Post, url, jar, payload, idempotencyKey == "generate" ? Guid.NewGuid().ToString("D") : idempotencyKey);

    private async Task<HttpResponseMessage> GetAsync(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, CookieJar jar, object? payload, string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(method, url) { Content = payload is null ? null : JsonContent.Create(payload) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
