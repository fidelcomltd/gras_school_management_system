using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.UnitTests.Domain.Pupils;

/// <summary>Entity-local invariants for <see cref="PupilRegNumberHistory"/> (TASK-0063, spec 6.5.10).</summary>
public sealed class PupilRegNumberHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_HappyPath_StoresEveryField()
    {
        var pupilId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();

        var result = PupilRegNumberHistory.Create(
            Guid.CreateVersion7(), pupilId, "GRAS/2025/0041", "Wrong admission year was entered.", actorId, Now);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        result.Value.PupilId.ShouldBe(pupilId);
        result.Value.OldRegistrationNumber.ShouldBe("GRAS/2025/0041");
        result.Value.Reason.ShouldBe("Wrong admission year was entered.");
        result.Value.CorrectedBy.ShouldBe(actorId);
        result.Value.CorrectedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Create_WithASystemActor_AllowsNullCorrectedBy()
    {
        var result = PupilRegNumberHistory.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "GRAS/2025/0041", "Wrong admission year was entered.", null, Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CorrectedBy.ShouldBeNull();
    }

    [Theory]
    [InlineData("too short")] // 9 characters — one under the ten-character floor.
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithAReasonUnderTenCharacters_Fails(string reason)
    {
        var result = PupilRegNumberHistory.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "GRAS/2025/0041", reason, null, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.regnumber_correction_reason_too_short");
    }

    [Fact]
    public void Create_WithAReasonOfExactlyTenCharacters_Succeeds()
    {
        var result = PupilRegNumberHistory.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "GRAS/2025/0041", "1234567890", null, Now);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithAnEmptyId_Fails()
    {
        var result = PupilRegNumberHistory.Create(
            Guid.Empty, Guid.CreateVersion7(), "GRAS/2025/0041", "Wrong admission year was entered.", null, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.regnumber_history.id_required");
    }

    [Fact]
    public void Create_WithAnEmptyPupilId_Fails()
    {
        var result = PupilRegNumberHistory.Create(
            Guid.CreateVersion7(), Guid.Empty, "GRAS/2025/0041", "Wrong admission year was entered.", null, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.regnumber_history.pupil_id_required");
    }
}
