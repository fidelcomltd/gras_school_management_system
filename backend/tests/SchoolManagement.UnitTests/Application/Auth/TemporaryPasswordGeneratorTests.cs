using SchoolManagement.Application.Auth;
using SchoolManagement.Domain.Auth;

namespace SchoolManagement.UnitTests.Application.Auth;

/// <summary>Tests <see cref="TemporaryPasswordGenerator"/>: digits only, at the policy's minimum length.</summary>
public sealed class TemporaryPasswordGeneratorTests
{
    [Fact]
    public void Generate_IsDigitsOnlyAtThePolicyMinimumLength()
    {
        for (var i = 0; i < 50; i++)
        {
            var password = TemporaryPasswordGenerator.Generate();

            password.Length.ShouldBe(AuthPolicy.PasswordMinLength);
            password.All(char.IsAsciiDigit).ShouldBeTrue();
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
