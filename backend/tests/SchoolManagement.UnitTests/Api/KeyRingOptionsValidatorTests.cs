using SchoolManagement.Api.Configuration;

namespace SchoolManagement.UnitTests.Api;

/// <summary>
/// Tests the Data Protection key ring path validation. The rejection worth testing is the RELATIVE
/// path: it would resolve against the process working directory, which differs between the systemd
/// service, a local <c>dotnet run</c> and the EF tooling — so one configured value would silently
/// address three key rings, and CSRF tokens would stop verifying whenever the process that issued
/// them was not the one validating them.
/// </summary>
/// <remarks>
/// WHY THESE TESTS ARE PLATFORM-AWARE. <see cref="Path.IsPathRooted(string)"/> answers for the
/// RUNNING platform, which is correct for this validator (the path is a directory on this host) but
/// means "absolute" is not one fixed set of strings: <c>/var/lib/gras</c> is rooted on Windows,
/// while <c>C:\ProgramData\gras</c> is NOT rooted on Linux — there it is an ordinary relative name
/// that happens to contain colons and backslashes. An earlier version of this file asserted that
/// both forms are accepted everywhere; it passed on a Windows dev machine, where both are rooted,
/// and failed in CI on Linux. So each test below asserts only what is true on the platform it is
/// running on, and there is no <c>Skip</c> anywhere: a skipped test fails this project's gates
/// (<c>rules/gates.md</c>), and a test that quietly asserts nothing on one platform is worse than
/// no test at all.
/// </remarks>
public sealed class KeyRingOptionsValidatorTests
{
    private static readonly KeyRingOptionsValidator Validator = new();

    /// <summary>An absolute path for whichever platform this test run is on.</summary>
    private static string AbsolutePathForThisPlatform =>
        OperatingSystem.IsWindows() ? @"C:\ProgramData\gras\keys" : "/var/lib/gras/dataprotection-keys";

    [Fact]
    public void Validate_AcceptsAnUnsetPath()
    {
        // Staging on a PaaS has nowhere persistent to put it; the warning at startup covers this.
        Validator.Validate(null, new KeyRingOptions()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AcceptsAnAbsolutePath()
    {
        var options = new KeyRingOptions { KeyRingPath = AbsolutePathForThisPlatform };

        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AcceptsThePathProductionActuallyUses_WhenRunningWhereProductionRuns()
    {
        // The committed production value (appsettings.Production.json). On Linux — CI, and the VPS —
        // this is the assertion that matters, because it is the real configuration. On Windows it is
        // also rooted (drive-relative), so the same expectation holds and the test stays honest
        // rather than branching into nothing.
        var options = new KeyRingOptions { KeyRingPath = "/var/lib/gras/dataprotection-keys" };

        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("keys")]
    [InlineData("./keys")]
    [InlineData("../shared/keys")]
    public void Validate_RejectsARelativePath(string path)
    {
        // Relative on every platform, so this is the one set that can be asserted unconditionally.
        var result = Validator.Validate(null, new KeyRingOptions { KeyRingPath = path });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("absolute path");
    }
}
