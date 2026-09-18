using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Pins Appendix F.3's 11 seeded affective and 8 seeded psychomotor traits, in printed order.</summary>
public sealed class TraitSeedTests
{
    [Fact]
    public void AffectiveTraits_AreTheElevenNamedInAppendixFThreePrintedOrder()
    {
        TraitSeed.AffectiveTraits.Count.ShouldBe(11);
        TraitSeed.AffectiveTraits.Select(trait => trait.Name).ShouldBe(
        [
            "Conduct",
            "Punctuality",
            "Honesty",
            "Neatness",
            "Attitude",
            "Attentiveness",
            "Co-operation",
            "Skills",
            "Perseverance",
            "Obedient",
            "Fluency",
        ]);
        TraitSeed.AffectiveTraits.ShouldAllBe(trait => trait.Domain == TraitDomain.Affective);
        TraitSeed.AffectiveTraits.Select(trait => trait.DisplayOrder).ShouldBe(Enumerable.Range(1, 11));
    }

    [Fact]
    public void PsychomotorTraits_AreTheEightNamedInAppendixFThreePrintedOrder()
    {
        TraitSeed.PsychomotorTraits.Count.ShouldBe(8);
        TraitSeed.PsychomotorTraits.Select(trait => trait.Name).ShouldBe(
        [
            "Sports",
            "Social activities",
            "Painting and drawing",
            "Hand writing",
            "Mathematical Skills",
            "Reasoning",
            "Health",
            "Creativity",
        ]);
        TraitSeed.PsychomotorTraits.ShouldAllBe(trait => trait.Domain == TraitDomain.Psychomotor);
        TraitSeed.PsychomotorTraits.Select(trait => trait.DisplayOrder).ShouldBe(Enumerable.Range(1, 8));
    }

    [Fact]
    public void NoNameRepeatsAcrossEitherDomain()
    {
        var allNames = TraitSeed.AffectiveTraits.Concat(TraitSeed.PsychomotorTraits).Select(trait => trait.Name);

        allNames.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(19);
    }
}
