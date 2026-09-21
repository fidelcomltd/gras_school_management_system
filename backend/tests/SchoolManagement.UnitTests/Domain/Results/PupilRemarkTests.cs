using SchoolManagement.Domain.Results;

namespace SchoolManagement.UnitTests.Domain.Results;

/// <summary>
/// Entity-local invariants for <see cref="PupilRemark"/> (TASK-0086 stage A), especially appendix
/// C.6's "captured at the time of writing, not rewritten by a later staff change" rule, which
/// <see cref="PupilRemark.UpdateText"/> enforces itself.
/// </summary>
public sealed class PupilRemarkTests
{
    private static readonly DateTimeOffset WrittenAt = new(2026, 12, 12, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithEveryReferenceSuppliedAndValidText_Succeeds()
    {
        var result = PupilRemark.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), RemarkKind.ClassTeacher,
            "A diligent pupil.", Guid.CreateVersion7(), "Mrs Adeyemi", WrittenAt);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Text.ShouldBe("A diligent pupil.");
        result.Value.WrittenByName.ShouldBe("Mrs Adeyemi");
        result.Value.WrittenAtUtc.ShouldBe(WrittenAt);
    }

    [Fact]
    public void Create_WithEmptyId_Fails()
    {
        var result = PupilRemark.Create(
            Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(), RemarkKind.ClassTeacher,
            "Text", Guid.CreateVersion7(), "Name", WrittenAt);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil_remark.id_required");
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Create_WithAnyReferenceEmpty_Fails(bool emptyResultSet, bool emptyPupil, bool emptyWriter)
    {
        var result = PupilRemark.Create(
            Guid.CreateVersion7(),
            emptyResultSet ? Guid.Empty : Guid.CreateVersion7(),
            emptyPupil ? Guid.Empty : Guid.CreateVersion7(),
            RemarkKind.HeadTeacher,
            "Text",
            emptyWriter ? Guid.Empty : Guid.CreateVersion7(),
            "Name",
            WrittenAt);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil_remark.reference_required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankText_Fails(string text)
    {
        var result = PupilRemark.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), RemarkKind.ClassTeacher,
            text, Guid.CreateVersion7(), "Name", WrittenAt);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil_remark.text_invalid");
    }

    [Fact]
    public void Create_WithTextOverMaxLength_Fails()
    {
        var result = PupilRemark.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), RemarkKind.ClassTeacher,
            new string('a', PupilRemark.TextMaxLength + 1), Guid.CreateVersion7(), "Name", WrittenAt);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil_remark.text_invalid");
    }

    [Fact]
    public void Create_WithTextAtMaxLength_Succeeds()
    {
        var result = PupilRemark.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), RemarkKind.ClassTeacher,
            new string('a', PupilRemark.TextMaxLength), Guid.CreateVersion7(), "Name", WrittenAt);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void UpdateText_WithDifferentText_RewritesTheSnapshotAndReturnsTrue()
    {
        var remark = PupilRemark.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), RemarkKind.ClassTeacher,
            "Original text.", Guid.CreateVersion7(), "Mrs Adeyemi", WrittenAt).Value;
        var newWriterId = Guid.CreateVersion7();
        var newWrittenAt = WrittenAt.AddDays(1);

        var changed = remark.UpdateText("Revised text.", newWriterId, "Mr Bello", newWrittenAt);

        changed.ShouldBeTrue();
        remark.Text.ShouldBe("Revised text.");
        remark.WrittenByAdminId.ShouldBe(newWriterId);
        remark.WrittenByName.ShouldBe("Mr Bello");
        remark.WrittenAtUtc.ShouldBe(newWrittenAt);
    }

    [Fact]
    public void UpdateText_WithTheSameText_LeavesTheSnapshotUntouchedAndReturnsFalse()
    {
        // Appendix C.6: "captured at the time of writing so a staff change later does not rewrite an
        // issued sheet" — a no-op resave, even by a DIFFERENT admin, must not disturb attribution.
        var remark = PupilRemark.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), RemarkKind.ClassTeacher,
            "Unchanged text.", Guid.CreateVersion7(), "Mrs Adeyemi", WrittenAt).Value;
        var originalWriterId = remark.WrittenByAdminId;

        var changed = remark.UpdateText("Unchanged text.", Guid.CreateVersion7(), "Mr Bello", WrittenAt.AddDays(1));

        changed.ShouldBeFalse();
        remark.Text.ShouldBe("Unchanged text.");
        remark.WrittenByAdminId.ShouldBe(originalWriterId);
        remark.WrittenByName.ShouldBe("Mrs Adeyemi");
        remark.WrittenAtUtc.ShouldBe(WrittenAt);
    }
}
