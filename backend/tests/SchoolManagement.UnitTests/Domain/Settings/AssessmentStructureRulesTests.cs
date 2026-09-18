using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="AssessmentStructureRules"/> against spec 6.2.6's six save-time rules.</summary>
public sealed class AssessmentStructureRulesTests
{
    [Fact]
    public void ValidateWholeStructure_AcceptsTheSeededGrasDefaultProfile()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(AssessmentStructureSeed.GrasDefaultComponents);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeStructure_AcceptsTheWithAssignmentProfile()
    {
        // 6.2.13: the second named profile must be reachable through the SAME validation path, with
        // no special-casing — proving that is exactly what "nothing branches on the choice" means.
        var result = AssessmentStructureRules.ValidateWholeStructure(AssessmentStructureSeed.WithAssignmentComponents);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeStructure_Rule1_RejectsAnExaminationOnlyStructure()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(
        [
            new AssessmentComponentInput(null, "Exam", "EXAM", 100, true),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AssessmentStructureRules.NoNonExaminationComponentCode);
    }

    [Fact]
    public void ValidateWholeStructure_Rule2_RejectsTwoComponentsMarkedAsTheExamination()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(
        [
            new AssessmentComponentInput(null, "CA", "CA", 40, false),
            new AssessmentComponentInput(null, "Exam", "EXAM", 30, true),
            new AssessmentComponentInput(null, "Exam2", "EXAM2", 30, true),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AssessmentStructureRules.NotExactlyOneExaminationCode);
    }

    [Fact]
    public void ValidateWholeStructure_Rule2_RejectsNoComponentMarkedAsTheExamination()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(
        [
            new AssessmentComponentInput(null, "CA1", "CA1", 50, false),
            new AssessmentComponentInput(null, "CA2", "CA2", 50, false),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AssessmentStructureRules.NotExactlyOneExaminationCode);
    }

    [Fact]
    public void ValidateWholeStructure_Rule3_RejectsComponentsTotallingUnderAHundred()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(
        [
            new AssessmentComponentInput(null, "CA", "CA", 30, false),
            new AssessmentComponentInput(null, "Exam", "EXAM", 60, true),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AssessmentStructureRules.DoesNotTotalHundredCode);
        result.Error.Description.ShouldContain("10 marks short");
    }

    [Fact]
    public void ValidateWholeStructure_Rule3b_RejectsComponentsTotallingOverAHundred()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(
        [
            new AssessmentComponentInput(null, "CA", "CA", 50, false),
            new AssessmentComponentInput(null, "Exam", "EXAM", 60, true),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AssessmentStructureRules.DoesNotTotalHundredCode);
        result.Error.Description.ShouldContain("10 marks over");
    }

    [Fact]
    public void ValidateWholeStructure_Rule4_RejectsAMaximumBelowOne()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(
        [
            new AssessmentComponentInput(null, "Assignment", "ASSGN", 0, false),
            new AssessmentComponentInput(null, "CA", "CA", 40, false),
            new AssessmentComponentInput(null, "Exam", "EXAM", 60, true),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AssessmentStructureRules.MaxMarkOutOfRangeCode);
        result.Error.Description.ShouldContain("Assignment");
    }

    [Fact]
    public void ValidateWholeStructure_Rule4_FiresBeforeRule5EvenWhenNamesAlsoDuplicate()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(
        [
            new AssessmentComponentInput(null, "2nd CA", "CA1", 40, false),
            new AssessmentComponentInput(null, "2nd ca", "CA2", 0, false), // duplicate name AND a zero maximum.
            new AssessmentComponentInput(null, "Exam", "EXAM", 60, true),
        ]);

        // Rule 4 (max mark 0) fires before rule 5 reaches the duplicate name — confirms spec order.
        result.Error.Code.ShouldBe(AssessmentStructureRules.MaxMarkOutOfRangeCode);
    }

    [Fact]
    public void ValidateWholeStructure_Rule5_RejectsDuplicateNamesWhenMaximumsAreOtherwiseValid()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(
        [
            new AssessmentComponentInput(null, "2nd CA", "CA1", 20, false),
            new AssessmentComponentInput(null, "2nd ca", "CA2", 20, false),
            new AssessmentComponentInput(null, "Exam", "EXAM", 60, true),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AssessmentStructureRules.DuplicateNameCode);
    }

    [Fact]
    public void ValidateWholeStructure_Rule5_RejectsDuplicateShortLabels()
    {
        var result = AssessmentStructureRules.ValidateWholeStructure(
        [
            new AssessmentComponentInput(null, "1st CA", "CA", 20, false),
            new AssessmentComponentInput(null, "2nd CA", "ca", 20, false),
            new AssessmentComponentInput(null, "Exam", "EXAM", 60, true),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AssessmentStructureRules.DuplicateShortLabelCode);
    }

    [Fact]
    public void ValidateWholeStructure_NeverHardcodesAComponentCount()
    {
        // Two, three, four and six non-examination components must all validate the same way — the
        // durability requirement, proven rather than asserted.
        foreach (var nonExaminationCount in new[] { 2, 3, 4, 6 })
        {
            var perComponentMark = 100 / (nonExaminationCount + 1);
            var examMark = 100 - (perComponentMark * nonExaminationCount);

            var components = Enumerable.Range(1, nonExaminationCount)
                .Select(index => new AssessmentComponentInput(null, $"CA{index}", $"C{index}", perComponentMark, false))
                .Append(new AssessmentComponentInput(null, "Exam", "EXAM", examMark, true))
                .ToList();

            var result = AssessmentStructureRules.ValidateWholeStructure(components);

            result.IsSuccess.ShouldBeTrue($"expected {nonExaminationCount} non-examination components to validate");
        }
    }
}
