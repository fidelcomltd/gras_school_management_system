namespace SchoolManagement.Domain.Classes;

/// <summary>
/// One seeded section (spec 6.4.2). Fixed id and concurrency-token seed value, same reasoning as
/// <see cref="SeededLevelDefinition"/>. <see cref="RatesTraits"/> is TASK-0083 ruling R1, not spec
/// 6.4.2 — Primary rates traits, Nursery does not (it rates development domains instead, via
/// <c>DevelopmentDomain.SectionId</c>, independently of this flag).
/// </summary>
public sealed record SeededSectionDefinition(Guid Id, string Name, bool RatesTraits, Guid Version);

/// <summary>One seeded level (spec 6.4.2). Fixed id and concurrency-token seed value, same reasoning as <c>SeededRoleDefinition</c>.</summary>
public sealed record SeededLevelDefinition(Guid Id, string Name, Guid SectionId, int ProgressionOrder, Guid? NextLevelId, Guid Version);

/// <summary>
/// Spec 6.4.2's seeded rows: "Nursery 1, Nursery 2, Nursery 3 in section Nursery, then Primary 1 to
/// Primary 6 in section Primary, chained in that order, with Primary 6 as the graduating level," and
/// two sections, Nursery and Primary. The SINGLE source both <c>SectionConfiguration</c>/
/// <c>ClassLevelConfiguration</c>'s <c>HasData</c> seeds and <c>ApiTestFixture</c>'s post-TRUNCATE
/// reseed are built from — same relationship <c>SeededRoles.All</c> already has with its own consumers.
/// </summary>
/// <remarks>
/// Nothing outside this file and the migration that seeds from it assumes these nine rows, their
/// names, or that the chain stays inside a section (spec 6.4.2's own instruction, and the eight rules
/// in <see cref="ProgressionChainGuard"/> operate over whatever set is passed to them) — the chain
/// deliberately crosses the Nursery/Primary section boundary at Nursery 3 → Primary 1.
/// </remarks>
public static class SeededClassLevels
{
    /// <summary>Fixed, documented section ids. Never regenerate — an existing database depends on these.</summary>
    public static readonly Guid NurserySectionId = new("00000000-0000-0000-0000-000000000301");

    /// <inheritdoc cref="NurserySectionId"/>
    public static readonly Guid PrimarySectionId = new("00000000-0000-0000-0000-000000000302");

    /// <summary>Sentinel "installed, not a real event" timestamp — same convention as <c>SeededRoles.SeedTimestamp</c>.</summary>
    public static readonly DateTimeOffset SeedTimestamp = DateTimeOffset.UnixEpoch;

    private static readonly Guid NurserySectionVersion = new("00000000-0000-0000-0000-000000000303");
    private static readonly Guid PrimarySectionVersion = new("00000000-0000-0000-0000-000000000304");

    /// <summary>The two seeded sections.</summary>
    public static readonly IReadOnlyList<SeededSectionDefinition> Sections =
    [
        new(NurserySectionId, "Nursery", RatesTraits: false, NurserySectionVersion),
        new(PrimarySectionId, "Primary", RatesTraits: true, PrimarySectionVersion),
    ];

    private static readonly Guid Nursery1Id = new("00000000-0000-0000-0000-000000000311");
    private static readonly Guid Nursery2Id = new("00000000-0000-0000-0000-000000000312");
    private static readonly Guid Nursery3Id = new("00000000-0000-0000-0000-000000000313");
    private static readonly Guid Primary1Id = new("00000000-0000-0000-0000-000000000314");
    private static readonly Guid Primary2Id = new("00000000-0000-0000-0000-000000000315");
    private static readonly Guid Primary3Id = new("00000000-0000-0000-0000-000000000316");
    private static readonly Guid Primary4Id = new("00000000-0000-0000-0000-000000000317");
    private static readonly Guid Primary5Id = new("00000000-0000-0000-0000-000000000318");
    private static readonly Guid Primary6Id = new("00000000-0000-0000-0000-000000000319");

    private static readonly Guid Nursery1Version = new("00000000-0000-0000-0000-000000000321");
    private static readonly Guid Nursery2Version = new("00000000-0000-0000-0000-000000000322");
    private static readonly Guid Nursery3Version = new("00000000-0000-0000-0000-000000000323");
    private static readonly Guid Primary1Version = new("00000000-0000-0000-0000-000000000324");
    private static readonly Guid Primary2Version = new("00000000-0000-0000-0000-000000000325");
    private static readonly Guid Primary3Version = new("00000000-0000-0000-0000-000000000326");
    private static readonly Guid Primary4Version = new("00000000-0000-0000-0000-000000000327");
    private static readonly Guid Primary5Version = new("00000000-0000-0000-0000-000000000328");
    private static readonly Guid Primary6Version = new("00000000-0000-0000-0000-000000000329");

    /// <summary>The nine seeded levels, in chain order (Nursery 1 the entry level, Primary 6 graduating).</summary>
    public static readonly IReadOnlyList<SeededLevelDefinition> Levels =
    [
        new(Nursery1Id, "Nursery 1", NurserySectionId, 1, Nursery2Id, Nursery1Version),
        new(Nursery2Id, "Nursery 2", NurserySectionId, 2, Nursery3Id, Nursery2Version),
        new(Nursery3Id, "Nursery 3", NurserySectionId, 3, Primary1Id, Nursery3Version),
        new(Primary1Id, "Primary 1", PrimarySectionId, 4, Primary2Id, Primary1Version),
        new(Primary2Id, "Primary 2", PrimarySectionId, 5, Primary3Id, Primary2Version),
        new(Primary3Id, "Primary 3", PrimarySectionId, 6, Primary4Id, Primary3Version),
        new(Primary4Id, "Primary 4", PrimarySectionId, 7, Primary5Id, Primary4Version),
        new(Primary5Id, "Primary 5", PrimarySectionId, 8, Primary6Id, Primary5Version),
        new(Primary6Id, "Primary 6", PrimarySectionId, 9, null, Primary6Version),
    ];
}
