using SchoolManagement.Application.Reference.Ping;

namespace SchoolManagement.UnitTests.Application;

/// <summary>
/// REFERENCE TEST — the template for a validator test.
/// </summary>
/// <remarks>
/// Test the BOUNDARY values, not just the obvious rejection. "Rejects an empty name" is the easy case;
/// off-by-one on a maximum length is the one that reaches production, because the column is sized to the
/// same constant and the failure becomes a database truncation error instead of a clean 422.
/// </remarks>
public sealed class PingQueryValidatorTests
{
    private readonly PingQueryValidator _validator = new();

    [Fact]
    public async Task Validate_AcceptsANormalName()
    {
        var result = await _validator.ValidateAsync(
            new PingQuery("Ada"),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_RejectsAnEmptyName(string name)
    {
        var result = await _validator.ValidateAsync(
            new PingQuery(name),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(PingQuery.Name));
    }

    [Fact]
    public async Task Validate_AcceptsANameAtTheMaximumLength()
    {
        var name = new string('a', PingQueryValidator.NameMaxLength);

        var result = await _validator.ValidateAsync(
            new PingQuery(name),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_RejectsANameOneCharacterOverTheMaximum()
    {
        var name = new string('a', PingQueryValidator.NameMaxLength + 1);

        var result = await _validator.ValidateAsync(
            new PingQuery(name),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
