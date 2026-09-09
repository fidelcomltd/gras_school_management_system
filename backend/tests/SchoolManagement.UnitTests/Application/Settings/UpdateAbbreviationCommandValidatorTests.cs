using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="UpdateAbbreviationCommandValidator"/> against spec 6.2.4's field rules.</summary>
public sealed class UpdateAbbreviationCommandValidatorTests
{
    private readonly UpdateAbbreviationCommandValidator _validator = new();

    private static UpdateAbbreviationCommand ValidCommand(
        string abbreviation = "GRA",
        string confirmationToken = UpdateAbbreviationCommandValidator.RequiredConfirmationToken,
        string reason = "The school shortened its registered trading name.",
        int expectedVersion = 0) =>
        new(abbreviation, confirmationToken, reason, expectedVersion);

    [Fact]
    public async Task Validate_AcceptsAWellFormedCommand()
    {
        var result = await _validator.ValidateAsync(ValidCommand(), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_AcceptsAbbreviationAtTheMinimumLength()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(abbreviation: new string('a', SchoolProfile.AbbreviationMinLength)),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_AcceptsAbbreviationAtTheMaximumLength()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(abbreviation: new string('a', SchoolProfile.AbbreviationMaxLength)),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_RejectsAbbreviationOneCharacterUnderTheMinimum()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(abbreviation: new string('a', SchoolProfile.AbbreviationMinLength - 1)),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateAbbreviationCommand.Abbreviation));
    }

    [Fact]
    public async Task Validate_RejectsAbbreviationOneCharacterOverTheMaximum()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(abbreviation: new string('a', SchoolProfile.AbbreviationMaxLength + 1)),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateAbbreviationCommand.Abbreviation));
    }

    [Theory]
    [InlineData("change")] // Lower-case — the literal token is case-sensitive.
    [InlineData("Change")]
    [InlineData("CHANGES")]
    [InlineData("")]
    public async Task Validate_RejectsAnythingOtherThanTheExactLiteralToken(string confirmationToken)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(confirmationToken: confirmationToken),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateAbbreviationCommand.ConfirmationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_RejectsAnEmptyOrWhitespaceOnlyReason(string reason)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(reason: reason),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateAbbreviationCommand.Reason));
    }

    [Fact]
    public async Task Validate_AcceptsAReasonShorterThanTenCharacters()
    {
        // The card's own point: NO 10-character floor here, unlike the grading-scale family.
        var result = await _validator.ValidateAsync(
            ValidCommand(reason: "Typo fix."),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_RejectsAReasonOverTheAuthoredCap()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(reason: new string('a', UpdateAbbreviationCommandValidator.ReasonMaxLength + 1)),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateAbbreviationCommand.Reason));
    }

    [Fact]
    public async Task Validate_RejectsANegativeExpectedVersion()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(expectedVersion: -1),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateAbbreviationCommand.ExpectedVersion));
    }
}
