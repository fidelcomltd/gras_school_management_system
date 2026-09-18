using SchoolManagement.Application.Audit;

namespace SchoolManagement.UnitTests.Application.Audit;

/// <summary>
/// Tests <see cref="AuditEventListCursor"/> — the composite <c>(occurred_at, id)</c> tie-break the
/// card's own risk note singles out (TASK-0049).
/// </summary>
public sealed class AuditEventListCursorTests
{
    private static readonly DateTimeOffset SampleOccurredAt =
        new(2026, 9, 9, 12, 30, 45, 123, TimeSpan.Zero);

    [Fact]
    public void EncodeThenTryDecode_RoundTrips()
    {
        var cursor = AuditEventListCursor.Encode(SampleOccurredAt, 48213L);

        var decoded = AuditEventListCursor.TryDecode(cursor, out var occurredAt, out var id);

        decoded.ShouldBeTrue();
        occurredAt.ShouldBe(SampleOccurredAt);
        id.ShouldBe(48213L);
    }

    [Fact]
    public void EncodeThenTryDecode_PreservesSubMillisecondPrecision()
    {
        // occurred_at can carry microsecond precision in Postgres; UtcTicks (100ns resolution)
        // must round-trip exactly or two rows within the same millisecond could still be misordered.
        var precise = SampleOccurredAt.AddTicks(37);

        var cursor = AuditEventListCursor.Encode(precise, 1L);
        AuditEventListCursor.TryDecode(cursor, out var occurredAt, out _);

        occurredAt.UtcTicks.ShouldBe(precise.UtcTicks);
    }

    [Fact]
    public void TryDecode_RejectsNull()
    {
        AuditEventListCursor.TryDecode(null, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_RejectsEmpty()
    {
        AuditEventListCursor.TryDecode(string.Empty, out _, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-valid-base64!!")]
    [InlineData("====")]
    public void TryDecode_RejectsMalformedBase64(string cursor)
    {
        AuditEventListCursor.TryDecode(cursor, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_RejectsATamperedSingleFieldCursor()
    {
        // Valid base64, decodes to "hello" — not the two-field shape Encode produces.
        var malformed = Convert.ToBase64String("hello"u8.ToArray());

        AuditEventListCursor.TryDecode(malformed, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void TwoRowsWithTheIdenticalOccurredAt_EncodeToDifferentCursors()
    {
        // The crux this type exists for: occurred_at alone cannot disambiguate two rows written in
        // the same instant, so their cursors — and therefore the keyset predicate built from them —
        // must differ purely on id.
        var first = AuditEventListCursor.Encode(SampleOccurredAt, 100L);
        var second = AuditEventListCursor.Encode(SampleOccurredAt, 101L);

        first.ShouldNotBe(second);

        AuditEventListCursor.TryDecode(first, out _, out var firstId);
        AuditEventListCursor.TryDecode(second, out _, out var secondId);

        firstId.ShouldNotBe(secondId);
    }
}
