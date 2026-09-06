using SchoolManagement.Application.Idempotency;

namespace SchoolManagement.UnitTests.Application.Idempotency;

/// <summary>
/// <see cref="IdempotencyOptionsValidator"/> — the §9.9 retention window this card commits to
/// (24h default) must stay inside a sane range at startup, not merely at the moment someone edits
/// the default.
/// </summary>
public sealed class IdempotencyOptionsValidatorTests
{
    private readonly IdempotencyOptionsValidator _validator = new();

    [Fact]
    public void Validate_AcceptsTheDefault()
    {
        var result = _validator.Validate(name: null, new IdempotencyOptions());

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(168)]
    public void Validate_AcceptsTheBoundaries(int retentionHours)
    {
        var result = _validator.Validate(name: null, new IdempotencyOptions { RetentionHours = retentionHours });

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(169)]
    public void Validate_RejectsOutsideTheBoundaries(int retentionHours)
    {
        var result = _validator.Validate(name: null, new IdempotencyOptions { RetentionHours = retentionHours });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(IdempotencyOptions.RetentionHours));
    }
}
