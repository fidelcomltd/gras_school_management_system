using SchoolManagement.Domain.Enrolments;

namespace SchoolManagement.UnitTests.Domain.Enrolments;

/// <summary>Entity-local invariants for <see cref="Enrolment"/> (spec 02-data-model.md §5.1, §5.2).</summary>
public sealed class EnrolmentTests
{
    private static readonly Guid PupilId = Guid.CreateVersion7();
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid OtherArmId = Guid.CreateVersion7();
    private static readonly DateOnly OpenedOn = new(2026, 9, 14);

    [Fact]
    public void Open_WithValidArguments_CreatesAnOpenEnrolment()
    {
        var result = Enrolment.Open(Guid.CreateVersion7(), PupilId, ArmId, OpenedOn);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PupilId.ShouldBe(PupilId);
        result.Value.ArmId.ShouldBe(ArmId);
        result.Value.EffectiveFrom.ShouldBe(OpenedOn);
        result.Value.EffectiveTo.ShouldBeNull();
        result.Value.IsOpen.ShouldBeTrue();
    }

    [Fact]
    public void Open_WithAnEmptyId_Fails()
    {
        var result = Enrolment.Open(Guid.Empty, PupilId, ArmId, OpenedOn);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("enrolment.id_required");
    }

    [Fact]
    public void Open_WithAnEmptyPupilId_Fails()
    {
        var result = Enrolment.Open(Guid.CreateVersion7(), Guid.Empty, ArmId, OpenedOn);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("enrolment.pupil_id_required");
    }

    [Fact]
    public void Open_WithAnEmptyArmId_Fails()
    {
        var result = Enrolment.Open(Guid.CreateVersion7(), PupilId, Guid.Empty, OpenedOn);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("enrolment.arm_id_required");
    }

    [Fact]
    public void Close_OnAnOpenEnrolment_SetsEffectiveToAndClosesIt()
    {
        var enrolment = Enrolment.Open(Guid.CreateVersion7(), PupilId, ArmId, OpenedOn).Value;
        var closedOn = OpenedOn.AddDays(30);

        var result = enrolment.Close(closedOn);

        result.IsSuccess.ShouldBeTrue();
        enrolment.EffectiveTo.ShouldBe(closedOn);
        enrolment.IsOpen.ShouldBeFalse();

        // ArmId is never mutated by any operation on this type (spec 02 §5.2's "never mutates
        // arm_id in place" — the history of who sat where is the point).
        enrolment.ArmId.ShouldBe(ArmId);
    }

    [Fact]
    public void Close_ALREADYClosedEnrolment_Fails()
    {
        var enrolment = Enrolment.Open(Guid.CreateVersion7(), PupilId, ArmId, OpenedOn).Value;
        enrolment.Close(OpenedOn.AddDays(10)).IsSuccess.ShouldBeTrue();

        var result = enrolment.Close(OpenedOn.AddDays(20));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("enrolment.already_closed");
    }

    [Fact]
    public void Close_WithAnEffectiveToBeforeEffectiveFrom_Fails()
    {
        var enrolment = Enrolment.Open(Guid.CreateVersion7(), PupilId, ArmId, OpenedOn).Value;

        var result = enrolment.Close(OpenedOn.AddDays(-1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("enrolment.effective_to_before_effective_from");
    }

    // Spec 02 §5.2, near-verbatim: "Moving a pupil ... closes the first enrolment on the transfer
    // date and opens a second." The history of who sat where is the point, so this is proven by
    // showing BOTH rows survive with their own arm and dates — not one row rewritten.
    [Fact]
    public void Transfer_ClosesTheCurrentEnrolmentAndOpensANewOneForTheDestinationArm()
    {
        var current = Enrolment.Open(Guid.CreateVersion7(), PupilId, ArmId, OpenedOn).Value;
        var transferDate = OpenedOn.AddDays(60);
        var newEnrolmentId = Guid.CreateVersion7();

        var result = Enrolment.Transfer(current, newEnrolmentId, OtherArmId, transferDate);

        result.IsSuccess.ShouldBeTrue();

        // The OLD row: closed the day before the transfer date (spec 6.4.4 step 3), its arm untouched.
        current.IsOpen.ShouldBeFalse();
        current.EffectiveTo.ShouldBe(transferDate.AddDays(-1));
        current.ArmId.ShouldBe(ArmId);

        // The NEW row: a different id, the destination arm, open from the transfer date.
        var next = result.Value;
        next.Id.ShouldBe(newEnrolmentId);
        next.PupilId.ShouldBe(PupilId);
        next.ArmId.ShouldBe(OtherArmId);
        next.EffectiveFrom.ShouldBe(transferDate);
        next.IsOpen.ShouldBeTrue();
    }

    [Fact]
    public void Transfer_ToTheSameArmThePupilIsAlreadyIn_Fails()
    {
        var current = Enrolment.Open(Guid.CreateVersion7(), PupilId, ArmId, OpenedOn).Value;

        var result = Enrolment.Transfer(current, Guid.CreateVersion7(), ArmId, OpenedOn.AddDays(10));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("enrolment.transfer_destination_is_current_arm");
        current.IsOpen.ShouldBeTrue("a rejected transfer must not close the existing enrolment");
    }

    [Fact]
    public void Transfer_OfAnAlreadyClosedEnrolment_Fails()
    {
        var current = Enrolment.Open(Guid.CreateVersion7(), PupilId, ArmId, OpenedOn).Value;
        current.Close(OpenedOn.AddDays(5));

        var result = Enrolment.Transfer(current, Guid.CreateVersion7(), OtherArmId, OpenedOn.AddDays(10));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("enrolment.already_closed");
    }

    [Fact]
    public void Transfer_OnTheDayTheEnrolmentOpened_Fails()
    {
        var current = Enrolment.Open(Guid.CreateVersion7(), PupilId, ArmId, OpenedOn).Value;

        var result = Enrolment.Transfer(current, Guid.CreateVersion7(), OtherArmId, OpenedOn);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("enrolment.transfer_date_not_after_start");
        current.IsOpen.ShouldBeTrue();
    }
}
