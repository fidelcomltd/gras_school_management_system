using SchoolManagement.Domain.Classes;

namespace SchoolManagement.UnitTests.Domain.Classes;

/// <summary>
/// Spec 6.4.3's display-name composition rule, and Appendix A entry 12's explicit rejection of making
/// it conditional on how many arms a level has. The "no second implementation" acceptance criterion is
/// <c>ArmDisplayNameCompositionTests</c> in the architecture-tests project.
/// </summary>
public sealed class ArmDisplayNameTests
{
    [Theory]
    [InlineData("Primary 1", "A", "Primary 1A")]
    [InlineData("Nursery 2", "B", "Nursery 2B")]
    [InlineData("Primary 1", "9", "Primary 19")]
    public void Compose_WithASingleAlphanumericLabel_JoinsWithNoSpace(string levelName, string label, string expected) =>
        ArmDisplayName.Compose(levelName, label).ShouldBe(expected);

    [Theory]
    [InlineData("Primary 1", "Gold", "Primary 1 Gold")]
    [InlineData("Primary 2", "Blue Room", "Primary 2 Blue Room")]
    public void Compose_WithAMultiCharacterLabel_JoinsWithOneSpace(string levelName, string label, string expected) =>
        ArmDisplayName.Compose(levelName, label).ShouldBe(expected);

    // Appendix A entry 12: a level with a single arm still renders level + label, never bare level
    // name — the function has no notion of "how many arms this level has," so there is nothing here
    // that could make it conditional even by accident.
    [Fact]
    public void Compose_DoesNotAcceptOrNeedAnArmCountToDecideSpacing()
    {
        ArmDisplayName.Compose("Primary 4", "A").ShouldBe("Primary 4A");
    }
}
