using SchoolManagement.Domain.Pins;

namespace SchoolManagement.UnitTests.Domain.Pins;

public sealed class PinDomainTests
{
    [Theory]
    [InlineData("h7k2m-qrw4t", "H7K2MQRW4T")]
    [InlineData(" H7K2M QRW4T ", "H7K2MQRW4T")]
    [InlineData("H7K2MQRW4T", "H7K2MQRW4T")]
    public void Normalize_UppercasesAndStripsSpacesAndHyphens(string typed, string expected) =>
        PinValue.Normalize(typed).ShouldBe(expected);

    [Fact]
    public void Format_GroupsInFives() => PinValue.Format("H7K2MQRW4T").ShouldBe("H7K2M-QRW4T");

    [Fact]
    public void Generate_UsesOnlyTheUnambiguousAlphabet()
    {
        var value = PinValue.Generate(2000);

        value.Length.ShouldBe(2000);
        value.ShouldAllBe(character => PinValue.Alphabet.Contains(character));
        PinValue.Alphabet.ShouldNotContain('O');
        PinValue.Alphabet.ShouldNotContain('0');
        PinValue.Alphabet.ShouldNotContain('I');
        PinValue.Alphabet.ShouldNotContain('1');
        PinValue.Alphabet.Length.ShouldBe(31);
    }

    [Fact]
    public void Batch_PurgesPlaintextThirtyDaysAfterGeneration_AndMovesThroughItsStates()
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var batch = PinBatch.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), " Batch 1 ", " ", 10, 3, 5, generatedAt, null);

        batch.Name.ShouldBe("Batch 1");
        batch.PurposeNote.ShouldBeNull();
        batch.PlaintextPurgeAtUtc.ShouldBe(generatedAt.AddDays(30));

        batch.MarkPrinted();
        batch.State.ShouldBe(PinBatchState.Printed);
        batch.MarkDistributed().IsSuccess.ShouldBeTrue();
        batch.MarkPrinted();
        batch.State.ShouldBe(PinBatchState.Active);
        batch.MarkDistributed().IsFailure.ShouldBeTrue();

        batch.Revoke("Lost sheet", null, generatedAt).IsSuccess.ShouldBeTrue();
        batch.Revoke("Again", null, generatedAt).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Pin_ReinstateRestoresTheStateItsUsesImply()
    {
        var pin = Pin.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", "key", "H7K2", "cipher", 3);

        pin.Reinstate().IsFailure.ShouldBeTrue();

        typeof(Pin).GetProperty(nameof(Pin.State))!.SetValue(pin, PinState.Suspended);
        pin.Reinstate().IsSuccess.ShouldBeTrue();
        pin.State.ShouldBe(PinState.Unused);
    }
}
