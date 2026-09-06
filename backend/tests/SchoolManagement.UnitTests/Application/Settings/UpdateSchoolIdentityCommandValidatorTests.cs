using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="UpdateSchoolIdentityCommandValidator"/> against spec 6.2.3's field rules.</summary>
public sealed class UpdateSchoolIdentityCommandValidatorTests
{
    private readonly UpdateSchoolIdentityCommandValidator _validator = new();

    private static UpdateSchoolIdentityCommand ValidCommand(
        string schoolName = "Golden Royal Ark School",
        string shortName = "GRAS",
        string address = "12 Ark Crescent",
        string phone = "08012345678",
        string email = "info@example.com",
        string? motto = "Excellence Through Character",
        string headTeacherName = "Chisom Maxwell",
        int expectedVersion = 0) =>
        new(schoolName, shortName, address, phone, email, motto, headTeacherName, expectedVersion);

    [Fact]
    public async Task Validate_AcceptsAWellFormedCommand()
    {
        var result = await _validator.ValidateAsync(ValidCommand(), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_AcceptsANullMotto()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(motto: null),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_RejectsAnEmptySchoolName(string schoolName)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(schoolName: schoolName),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateSchoolIdentityCommand.SchoolName));
    }

    [Fact]
    public async Task Validate_AcceptsASchoolNameAtTheMaximumLength()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(schoolName: new string('a', SchoolProfile.SchoolNameMaxLength)),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_RejectsASchoolNameOneCharacterOverTheMaximum()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(schoolName: new string('a', SchoolProfile.SchoolNameMaxLength + 1)),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_RejectsAMottoOneCharacterOverTheMaximum()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(motto: new string('a', SchoolProfile.MottoMaxLength + 1)),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateSchoolIdentityCommand.Motto));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.example.com")]
    public async Task Validate_RejectsAMalformedEmail(string email)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(email: email),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateSchoolIdentityCommand.Email));
    }

    [Theory]
    [InlineData("08012345678")]
    [InlineData("+2348012345678")]
    public async Task Validate_AcceptsBothPhoneForms(string phone)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(phone: phone),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("0801234567")] // 10 digits — one short of the required 11 in national form.
    [InlineData("2348012345678")] // Missing the leading +.
    [InlineData("not-a-phone-number")]
    public async Task Validate_RejectsAPhoneOutsideTheTwoAcceptedForms(string phone)
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(phone: phone),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateSchoolIdentityCommand.Phone));
    }

    [Fact]
    public async Task Validate_RejectsANegativeExpectedVersion()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(expectedVersion: -1),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateSchoolIdentityCommand.ExpectedVersion));
    }

    [Fact]
    public async Task Validate_AcceptsAnExpectedVersionOfZero_TheNeverYetSavedState()
    {
        var result = await _validator.ValidateAsync(
            ValidCommand(expectedVersion: 0),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }
}
