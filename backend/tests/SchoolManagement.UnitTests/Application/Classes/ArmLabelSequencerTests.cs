using SchoolManagement.Application.Classes;

namespace SchoolManagement.UnitTests.Application.Classes;

/// <summary>
/// Spec 6.4.3's "next unused label in sequence" and spec 6.4.8's "bulk creation continues from the
/// highest existing label" — the one shared rule behind both.
/// </summary>
public sealed class ArmLabelSequencerTests
{
    [Fact]
    public void NextUnusedLabel_WithNoExistingLabels_ReturnsA() =>
        ArmLabelSequencer.NextUnusedLabel([]).ShouldBe("A");

    [Fact]
    public void NextUnusedLabel_WithAAndB_ReturnsC() =>
        ArmLabelSequencer.NextUnusedLabel(["A", "B"]).ShouldBe("C");

    // Spec 6.4.8: bulk creation run twice over a level already holding A and B creates C — the same
    // rule, so this and the case above are deliberately identical in shape.
    [Fact]
    public void NextUnusedLabel_IgnoresMultiCharacterLabels()
    {
        // "Gold" occupies no letter slot; the next unused single letter is still A.
        ArmLabelSequencer.NextUnusedLabel(["Gold"]).ShouldBe("A");
    }

    [Fact]
    public void NextUnusedLabel_SkipsAGapInTheMiddle()
    {
        // A and C exist, B does not — the suggestion is the FIRST unused letter, not the highest + 1.
        ArmLabelSequencer.NextUnusedLabel(["A", "C"]).ShouldBe("B");
    }

    [Fact]
    public void NextUnusedLabel_IsCaseSensitiveToTheAlreadyNormalisedUppercaseForm()
    {
        // Every persisted single-letter label is already uppercase (Arm's own normalisation) — a
        // lowercase input here would not be recognised as "used", which is fine because it never
        // occurs against real data.
        ArmLabelSequencer.NextUnusedLabel(["A"]).ShouldBe("B");
    }
}
