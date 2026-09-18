using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="RatingScaleRules"/> against spec 6.2.13's rating-scale save-time rules.</summary>
public sealed class RatingScaleRulesTests
{
    private static readonly IReadOnlyList<RatingScalePointInput> ValidTwoPointScale =
    [
        new("N", "Needs Improvement", 1),
        new("E", "Excellent", 2),
    ];

    [Fact]
    public void ValidateWholeSet_AcceptsTheSeededThreeScaleSet()
    {
        var result = RatingScaleRules.ValidateWholeSet(RatingScaleSeed.SeededScales);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeSet_AcceptsAWellFormedTwoPointScale()
    {
        var result = RatingScaleRules.ValidateWholeSet([new RatingScaleInput("Custom", ValidTwoPointScale)]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeSet_RejectsAScaleWithOnlyOnePoint()
    {
        var result = RatingScaleRules.ValidateWholeSet(
        [
            new RatingScaleInput("Too small", [new RatingScalePointInput("E", "Excellent", 1)]),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RatingScaleRules.PointCountInvalidCode);
        result.Error.Description.ShouldBe("A rating scale needs at least two points.");
        var error = result.Error.ShouldBeOfType<RatingScaleValidationError>();
        error.ScaleIndex.ShouldBe(0);
        error.PointIndex.ShouldBeNull();
    }

    [Fact]
    public void ValidateWholeSet_RejectsAScaleWithMoreThanNinePoints()
    {
        var tooManyPoints = Enumerable.Range(1, 10)
            .Select(order => new RatingScalePointInput(order.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Point {order}", order))
            .ToList();

        var result = RatingScaleRules.ValidateWholeSet([new RatingScaleInput("Too big", tooManyPoints)]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RatingScaleRules.PointCountInvalidCode);
    }

    [Fact]
    public void ValidateWholeSet_RejectsADuplicatePointCodeWithinAScale()
    {
        var result = RatingScaleRules.ValidateWholeSet(
        [
            new RatingScaleInput(
                "Dup codes",
                [
                    new RatingScalePointInput("E", "Excellent", 1),
                    new RatingScalePointInput("e", "Also excellent", 2),
                ]),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RatingScaleRules.DuplicatePointCodeCode);
        var error = result.Error.ShouldBeOfType<RatingScaleValidationError>();
        error.ScaleIndex.ShouldBe(0);
        error.PointIndex.ShouldBe(1);
    }

    [Fact]
    public void ValidateWholeSet_RejectsADuplicatePointOrderWithinAScale()
    {
        var result = RatingScaleRules.ValidateWholeSet(
        [
            new RatingScaleInput(
                "Dup orders",
                [
                    new RatingScalePointInput("N", "Needs Improvement", 1),
                    new RatingScalePointInput("E", "Excellent", 1),
                ]),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RatingScaleRules.DuplicatePointOrderCode);
    }

    [Fact]
    public void ValidateWholeSet_RejectsTwoScalesWithTheSameNameCaseInsensitively()
    {
        var result = RatingScaleRules.ValidateWholeSet(
        [
            new RatingScaleInput("Custom", ValidTwoPointScale),
            new RatingScaleInput("custom", ValidTwoPointScale),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RatingScaleRules.DuplicateNameCode);
        var error = result.Error.ShouldBeOfType<RatingScaleValidationError>();
        error.ScaleIndex.ShouldBe(1);
    }

    [Fact]
    public void ValidateWholeSet_AcceptsAnEmptySet()
    {
        // No minimum-scale-count rule — spec 6.2.13 never states one, unlike grading's "at least one
        // band". An empty submission is structurally valid; whether it is wise is a state-conflict
        // concern (settings.ratingscales.in_use), not a structural one.
        var result = RatingScaleRules.ValidateWholeSet([]);

        result.IsSuccess.ShouldBeTrue();
    }
}
