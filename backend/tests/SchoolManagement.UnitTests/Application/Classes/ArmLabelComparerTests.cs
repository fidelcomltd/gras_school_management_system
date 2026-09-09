using SchoolManagement.Application.Classes;

namespace SchoolManagement.UnitTests.Application.Classes;

/// <summary>
/// Spec 6.4.5: arm labels sort "collated naturally... Not alphabetical" — the reason this comparer
/// exists at all, rather than <see cref="StringComparer.OrdinalIgnoreCase"/>.
/// </summary>
public sealed class ArmLabelComparerTests
{
    [Fact]
    public void Compare_PlainLetters_SortsAlphabetically()
    {
        var ordered = new[] { "C", "A", "B" }.OrderBy(label => label, ArmLabelComparer.Instance);

        ordered.ShouldBe(["A", "B", "C"]);
    }

    // The whole reason this comparer exists: plain ordinal string comparison would put "A10" before
    // "A2" (the character '1' sorts before '2'), which is not how a person reading the list expects
    // a numbered set of rooms to order.
    [Fact]
    public void Compare_LabelsWithEmbeddedNumbers_SortsNumericallyNotLexicographically()
    {
        var ordered = new[] { "A10", "A2", "A1" }.OrderBy(label => label, ArmLabelComparer.Instance);

        ordered.ShouldBe(["A1", "A2", "A10"]);
    }

    [Fact]
    public void Compare_IsCaseInsensitiveOnTheNonDigitPortion()
    {
        ArmLabelComparer.Instance.Compare("gold", "Silver").ShouldBeLessThan(0);
    }

    [Fact]
    public void Compare_AShorterLabelThatIsAPrefixOfALonger_SortsFirst()
    {
        var ordered = new[] { "AB", "A" }.OrderBy(label => label, ArmLabelComparer.Instance);

        ordered.ShouldBe(["A", "AB"]);
    }

    [Fact]
    public void Compare_EqualLabels_ReturnsZero() =>
        ArmLabelComparer.Instance.Compare("A", "A").ShouldBe(0);
}
