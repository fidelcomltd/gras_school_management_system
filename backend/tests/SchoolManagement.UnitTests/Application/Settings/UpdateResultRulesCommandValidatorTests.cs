using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="UpdateResultRulesCommandValidator"/> against spec 6.2.8's field table.</summary>
public sealed class UpdateResultRulesCommandValidatorTests
{
    private readonly UpdateResultRulesCommandValidator _validator = new();

    private static UpdateResultRulesCommand ValidCommand(
        AnnualMethod annualMethod = AnnualMethod.SimpleAverage,
        int? weightFirst = null,
        int? weightSecond = null,
        int? weightThird = null,
        PrimaryPositionScope primaryPositionScope = PrimaryPositionScope.Arm,
        bool showLevelPosition = true,
        TieBreakRule tieBreakRule = TieBreakRule.SharedPosition,
        int passMark = 40,
        int promotionThreshold = 40,
        bool requireCorePass = false,
        IReadOnlyList<Guid>? coreSubjectIds = null,
        int minSubjectsForPosition = 1,
        int expectedVersion = 0,
        string? reason = null) =>
        new(
            annualMethod,
            weightFirst,
            weightSecond,
            weightThird,
            primaryPositionScope,
            showLevelPosition,
            tieBreakRule,
            passMark,
            promotionThreshold,
            requireCorePass,
            coreSubjectIds ?? [],
            minSubjectsForPosition,
            expectedVersion,
            reason);

    [Fact]
    public async Task Validate_AcceptsTheSeededDefaults()
    {
        var result = await _validator.ValidateAsync(ValidCommand(), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_AcceptsWeightedWithThreeWeightsTotallingAHundred()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(annualMethod: AnnualMethod.Weighted, weightFirst: 30, weightSecond: 30, weightThird: 40),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_RejectsWeightedWithAMissingWeight()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(annualMethod: AnnualMethod.Weighted, weightFirst: 50, weightSecond: 50, weightThird: null),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateResultRulesCommand.WeightThird));
    }

    [Fact]
    public async Task Validate_RejectsWeightedWithWeightsThatDoNotTotalAHundred()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(annualMethod: AnnualMethod.Weighted, weightFirst: 30, weightSecond: 30, weightThird: 30),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage == "The three term weights total 90. They must total 100.");
    }

    [Fact]
    public async Task Validate_AcceptsSimpleAverageWithNoWeightsGiven()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(annualMethod: AnnualMethod.SimpleAverage, weightFirst: null, weightSecond: null, weightThird: null),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Validate_RejectsAWeightOutsideZeroToOneHundred(int weight)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(annualMethod: AnnualMethod.Weighted, weightFirst: weight, weightSecond: 0, weightThird: 0),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task Validate_AcceptsPassMarkAtEitherBoundary(int passMark)
    {
        var result = await _validator.ValidateAsync(ValidCommand(passMark: passMark), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Validate_RejectsPassMarkOutsideZeroToOneHundred(int passMark)
    {
        var result = await _validator.ValidateAsync(ValidCommand(passMark: passMark), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateResultRulesCommand.PassMark));
    }

    [Fact]
    public async Task Validate_RejectsRequireCorePassWithNoCoreSubjectIds()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(requireCorePass: true, coreSubjectIds: []),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateResultRulesCommand.CoreSubjectIds));
    }

    [Fact]
    public async Task Validate_AcceptsRequireCorePassWithAtLeastOneCoreSubjectId()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(requireCorePass: true, coreSubjectIds: [Guid.CreateVersion7()]),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_RejectsMinSubjectsForPositionBelowOne()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(minSubjectsForPosition: 0),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateResultRulesCommand.MinSubjectsForPosition));
    }

    [Fact]
    public async Task Validate_RejectsANegativeExpectedVersion()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(expectedVersion: -1),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateResultRulesCommand.ExpectedVersion));
    }

    [Fact]
    public async Task Validate_RejectsAReasonShorterThanTenCharacters()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(reason: "too short"),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateResultRulesCommand.Reason));
    }

    [Fact]
    public async Task Validate_AcceptsANullReason()
    {
        var result = await _validator.ValidateAsync(ValidCommand(reason: null), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }
}
