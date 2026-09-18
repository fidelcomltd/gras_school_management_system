using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="UpdateRegNumberCommandValidator"/> against spec 6.2.4's field table.</summary>
public sealed class UpdateRegNumberCommandValidatorTests
{
    private readonly UpdateRegNumberCommandValidator _validator = new();

    private static UpdateRegNumberCommand ValidCommand(
        string separator = "/",
        int serialWidth = 4,
        RegNumberSerialReset serialReset = RegNumberSerialReset.PerYear,
        int expectedVersion = 0) =>
        new(separator, serialWidth, serialReset, expectedVersion);

    [Fact]
    public async Task Validate_AcceptsAWellFormedCommand()
    {
        var result = await _validator.ValidateAsync(ValidCommand(), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("-")]
    [InlineData(".")]
    public async Task Validate_AcceptsEachOfTheThreeAllowedSeparators(string separator)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(separator: separator),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(",")]
    [InlineData("//")]
    [InlineData("")]
    [InlineData("_")]
    public async Task Validate_RejectsAnySeparatorOutsideTheAllowedThree(string separator)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(separator: separator),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateRegNumberCommand.Separator));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public async Task Validate_AcceptsSerialWidthAtEitherBoundary(int serialWidth)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(serialWidth: serialWidth),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    public async Task Validate_RejectsSerialWidthOutsideThreeToSix(int serialWidth)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(serialWidth: serialWidth),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateRegNumberCommand.SerialWidth));
    }

    [Theory]
    [InlineData(RegNumberSerialReset.PerYear)]
    [InlineData(RegNumberSerialReset.Continuous)]
    public async Task Validate_AcceptsBothSerialResetModes(RegNumberSerialReset serialReset)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(serialReset: serialReset),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_RejectsAnUndefinedSerialReset()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(serialReset: (RegNumberSerialReset)99),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateRegNumberCommand.SerialReset));
    }

    [Fact]
    public async Task Validate_RejectsANegativeExpectedVersion()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(expectedVersion: -1),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateRegNumberCommand.ExpectedVersion));
    }
}
