using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Idempotency;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests.Idempotency;

/// <summary>
/// Proves the TASK-0019 idempotency substrate end to end against a REAL PostgreSQL, via
/// <see cref="IdempotencyTestHost"/> — see that class's remarks for why a second, purpose-built host
/// stands in for a production route the card explicitly forbids adding.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class IdempotencySubstrateTests(ApiTestFixture fixture) : IAsyncLifetime
{
    private IdempotencyTestHost? _host;

    public async ValueTask InitializeAsync()
    {
        if (!fixture.IsDatabaseAvailable)
        {
            return;
        }

        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        _host = await IdempotencyTestHost.StartAsync(fixture.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    private void RequireDatabase()
    {
        if (!fixture.IsDatabaseAvailable)
        {
            Assert.Skip(fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    private IdempotencyTestHost Host => _host ?? throw new InvalidOperationException("Host not started.");

    [Fact]
    public async Task Post_WithoutIdempotencyKey_ReturnsKeyMissing()
    {
        RequireDatabase();

        using var response = await Host.Client.PostAsJsonAsync(
            IdempotencyTestHost.ProbeRoute,
            new ProbeCommand("no-key"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await ReadProblemAsync(response);
        body.ShouldContain("idempotency.key_missing");
    }

    [Theory]
    [InlineData("has a space")]
    [InlineData("has\ta-tab")]
    public async Task Post_WithMalformedIdempotencyKey_ReturnsKeyMalformed(string malformedKey)
    {
        RequireDatabase();

        using var request = NewRequest(malformedKey, "caller-1", new ProbeCommand("malformed"));
        using var response = await Host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await ReadProblemAsync(response);
        body.ShouldContain("idempotency.key_malformed");
    }

    [Fact]
    public async Task DuplicatePost_WithSameKeyAndSamePayload_CreatesExactlyOneRowAndReplays()
    {
        RequireDatabase();

        var key = $"key-{Guid.NewGuid():N}";
        var command = new ProbeCommand($"label-{Guid.NewGuid():N}");

        using var first = await SendAsync(key, "caller-1", command);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        first.Headers.Contains("Idempotency-Replay").ShouldBeFalse("the first call is not a replay");

        using var second = await SendAsync(key, "caller-1", command);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Headers.TryGetValues("Idempotency-Replay", out var replayValues).ShouldBeTrue();
        replayValues!.ShouldContain("true");

        var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var secondBody = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        secondBody.ShouldBe(firstBody, "a genuine replay returns the ORIGINAL response body verbatim");

        await using var scope = Host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var matchingRows = await context.SampleRecords
            .CountAsync(record => record.Label == command.Value, TestContext.Current.CancellationToken);

        matchingRows.ShouldBe(1, "the duplicate POST must perform the side effect exactly once");
    }

    [Fact]
    public async Task Post_WithSameKeyButDifferentPayload_ReturnsKeyConflict_NotReplay()
    {
        RequireDatabase();

        var key = $"key-{Guid.NewGuid():N}";

        using var first = await SendAsync(key, "caller-1", new ProbeCommand($"label-{Guid.NewGuid():N}"));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var second = await SendAsync(key, "caller-1", new ProbeCommand($"a-completely-different-label-{Guid.NewGuid():N}"));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await ReadProblemAsync(second);
        body.ShouldContain("idempotency.key_conflict");
    }

    [Fact]
    public async Task Post_WithSameKeyButDifferentCaller_IsANewClaim_NotAConflict()
    {
        RequireDatabase();

        var key = $"key-{Guid.NewGuid():N}";

        using var asCallerOne = await SendAsync(key, "caller-1", new ProbeCommand($"label-{Guid.NewGuid():N}"));
        using var asCallerTwo = await SendAsync(key, "caller-2", new ProbeCommand($"label-{Guid.NewGuid():N}"));

        asCallerOne.StatusCode.ShouldBe(HttpStatusCode.Created);
        asCallerTwo.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            "the same key from a different caller is a different request, not a conflict or a replay");
        asCallerTwo.Headers.Contains("Idempotency-Replay").ShouldBeFalse();
    }

    [Fact]
    public async Task ConcurrentDuplicatePosts_ProduceExactlyOneSideEffect()
    {
        RequireDatabase();

        var key = $"key-{Guid.NewGuid():N}";
        var command = new ProbeCommand($"label-{Guid.NewGuid():N}");

        // Genuinely concurrent: both requests are in flight before either completes. The STORE's
        // guarantee — proven structurally by TryClaimAsync/RequireIdempotencyKeyExtensions, not just
        // by this test — is that AT MOST ONE of the two ever reaches the protected handler at all
        // (the unique index on (key_hash, caller) admits exactly one "Claimed" outcome; every other
        // outcome returns before `next(context)` runs), so the side effect can never happen twice.
        // WHICH of the two remaining legal shapes the loser gets is a scheduling accident, not a
        // guarantee this test may assert on:
        //   (a) the loser's read lands before the winner's claim is marked complete -> InProgress ->
        //       409 idempotency.request_in_progress ([Created, Conflict]).
        //   (b) the winner completes first -> the loser's read finds a COMPLETED row with a matching
        //       fingerprint -> a genuine REPLAY of the winner's stored 201, `Idempotency-Replay: true`
        //       ([Created, Created]). Confirmed empirically (not just by reading the store): under
        //       real load against the hosted Neon instance this branch reproduces, and the second
        //       response DOES carry the replay header, never a fresh execution.
        // Both are legal; a naive [Created, Conflict]-only assertion (the shape this test used to
        // require) fails on legitimate (b) — orchestrator-diagnosed 2026-09-06, TASK-0027 dispatch 2.
        var firstTask = SendAsync(key, "caller-1", command);
        var secondTask = SendAsync(key, "caller-1", command);

        var responses = await Task.WhenAll(firstTask, secondTask);

        try
        {
            var statusCodes = responses.Select(response => response.StatusCode).ToArray();
            var replayFlags = responses
                .Select(response => response.Headers.TryGetValues("Idempotency-Replay", out var values) &&
                    values.Contains("true"))
                .ToArray();

            var isInProgressShape = statusCodes.OrderBy(code => code).SequenceEqual(
                [HttpStatusCode.Created, HttpStatusCode.Conflict]);
            var isReplayShape = statusCodes.All(code => code == HttpStatusCode.Created) &&
                replayFlags.Count(isReplay => isReplay) == 1;

            (isInProgressShape || isReplayShape).ShouldBeTrue(
                "expected either [Created, Conflict] (in-progress) or [Created, Created] with exactly " +
                $"one carrying Idempotency-Replay: true (a genuine replay), got statuses " +
                $"[{string.Join(", ", statusCodes)}] with replay flags [{string.Join(", ", replayFlags)}].");

            if (isInProgressShape)
            {
                var conflictResponse = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
                var body = await ReadProblemAsync(conflictResponse);
                body.ShouldContain("idempotency.request_in_progress");
            }
            else
            {
                var winner = responses[Array.IndexOf(replayFlags, false)];
                var loser = responses[Array.IndexOf(replayFlags, true)];

                var winnerBody = await winner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var loserBody = await loser.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                loserBody.ShouldBe(winnerBody, "a genuine replay returns the ORIGINAL response body verbatim");
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        // The assertion that actually matters, unconditionally in EITHER legal branch: the side
        // effect ran exactly once. This is what the status-code shape was only ever a proxy for.
        await using var scope = Host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var matchingRows = await context.SampleRecords
            .CountAsync(record => record.Label == command.Value, TestContext.Current.CancellationToken);

        matchingRows.ShouldBe(1, "two genuinely concurrent duplicate requests must still perform the side effect exactly once");
    }

    [Fact]
    public async Task Replay_RedactsTheMarkedProperty_ButTheLiveResponseKeepsTheRealValue()
    {
        RequireDatabase();

        var key = $"key-{Guid.NewGuid():N}";
        const string realSecret = "shown-once-only";
        var command = new ProbeCommand($"label-{Guid.NewGuid():N}", realSecret);

        using var first = await SendAsync(key, "caller-1", command);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var firstJson = await first.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        firstJson.GetProperty("secret").GetString().ShouldBe(realSecret, "the live caller must see the real value");

        using var replay = await SendAsync(key, "caller-1", command);
        replay.Headers.TryGetValues("Idempotency-Replay", out _).ShouldBeTrue();

        var replayJson = await replay.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        replayJson.GetProperty("secret").ValueKind.ShouldBe(
            JsonValueKind.Null,
            "the replayed response the SECOND caller sees must already be redacted");

        // The real proof per the acceptance criterion: read the STORED row back directly, not just
        // the second HTTP response (which the same redaction code path produced either way).
        await using var scope = Host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var storedJson = await context.IdempotencyRecords
            .AsNoTracking()
            .Where(record => record.Caller == "caller-1")
            .Select(record => record.ResponseBodyJson)
            .SingleAsync(TestContext.Current.CancellationToken);

        storedJson.ShouldNotBeNull();
        using var storedDocument = JsonDocument.Parse(storedJson);
        storedDocument.RootElement.GetProperty("secret").ValueKind.ShouldBe(
            JsonValueKind.Null,
            "the STORED replay copy must never contain the one-time secret");
    }

    [Fact]
    public async Task PurgeJob_RemovesExpiredRows_AndWritesItsAuditEvent()
    {
        RequireDatabase();

        await using var scope = Host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        var now = DateTimeOffset.UtcNow;

        // A negative retention puts ExpiresAtUtc in the past immediately — no need to wait for a
        // real 24h window to prove the purge actually removes an eligible row.
        var claim = await store.TryClaimAsync(
            $"expired-{Guid.NewGuid():N}",
            "caller-1",
            "fingerprint",
            now,
            TimeSpan.FromHours(-1),
            TestContext.Current.CancellationToken);

        claim.Outcome.ShouldBe(IdempotencyClaimOutcome.Claimed);

        var beforeCount = await context.IdempotencyRecords.CountAsync(TestContext.Current.CancellationToken);
        beforeCount.ShouldBeGreaterThan(0);

        var job = scope.ServiceProvider.GetRequiredService<IdempotencyPurgeJob>();
        var purgedCount = await job.RunOnceAsync(TestContext.Current.CancellationToken);

        purgedCount.ShouldBeGreaterThan(0, "the row seeded above is already past its retention window");

        var afterCount = await context.IdempotencyRecords.CountAsync(TestContext.Current.CancellationToken);
        afterCount.ShouldBe(0, "a row past the retention window must be GONE after a purge run, not merely flagged");
    }

    private async Task<HttpResponseMessage> SendAsync(string key, string caller, ProbeCommand command)
    {
        using var request = NewRequest(key, caller, command);
        return await Host.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage NewRequest(string key, string caller, ProbeCommand command)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, IdempotencyTestHost.ProbeRoute)
        {
            Content = JsonContent.Create(command),
        };

        request.Headers.TryAddWithoutValidation(RequireIdempotencyKeyHeaderName, key);
        request.Headers.TryAddWithoutValidation(IdempotencyTestHost.CallerHeaderName, caller);

        return request;
    }

    // Mirrors SchoolManagement.Api.Idempotency.RequireIdempotencyKeyExtensions.HeaderName — that
    // type is internal and this constant is simple enough not to need InternalsVisibleTo plumbing
    // just to read one string literal.
    private const string RequireIdempotencyKeyHeaderName = "Idempotency-Key";

    private static async Task<string> ReadProblemAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
