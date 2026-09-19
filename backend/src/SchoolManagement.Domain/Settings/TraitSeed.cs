namespace SchoolManagement.Domain.Settings;

/// <summary>One seeded trait's domain, name and printed order (<see cref="TraitSeed"/>).</summary>
public sealed record TraitSeedRow(TraitDomain Domain, string Name, int DisplayOrder);

/// <summary>
/// Spec 6.2.13's "Trait lists replaced" / Appendix F.3's 11 seeded affective and 8 seeded psychomotor
/// traits — the school's own, replacing the guessed lists 6.2.7 seeded before the school's form
/// arrived. The SINGLE source both the install-time migration seed (<c>TraitConfiguration.HasData</c>)
/// and <c>ApiTestFixture</c>'s post-TRUNCATE reseed build from — the same relationship
/// <see cref="DevelopmentDomainSeed"/> already has with its own consumers.
/// </summary>
public static class TraitSeed
{
    /// <summary>Appendix F.3: "Seeded affective traits (11)", in printed order.</summary>
    public static readonly IReadOnlyList<TraitSeedRow> AffectiveTraits = Traits(
        TraitDomain.Affective,
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
        "Fluency");

    /// <summary>Appendix F.3: "Seeded psychomotor traits (8)", in printed order.</summary>
    public static readonly IReadOnlyList<TraitSeedRow> PsychomotorTraits = Traits(
        TraitDomain.Psychomotor,
        "Sports",
        "Social activities",
        "Painting and drawing",
        "Hand writing",
        "Mathematical Skills",
        "Reasoning",
        "Health",
        "Creativity");

    private static List<TraitSeedRow> Traits(TraitDomain domain, params string[] names) =>
        names.Select((name, index) => new TraitSeedRow(domain, name, index + 1)).ToList();
}
