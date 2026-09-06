using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Idempotency;

namespace SchoolManagement.UnitTests.Application.Idempotency;

/// <summary>
/// <see cref="IdempotencyPurgeJob"/> (TASK-0019, §9.9): purges expired rows and records exactly one
/// audit event per run, unconditionally — even when nothing was purged.
/// </summary>
public sealed class IdempotencyPurgeJobTests
{
    private readonly IIdempotencyStore _store = Substitute.For<IIdempotencyStore>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.Parse("2026-09-06T12:00:00+00:00"));

    private IdempotencyPurgeJob CreateJob() => new(_store, _auditSink, _timeProvider);

    [Fact]
    public async Task RunOnceAsync_PurgesAgainstTheCurrentInstant_AndReturnsTheCount()
    {
        _store.PurgeExpiredAsync(_timeProvider.GetUtcNow(), Arg.Any<CancellationToken>())
            .Returns(3);

        var purgedCount = await CreateJob().RunOnceAsync(TestContext.Current.CancellationToken);

        purgedCount.ShouldBe(3);
        await _store.Received(1).PurgeExpiredAsync(_timeProvider.GetUtcNow(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_RecordsExactlyOneAuditEvent_WithTheStableActionCodeAndTheCount()
    {
        _store.PurgeExpiredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(7);

        await CreateJob().RunOnceAsync(TestContext.Current.CancellationToken);

        await _auditSink.Received(1).RecordAsync(
            IdempotencyPurgeJob.PurgeAction,
            IdempotencyPurgeJob.PurgeEntityType,
            entityId: null,
            Arg.Is<IReadOnlyDictionary<string, object?>>(metadata => HasCount(metadata, 7)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_StillRecordsAnAuditEvent_WhenNothingWasPurged()
    {
        // The run itself must be on the trail even when it found nothing — otherwise a gap in the
        // log is ambiguous between "the job did not run" and "it ran and found nothing" (§9.9).
        _store.PurgeExpiredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(0);

        await CreateJob().RunOnceAsync(TestContext.Current.CancellationToken);

        await _auditSink.Received(1).RecordAsync(
            IdempotencyPurgeJob.PurgeAction,
            IdempotencyPurgeJob.PurgeEntityType,
            entityId: null,
            Arg.Is<IReadOnlyDictionary<string, object?>>(metadata => HasCount(metadata, 0)),
            Arg.Any<CancellationToken>());
    }

    // A plain method call, not an inline `out var`/null-conditional lambda body: Arg.Is<T> compiles
    // its predicate as an expression tree, which supports neither.
    private static bool HasCount(IReadOnlyDictionary<string, object?>? metadata, int expected) =>
        metadata is not null && metadata.TryGetValue("count", out var count) && Equals(count, expected);
}
