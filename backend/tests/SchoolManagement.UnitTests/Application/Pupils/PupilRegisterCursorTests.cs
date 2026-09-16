using SchoolManagement.Application.Pupils;

namespace SchoolManagement.UnitTests.Application.Pupils;

/// <summary>
/// Tests <see cref="PupilRegisterCursor"/> (spec 6.5.15, TASK-0061) — the register's widened
/// class-progression cursor, kept separate from <see cref="PupilListCursor"/> (the admissions
/// queue's, untouched by this card).
/// </summary>
public sealed class PupilRegisterCursorTests
{
    [Theory]
    [InlineData(1, "1a", "okafor", "3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData(9, "", "zulu", "00000000-0000-0000-0000-000000000000")]
    [InlineData(PupilRegisterCursor.UnenrolledLevelOrdinal, PupilRegisterCursor.UnenrolledArmKey, "adaeze", "3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public void EncodeThenTryDecode_RoundTrips(int levelOrdinal, string armKey, string surnameKey, string idText)
    {
        var id = Guid.Parse(idText);
        var cursor = PupilRegisterCursor.Encode(levelOrdinal, armKey, surnameKey, id);

        var decoded = PupilRegisterCursor.TryDecode(
            cursor, out var decodedLevelOrdinal, out var decodedArmKey, out var decodedSurnameKey, out var decodedId);

        decoded.ShouldBeTrue();
        decodedLevelOrdinal.ShouldBe(levelOrdinal);
        decodedArmKey.ShouldBe(armKey);
        decodedSurnameKey.ShouldBe(surnameKey);
        decodedId.ShouldBe(id);
    }

    [Fact]
    public void TryDecode_RejectsNull()
    {
        PupilRegisterCursor.TryDecode(null, out _, out _, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_RejectsEmpty()
    {
        PupilRegisterCursor.TryDecode(string.Empty, out _, out _, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_RejectsMalformedBase64()
    {
        PupilRegisterCursor.TryDecode("not-valid-base64!!", out _, out _, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_RejectsAWellFormedPupilListCursor()
    {
        // Two fields, not four — the admissions queue's cursor shape must never be mistaken for the
        // register's widened one (the trap this card's Notes named).
        var queueCursor = PupilListCursor.Encode("okafor", Guid.NewGuid());

        PupilRegisterCursor.TryDecode(queueCursor, out _, out _, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_RejectsANonIntegerLevelOrdinal()
    {
        var malformed = Convert.ToBase64String("not-a-number1aokafor3fa85f64-5717-4562-b3fc-2c963f66afa6"u8.ToArray());

        PupilRegisterCursor.TryDecode(malformed, out _, out _, out _, out _).ShouldBeFalse();
    }
}
