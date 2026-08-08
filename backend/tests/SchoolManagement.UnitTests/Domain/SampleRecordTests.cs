using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Reference;

namespace SchoolManagement.UnitTests.Domain;

/// <summary>
/// REFERENCE TEST — the template for testing a domain factory's invariants.
/// </summary>
public sealed class SampleRecordTests
{
    private static readonly Guid ValidId = Guid.CreateVersion7();

    [Fact]
    public void Create_SucceedsWithValidInput()
    {
        var result = SampleRecord.Create(ValidId, "Timetable", "A note.");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Label.ShouldBe("Timetable");
        result.Value.Note.ShouldBe("A note.");
        result.Value.Id.ShouldBe(ValidId);
    }

    [Fact]
    public void Create_TrimsTheLabel()
    {
        // Untrimmed input is how you end up with "Maths" and "Maths " as two different records that a
        // unique index cannot tell apart but a human can.
        var result = SampleRecord.Create(ValidId, "   Timetable   ", note: null);

        result.Value.Label.ShouldBe("Timetable");
    }

    [Fact]
    public void Create_AllowsANullNote()
    {
        SampleRecord.Create(ValidId, "Timetable", note: null).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_RejectsAnEmptyId()
    {
        var result = SampleRecord.Create(Guid.Empty, "Timetable", note: null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("sample_record.id_required");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsABlankLabel(string label)
    {
        var result = SampleRecord.Create(ValidId, label, note: null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("sample_record.label_required");
    }

    [Fact]
    public void Create_AcceptsALabelAtTheMaximumLength()
    {
        var label = new string('a', SampleRecord.LabelMaxLength);

        SampleRecord.Create(ValidId, label, note: null).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_RejectsALabelOneCharacterOverTheMaximum()
    {
        // The boundary matters: the column is sized to LabelMaxLength, so an off-by-one here becomes a
        // database truncation error at runtime instead of a clean 422.
        var label = new string('a', SampleRecord.LabelMaxLength + 1);

        var result = SampleRecord.Create(ValidId, label, note: null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("sample_record.label_too_long");
    }

    [Fact]
    public void Create_RejectsANoteOverTheMaximum()
    {
        var note = new string('a', SampleRecord.NoteMaxLength + 1);

        var result = SampleRecord.Create(ValidId, "Timetable", note);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("sample_record.note_too_long");
    }

    [Fact]
    public void Create_DoesNotSetAuditFields()
    {
        // Audit fields are the interceptor's job. If the factory set them, a save would overwrite them
        // anyway and the two sources would disagree in any code path that skipped the interceptor.
        var record = SampleRecord.Create(ValidId, "Timetable", note: null).Value;

        record.CreatedAtUtc.ShouldBe(default);
        record.ModifiedAtUtc.ShouldBeNull();
        record.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public void Entities_AreEqualByIdentity()
    {
        var first = SampleRecord.Create(ValidId, "One", note: null).Value;
        var second = SampleRecord.Create(ValidId, "Two — a different label", note: null).Value;

        // Same identity means same entity, whatever the field values. This is what makes change
        // tracking and set operations behave sensibly.
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Entities_WithDifferentIdsAreNotEqual()
    {
        var first = SampleRecord.Create(Guid.CreateVersion7(), "One", note: null).Value;
        var second = SampleRecord.Create(Guid.CreateVersion7(), "One", note: null).Value;

        first.ShouldNotBe(second);
    }
}
