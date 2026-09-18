namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Spec 6.2.13's three seeded rating scales. The SINGLE source both the install-time migration seed
/// (<c>RatingScaleConfiguration.HasData</c>/<c>RatingScalePointConfiguration.HasData</c>) and
/// <c>ApiTestFixture</c>'s post-TRUNCATE reseed build from — the same relationship
/// <see cref="GradingScaleSeed"/> already has with its own consumers.
/// </summary>
public static class RatingScaleSeed
{
    /// <summary>Nursery development: E/S/I/N. Used by the four nursery development domains (stage 2).</summary>
    public const string NurseryDevelopmentName = "Nursery development";

    /// <summary>Primary trait: E/I/N. Used by the primary affective and psychomotor blocks (stage 3).</summary>
    public const string PrimaryTraitName = "Primary trait";

    /// <summary>Five-point numeric, Appendix A entry 48's legend. Referenced by nothing — retained as an alternative.</summary>
    public const string FivePointNumericName = "Five-point numeric";

    /// <summary>The three seeded scales, in the order printed here.</summary>
    public static readonly IReadOnlyList<RatingScaleInput> SeededScales =
    [
        new(
            NurseryDevelopmentName,
            [
                new("N", "Needs Improvement", 1),
                new("I", "Improving", 2),
                new("S", "Satisfied", 3),
                new("E", "Excellent", 4),
            ]),
        new(
            PrimaryTraitName,
            [
                new("N", "Needs Improvement", 1),
                new("I", "Improving", 2),
                new("E", "Excellent", 3),
            ]),
        new(
            FivePointNumericName,
            [
                new("1", "Shows no regard for observable traits.", 1),
                new("2", "Shows minimal regard for observable traits.", 2),
                new("3", "Shows an acceptable level of observable traits.", 3),
                new("4", "Maintains a high level of observable traits.", 4),
                new("5", "Maintains an excellent degree of observable traits.", 5),
            ]),
    ];
}
