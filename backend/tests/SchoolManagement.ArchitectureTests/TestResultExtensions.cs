// Aliased because xunit.v3 also exposes a Xunit.TestResult, and the global `using Xunit` in
// tests/Directory.Build.props makes the bare name ambiguous.
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// Turns a NetArchTest result into an assertion with a message worth reading.
/// </summary>
/// <remarks>
/// NetArchTest's own failure output is a bare boolean. A developer who breaks an architecture rule
/// needs three things: WHICH types broke it, WHAT the rule is, and WHY the rule exists — otherwise the
/// path of least resistance is to delete the test. Every call site therefore passes a rationale.
/// </remarks>
internal static class TestResultExtensions
{
    public static void ShouldBeSuccessful(this ArchTestResult result, string because)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccessful)
        {
            return;
        }

        var offenders = result.FailingTypeNames is { } names
            ? string.Join(Environment.NewLine, names.Select(name => "  - " + name))
            : "  (NetArchTest reported no type names)";

        result.IsSuccessful.ShouldBeTrue(
            $"{because}{Environment.NewLine}{Environment.NewLine}Offending types:{Environment.NewLine}{offenders}");
    }
}
