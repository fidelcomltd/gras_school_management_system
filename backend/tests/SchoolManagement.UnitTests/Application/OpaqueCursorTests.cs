using SchoolManagement.Application.Common.Pagination;

namespace SchoolManagement.UnitTests.Application;

/// <summary>Tests <see cref="OpaqueCursor"/> (spec 9.5).</summary>
public sealed class OpaqueCursorTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(41L)]
    [InlineData(long.MaxValue)]
    public void EncodeThenTryDecode_RoundTrips(long value)
    {
        var cursor = OpaqueCursor.Encode(value);

        var decoded = OpaqueCursor.TryDecode(cursor, out var result);

        decoded.ShouldBeTrue();
        result.ShouldBe(value);
    }

    [Fact]
    public void TryDecode_RejectsNull()
    {
        OpaqueCursor.TryDecode(null, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_RejectsEmpty()
    {
        OpaqueCursor.TryDecode(string.Empty, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-valid-base64!!")]
    [InlineData("====")]
    public void TryDecode_RejectsMalformedBase64(string cursor)
    {
        OpaqueCursor.TryDecode(cursor, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_RejectsBase64ThatIsNotAnInteger()
    {
        // Valid base64, decodes to "hello" — not a number.
        var notANumber = Convert.ToBase64String("hello"u8.ToArray());

        OpaqueCursor.TryDecode(notANumber, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_RejectsANegativeNumber()
    {
        // NumberStyles.None rejects a leading sign — a cursor is never negative.
        var negative = Convert.ToBase64String("-1"u8.ToArray());

        OpaqueCursor.TryDecode(negative, out _).ShouldBeFalse();
    }
}
