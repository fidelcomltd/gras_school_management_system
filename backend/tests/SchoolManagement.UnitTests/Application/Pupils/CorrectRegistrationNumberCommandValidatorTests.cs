using SchoolManagement.Application.Pupils;

namespace SchoolManagement.UnitTests.Application.Pupils;

/// <summary>Tests <see cref="CorrectRegistrationNumberCommandValidator"/> (TASK-0063, spec 6.5.10).</summary>
public sealed class CorrectRegistrationNumberCommandValidatorTests
{
    private readonly CorrectRegistrationNumberCommandValidator _validator = new();

    private static CorrectRegistrationNumberCommand ValidCommand(
        string registrationNumber = "GRAS/2026/0041",
        string reason = "Wrong admission year was entered at approval.") =>
        new(Guid.CreateVersion7(), registrationNumber, reason);

    [Fact]
    public async Task Validate_AcceptsAWellFormedCommand()
    {
        var result = await _validator.ValidateAsync(ValidCommand(), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("GRAS20260041")] // no separators at all.
    [InlineData("GRAS/26/0041")] // year is not four digits.
    [InlineData("/2026/0041")] // no abbreviation.
    public async Task Validate_RejectsAMalformedRegistrationNumber(string registrationNumber)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(registrationNumber: registrationNumber),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(CorrectRegistrationNumberCommand.RegistrationNumber));
    }

    [Fact]
    public async Task Validate_RejectsARegistrationNumberOverTheColumnWidth()
    {
        var tooLong = "GRAS/2026/" + new string('0', 20); // 30 characters, over the 24-character column.

        var result = await _validator.ValidateAsync(
            ValidCommand(registrationNumber: tooLong),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(CorrectRegistrationNumberCommand.RegistrationNumber));
    }

    [Theory]
    [InlineData("too short")] // nine characters.
    [InlineData("")]
    public async Task Validate_RejectsAReasonUnderTenCharacters(string reason)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(reason: reason),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(CorrectRegistrationNumberCommand.Reason));
    }

    [Fact]
    public async Task Validate_AcceptsAReasonOfExactlyTenCharacters()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(reason: "1234567890"),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }
}
