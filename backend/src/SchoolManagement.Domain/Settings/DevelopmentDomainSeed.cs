namespace SchoolManagement.Domain.Settings;

/// <summary>One seeded indicator's name and printed order within its domain (<see cref="DevelopmentDomainSeed"/>).</summary>
public sealed record DevelopmentIndicatorSeed(string Name, int DisplayOrder);

/// <summary>One seeded domain: its name, printed block order, comment-column choice and ordered indicators (<see cref="DevelopmentDomainSeed"/>).</summary>
public sealed record DevelopmentDomainSeedRow(
    string Name,
    int DisplayOrder,
    bool AllowsIndicatorComment,
    IReadOnlyList<DevelopmentIndicatorSeed> Indicators);

/// <summary>
/// Spec 6.2.13 / Appendix E.3's four seeded nursery development domains and their indicators. The
/// SINGLE source both the install-time migration seed
/// (<c>DevelopmentDomainConfiguration.HasData</c>/<c>DevelopmentIndicatorConfiguration.HasData</c>)
/// and <c>ApiTestFixture</c>'s post-TRUNCATE reseed build from — the same relationship
/// <see cref="RatingScaleSeed"/> already has with its own consumers. Ids, the owning section
/// (Nursery) and the rating scale (Nursery development) are NOT part of this type — those are fixed,
/// documented ids assigned only in Infrastructure's configuration classes, the same split
/// <see cref="RatingScaleSeed"/> keeps from <c>RatingScaleConfiguration</c>.
/// </summary>
/// <remarks>
/// Appendix E.3's parenthetical indicator counts for domains 2 and 3 ("13 indicators", "16
/// indicators") do not match the number of items actually listed there (14 and 15) — the domain and
/// grand totals (4 domains, 45 indicators) agree either way, so this is an arithmetic slip in the
/// section header text, not in the transcribed list, which the appendix itself says is "confirmed
/// correct by the school." This seed follows the LISTED items, 14 and 15, as the ground truth.
/// Flagged for the human at TASK-0072 stage 2a close; not silently reconciled.
/// </remarks>
public static class DevelopmentDomainSeed
{
    /// <summary>Domain 1 name (Appendix E.3).</summary>
    public const string MathsReadinessName = "Maths Readiness";

    /// <summary>Domain 2 name (Appendix E.3).</summary>
    public const string LanguageCommunicationName = "Language/Communication Development";

    /// <summary>Domain 3 name (Appendix E.3).</summary>
    public const string PersonalPhysicalName = "Personal & Physical Development";

    /// <summary>Domain 4 name (Appendix E.3).</summary>
    public const string SocialEmotionalName = "Social & Emotional";

    /// <summary>The four seeded domains, in Appendix E.3's printed order. 45 indicators in total.</summary>
    public static readonly IReadOnlyList<DevelopmentDomainSeedRow> NurseryDomains =
    [
        new(
            MathsReadinessName,
            1,
            true,
            Indicators(
                "Ability to Count",
                "Write Number Clearly",
                "Ability to Recognize numbers",
                "Ability to Reason and Answer question")),
        new(
            LanguageCommunicationName,
            2,
            true,
            Indicators(
                "Ability to recite letters",
                "Ability to Recognize Upper/Lower case",
                "Can write Upper/Lower case",
                "Can construct simple sentence",
                "Can identify object",
                "Can recognize similarities & differences",
                "Know Letters and Alphabet in sequence",
                "Can Trace Letters & Object",
                "State own name and write",
                "Expression of own feeling and thought",
                "Listen attentively and contribute to discussion",
                "Shows interest in books",
                "Speaks clearly",
                "Able to speak with right vocabulary")),
        new(
            PersonalPhysicalName,
            3,
            true,
            Indicators(
                "Can Recognize different Colours",
                "Can recognize different shapes",
                "Hold Pencils Correctly & firmly",
                "Take simply instruction/directions",
                "Work independently",
                "Can run and jump well",
                "Can Catch, Bounce, and throw ball",
                "Move all parts of the body very well",
                "Fit small items together",
                "Logical reasoning",
                "Cleanliness",
                "Persistence",
                "Wear cloth independently",
                "Potty trained",
                "Home work on High Quality")),
        new(
            SocialEmotionalName,
            4,
            true,
            Indicators(
                "Happy at School",
                "Behaves Well in School",
                "Etiquette and manners",
                "Expression and Emotions and Feeling",
                "Accept Correction",
                "Honesty",
                "Obedient to Instruction",
                "Behaves well in Class",
                "Work and Mixes well with Others",
                "Concentration",
                "Attendance to Class",
                "Punctuality")),
    ];

    private static List<DevelopmentIndicatorSeed> Indicators(params string[] names) =>
        names.Select((name, index) => new DevelopmentIndicatorSeed(name, index + 1)).ToList();
}
