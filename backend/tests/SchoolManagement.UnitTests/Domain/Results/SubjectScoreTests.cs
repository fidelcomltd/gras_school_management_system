using SchoolManagement.Domain.Results;

namespace SchoolManagement.UnitTests.Domain.Results;

/// <summary>Entity-local invariants for <see cref="SubjectScore"/> (TASK-0076 dispatch B).</summary>
public sealed class SubjectScoreTests
{
    private static SubjectScore CreateScore(int? examMark = 55, bool examAbsent = false) =>
        SubjectScore.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), """{"c1":18}""", examMark, examAbsent).Value;

    [Fact]
    public void UpdateMarks_OnAnActiveRow_ReplacesEveryField()
    {
        var score = CreateScore();

        var result = score.UpdateMarks("""{"c1":20}""", 60, false);

        result.IsSuccess.ShouldBeTrue();
        score.ComponentMarksJson.ShouldBe("""{"c1":20}""");
        score.ExamMark.ShouldBe(60);
        score.ExamAbsent.ShouldBeFalse();
    }

    [Fact]
    public void UpdateMarks_CanClearAnExamMarkAndMarkAbsent()
    {
        var score = CreateScore();

        var result = score.UpdateMarks("""{"c1":18}""", null, true);

        result.IsSuccess.ShouldBeTrue();
        score.ExamMark.ShouldBeNull();
        score.ExamAbsent.ShouldBeTrue();
    }

    [Fact]
    public void UpdateMarks_OnAVoidedRow_Fails()
    {
        var score = CreateScore();
        score.Void("Whole class re-marked after a transcription error.", "admin-1", DateTimeOffset.UtcNow);

        var result = score.UpdateMarks("""{"c1":20}""", 60, false);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_score.already_voided");
    }

    [Fact]
    public void UpdateMarks_WithBlankComponentMarksJson_Fails()
    {
        var score = CreateScore();

        var result = score.UpdateMarks(" ", 60, false);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_score.component_marks_required");
    }
}
