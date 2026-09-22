using SchoolManagement.Api.Configuration;

namespace SchoolManagement.UnitTests.Api;

/// <summary>
/// Tests the Data Protection key ring path validation. The rejection worth testing is the RELATIVE
/// path: it would resolve against the process working directory, which differs between the systemd
/// service, a local <c>dotnet run</c> and the EF tooling — so one configured value would silently
/// address three key rings, and CSRF tokens would stop verifying whenever the process that issued
/// them was not the one validating them.
/// </summary>
public sealed class KeyRingOptionsValidatorTests
{
    private static readonly KeyRingOptionsValidator Validator = new();

    [Fact]
    public void Validate_AcceptsAnUnsetPath()
    {
        // Staging on a PaaS has nowhere persistent to put it; the warning at startup covers this.
        Validator.Validate(null, new KeyRingOptions()).Succeeded.ShouldBeTrue();
    }

    // Both conventions are accepted on either platform: the production value is a Linux path and
    // this suite also runs on Windows, so a platform-sensitive check here would reject the real
    // configuration on the machine it is being developed on.
    [Theory]
    [InlineData("/var/lib/gras/dataprotection-keys")]
    [InlineData(@"C:\ProgramData\gras\keys")]
    public void Validate_AcceptsAnAbsolutePath(string path)
    {
        Validator.Validate(null, new KeyRingOptions { KeyRingPath = path }).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("keys")]
    [InlineData("./keys")]
    [InlineData("../shared/keys")]
    public void Validate_RejectsARelativePath(string path)
    {
        var result = Validator.Validate(null, new KeyRingOptions { KeyRingPath = path });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("absolute path");
    }
}
