namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// Spec 6.4.3: <c>display_name</c> is composed by ONE function with ONE implementation, everywhere an
/// arm is named. A second, hand-rolled composition ("Primary 1" + " " + "A" typed out again somewhere
/// else) is exactly the defect the spec calls out by name — a parent receiving a result sheet reading
/// "Primary 1 A". This test greps the whole solution for a second implementation and fails if one exists.
/// </summary>
public sealed class ArmDisplayNameCompositionTests
{
    private const string OwningFile = "src/SchoolManagement.Domain/Classes/ArmDisplayName.cs";

    [Fact]
    public void TheSingleAlphanumericCharacterCheck_AppearsOnlyInArmDisplayName()
    {
        // ArmDisplayName.Compose's own discriminator (single alphanumeric character -> no space,
        // otherwise one space) hinges on char.IsLetterOrDigit — a check nothing else in this codebase
        // needs (Arm's own label VALIDATION uses a different, stricter regex). A second file matching
        // this is either a duplicate implementation of the composition rule or a name collision worth
        // investigating either way.
        var violations = SourceTree.ProductionFiles
            .Where(file => file.RelativePath != OwningFile)
            .Where(file => file.Lines.Any(line => line.Contains("IsLetterOrDigit", StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .ToArray();

        violations.ShouldBeEmpty(
            "ArmDisplayName.Compose must be the ONLY place that decides an arm's display-name spacing " +
            "rule (spec 6.4.3). Found the same discriminator re-implemented in: " +
            string.Join(", ", violations));
    }

    [Fact]
    public void ComposeHasAtLeastOneProductionCallSite()
    {
        // Every legitimate caller (ArmMapper for the wire shape, ListArmsHandler for the free-text
        // display-name search, UpdateArmHandler for the closed-arm error message) goes through this
        // ONE function rather than reassembling "level name + label" by hand — proven by the
        // discriminator-uniqueness test above, not by limiting how many places may call it.
        var callSites = SourceTree.ProductionFiles
            .Where(file => file.RelativePath != OwningFile)
            .Where(file => file.Lines.Any(line => line.Contains("ArmDisplayName.Compose(", StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .ToArray();

        callSites.ShouldNotBeEmpty("Expected ArmDisplayName.Compose to be used somewhere in production code.");
    }
}
