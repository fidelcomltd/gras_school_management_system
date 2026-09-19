using SchoolManagement.Domain.Results;

namespace SchoolManagement.UnitTests.Domain.Results;

/// <summary>Entity-local invariants for <see cref="AttendanceEntry"/> (TASK-0086 stage A).</summary>
public sealed class AttendanceEntryTests
{
    [Fact]
    public void Create_WithEveryReferenceSuppliedAndNonNegativeTimesPresent_Succeeds()
    {
        var result = AttendanceEntry.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TimesPresent.ShouldBe(42);
    }

    [Fact]
    public void Create_WithEmptyId_Fails()
    {
        var result = AttendanceEntry.Create(Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(), 10);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("attendance_entry.id_required");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Create_WithAnyReferenceEmpty_Fails(bool emptyResultSet, bool emptyPupil)
    {
        var result = AttendanceEntry.Create(
            Guid.CreateVersion7(),
            emptyResultSet ? Guid.Empty : Guid.CreateVersion7(),
            emptyPupil ? Guid.Empty : Guid.CreateVersion7(),
            10);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("attendance_entry.reference_required");
    }

    [Fact]
    public void Create_WithNegativeTimesPresent_Fails()
    {
        var result = AttendanceEntry.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), -1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("attendance_entry.times_present_out_of_range");
    }

    [Fact]
    public void Create_WithZeroTimesPresent_Succeeds()
    {
        var result = AttendanceEntry.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 0);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void UpdateTimesPresent_ReplacesOnlyTheValue()
    {
        var entry = AttendanceEntry.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 10).Value;
        var pupilId = entry.PupilId;

        entry.UpdateTimesPresent(25);

        entry.TimesPresent.ShouldBe(25);
        entry.PupilId.ShouldBe(pupilId);
    }
}
