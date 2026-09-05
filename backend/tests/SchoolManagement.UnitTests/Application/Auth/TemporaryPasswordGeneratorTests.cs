using SchoolManagement.Application.Auth;
using SchoolManagement.Domain.Auth;

namespace SchoolManagement.UnitTests.Application.Auth;

/// <summary>Tests <see cref="TemporaryPasswordGenerator"/> against spec 6.1.11's composition rule.</summary>
public sealed class TemporaryPasswordGeneratorTests
{
    [Fact]
    public void Generate_MeetsThePolicyLengthAndCompositionRules()
    {
        for (var i = 0; i < 50; i++)
        {
            var password = TemporaryPasswordGenerator.Generate();

            password.Length.ShouldBeGreaterThanOrEqualTo(AuthPolicy.PasswordMinLength);
            password.Length.ShouldBeLessThanOrEqualTo(AuthPolicy.PasswordMaxLength);
            password.Any(char.IsLetter).ShouldBeTrue();
            password.Any(char.IsDigit).ShouldBeTrue();
        }
    }

    [Fact]
    public void Generate_ProducesADifferentPasswordEachTime()
    {
        var first = TemporaryPasswordGenerator.Generate();
        var second = TemporaryPasswordGenerator.Generate();

        first.ShouldNotBe(second);
    }
}
