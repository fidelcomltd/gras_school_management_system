using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="GradingScaleRules"/> against spec 6.2.5's ten save-time rules.</summary>
public sealed class GradingScaleRulesTests
{
    private static readonly IReadOnlyList<GradingBandInput> ValidTwoBandScale =
    [
        new(50, 100, "P", "Pass"),
        new(0, 49, "F", "Fail"),
    ];

    [Fact]
    public void ValidateWholeScale_AcceptsTheSeededNineBandScale()
    {
        var result = GradingScaleRules.ValidateWholeScale(GradingScaleSeed.SeededBands);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeScale_AcceptsAWellFormedTwoBandScale()
    {
        var result = GradingScaleRules.ValidateWholeScale(ValidTwoBandScale);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeScale_Rule1_RejectsAnEmptyScale()
    {
        var result = GradingScaleRules.ValidateWholeScale([]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.EmptyScaleCode);
        var error = result.Error.ShouldBeOfType<GradingBandValidationError>();
        error.BandIndex.ShouldBe(-1);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 105)]
    public void ValidateWholeScale_Rule3_RejectsABoundOutsideZeroToHundred(int lower, int upper)
    {
        var result = GradingScaleRules.ValidateWholeScale([new GradingBandInput(lower, upper, "A", "Pass")]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.BoundOutOfRangeCode);
    }

    [Fact]
    public void ValidateWholeScale_Rule4_RejectsALowerBoundAboveItsUpperBound()
    {
        var result = GradingScaleRules.ValidateWholeScale(
        [
            new GradingBandInput(69, 60, "C", "Fair"),
            new GradingBandInput(0, 59, "F", "Fail"),
            new GradingBandInput(70, 100, "A", "Excellent"),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.BoundsReversedCode);
        result.Error.Description.ShouldContain("Band C");
    }

    [Fact]
    public void ValidateWholeScale_Rule5_RejectsOverlappingBandsAndNamesTheOverlappingMarks()
    {
        var result = GradingScaleRules.ValidateWholeScale(
        [
            new GradingBandInput(60, 69, "C", "Fair"),
            new GradingBandInput(68, 79, "B", "Good"),
            new GradingBandInput(0, 59, "F", "Fail"),
            new GradingBandInput(80, 100, "A", "Excellent"),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.BandsOverlapCode);
        result.Error.Description.ShouldContain("marks 68 and 69");
    }

    [Fact]
    public void ValidateWholeScale_Rule6_RejectsAGapBetweenBandsAndNamesTheUnbandedMark()
    {
        var result = GradingScaleRules.ValidateWholeScale(
        [
            new GradingBandInput(50, 59, "D", "Fair"),
            new GradingBandInput(61, 69, "C", "Good"),
            new GradingBandInput(0, 49, "F", "Fail"),
            new GradingBandInput(70, 100, "A", "Excellent"),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.CoverageGapCode);
        result.Error.Description.ShouldContain("Mark 60");
    }

    [Fact]
    public void ValidateWholeScale_Rule7_RejectsAScaleThatDoesNotStartAtZero()
    {
        var result = GradingScaleRules.ValidateWholeScale(
        [
            new GradingBandInput(40, 100, "P", "Pass"),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.DoesNotStartAtZeroCode);
    }

    [Fact]
    public void ValidateWholeScale_Rule8_RejectsAScaleThatDoesNotEndAtHundred()
    {
        var result = GradingScaleRules.ValidateWholeScale(
        [
            new GradingBandInput(0, 99, "P", "Pass"),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.DoesNotEndAtHundredCode);
    }

    [Fact]
    public void ValidateWholeScale_Rule9_RejectsDuplicateGradeLettersCaseInsensitively()
    {
        var result = GradingScaleRules.ValidateWholeScale(
        [
            new GradingBandInput(50, 100, "p", "Pass"),
            new GradingBandInput(0, 49, "P", "Fail"),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.DuplicateGradeLetterCode);
    }

    [Fact]
    public void ValidateWholeScale_Rule10_RejectsARemarkUnderThreeCharacters()
    {
        var result = GradingScaleRules.ValidateWholeScale(
        [
            new GradingBandInput(50, 100, "P", "OK"),
            new GradingBandInput(0, 49, "F", "Fail"),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.RemarkTooShortCode);
    }

    [Fact]
    public void ValidateWholeScale_ChoosesTheFirstFailureInSpecOrder()
    {
        // Both rule 3 (upper bound 105) and rule 9 (duplicate letter P) fail here; rule 3 must win.
        var result = GradingScaleRules.ValidateWholeScale(
        [
            new GradingBandInput(50, 105, "P", "Pass"),
            new GradingBandInput(0, 49, "P", "Fail"),
        ]);

        result.Error.Code.ShouldBe(GradingScaleRules.BoundOutOfRangeCode);
    }

    [Fact]
    public void ValidateWholeScale_NamesTheSubmittedArrayIndexOfTheOffendingBand()
    {
        var result = GradingScaleRules.ValidateWholeScale(
        [
            new GradingBandInput(50, 100, "P", "Pass"),
            new GradingBandInput(0, 49, "P", "Fail"), // index 1: duplicate of index 0's letter.
        ]);

        var error = result.Error.ShouldBeOfType<GradingBandValidationError>();
        error.BandIndex.ShouldBe(1);
    }
}
