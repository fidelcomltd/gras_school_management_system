namespace SchoolManagement.Domain.Subjects;

/// <summary>
/// Which of the school's two result sheets a seeded subject appears on. NOT a column on
/// <see cref="Subject"/> and not seed data in its own right — spec 6.6.2 has no section field, and
/// section scoping emerges from which levels a mapping points at
/// (<c>04-module-school-settings.md:438</c>). It exists only so
/// <c>POST /subject-mappings/prefill</c> knows which levels to map each subject to.
/// </summary>
public enum SubjectSheet
{
    /// <summary>Appears on the nursery result sheet only (appendix E).</summary>
    Nursery,

    /// <summary>Appears on the primary result sheet only (appendix F).</summary>
    Primary,

    /// <summary>Appears on both sheets.</summary>
    Both,
}

/// <summary>One seeded subject. Fixed id and concurrency-token seed value, same reasoning as <c>SeededLevelDefinition</c>.</summary>
public sealed record SeededSubjectDefinition(Guid Id, string Name, SubjectSheet Sheet, Guid Version);

/// <summary>
/// Spec 6.6.2's seeded rows, corrected against <c>index.md</c> §44 and appendices E and F rather than
/// against §6.6.2's own superseded "no subjects are seeded" text (see the card's was/now table): 28
/// subjects, transcribed verbatim from the school's own two result sheets, spellings included. The
/// five names appearing on both sheets are ONE row each (human ruling 2026-09-16) — seeding all 33
/// names would collide on <see cref="Subject"/>'s case-insensitive unique name index.
/// </summary>
/// <remarks>
/// The SINGLE source both <c>SubjectConfiguration</c>'s <c>HasData</c> seed and
/// <c>ApiTestFixture</c>'s post-TRUNCATE reseed are built from — same relationship
/// <c>SeededClassLevels</c> already has with its own consumers. No <see cref="Subject.Code"/> is
/// seeded (see the card's "Why code is optional" section and TASK-0070's contract-delta amendment 1)
/// and no <c>subject_mapping</c> row is seeded at all — that needs a <c>term_id</c>, and no session or
/// term is seeded, so the 14/19 split is applied later, on demand, by
/// <c>POST /subject-mappings/prefill</c> using <see cref="NurserySheetOrder"/> and
/// <see cref="PrimarySheetOrder"/> below.
/// </remarks>
public static class SeededSubjects
{
    /// <summary>Sentinel "installed, not a real event" timestamp — same convention as <c>SeededRoles.SeedTimestamp</c>.</summary>
    public static readonly DateTimeOffset SeedTimestamp = DateTimeOffset.UnixEpoch;

    /// <summary>The 28 seeded subjects. Order here is grouped N/B/P for readability — NOT the printed sheet order; see the two lists below.</summary>
    public static readonly IReadOnlyList<SeededSubjectDefinition> All =
    [
        new(new Guid("00000000-0000-0000-0000-000000000601"), "Number work", SubjectSheet.Nursery, new Guid("00000000-0000-0000-0000-000000000701")),
        new(new Guid("00000000-0000-0000-0000-000000000602"), "Letter work", SubjectSheet.Nursery, new Guid("00000000-0000-0000-0000-000000000702")),
        new(new Guid("00000000-0000-0000-0000-000000000603"), "Phonics", SubjectSheet.Nursery, new Guid("00000000-0000-0000-0000-000000000703")),
        new(new Guid("00000000-0000-0000-0000-000000000604"), "Pre Science", SubjectSheet.Nursery, new Guid("00000000-0000-0000-0000-000000000704")),
        new(new Guid("00000000-0000-0000-0000-000000000605"), "Social habit", SubjectSheet.Nursery, new Guid("00000000-0000-0000-0000-000000000705")),
        new(new Guid("00000000-0000-0000-0000-000000000606"), "Health habit", SubjectSheet.Nursery, new Guid("00000000-0000-0000-0000-000000000706")),
        new(new Guid("00000000-0000-0000-0000-000000000607"), "Handwriting", SubjectSheet.Nursery, new Guid("00000000-0000-0000-0000-000000000707")),
        new(new Guid("00000000-0000-0000-0000-000000000608"), "Creative skills", SubjectSheet.Nursery, new Guid("00000000-0000-0000-0000-000000000708")),
        new(new Guid("00000000-0000-0000-0000-000000000609"), "Rhyme", SubjectSheet.Nursery, new Guid("00000000-0000-0000-0000-000000000709")),
        new(new Guid("00000000-0000-0000-0000-000000000610"), "Literature", SubjectSheet.Both, new Guid("00000000-0000-0000-0000-000000000710")),
        new(new Guid("00000000-0000-0000-0000-000000000611"), "Computer Science", SubjectSheet.Both, new Guid("00000000-0000-0000-0000-000000000711")),
        new(new Guid("00000000-0000-0000-0000-000000000612"), "Quantitative Reasoning", SubjectSheet.Both, new Guid("00000000-0000-0000-0000-000000000712")),
        new(new Guid("00000000-0000-0000-0000-000000000613"), "Verbal Reasoning", SubjectSheet.Both, new Guid("00000000-0000-0000-0000-000000000713")),
        new(new Guid("00000000-0000-0000-0000-000000000614"), "Christian Religious Knowledge", SubjectSheet.Both, new Guid("00000000-0000-0000-0000-000000000714")),
        new(new Guid("00000000-0000-0000-0000-000000000615"), "Mathematics", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000715")),
        new(new Guid("00000000-0000-0000-0000-000000000616"), "English Language", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000716")),
        new(new Guid("00000000-0000-0000-0000-000000000617"), "Phonics/Diction", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000717")),
        new(new Guid("00000000-0000-0000-0000-000000000618"), "Hand writing", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000718")),
        new(new Guid("00000000-0000-0000-0000-000000000619"), "Basic Science/Tech", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000719")),
        new(new Guid("00000000-0000-0000-0000-000000000620"), "Social Studies", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000720")),
        new(new Guid("00000000-0000-0000-0000-000000000621"), "Health Education", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000721")),
        new(new Guid("00000000-0000-0000-0000-000000000622"), "Creative Art", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000722")),
        new(new Guid("00000000-0000-0000-0000-000000000623"), "Agric Science", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000723")),
        new(new Guid("00000000-0000-0000-0000-000000000624"), "Home Economics", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000724")),
        new(new Guid("00000000-0000-0000-0000-000000000625"), "Civic Education", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000725")),
        new(new Guid("00000000-0000-0000-0000-000000000626"), "History", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000726")),
        new(new Guid("00000000-0000-0000-0000-000000000627"), "French", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000727")),
        new(new Guid("00000000-0000-0000-0000-000000000628"), "Igbo", SubjectSheet.Primary, new Guid("00000000-0000-0000-0000-000000000728")),
    ];

    /// <summary>
    /// The 14 nursery subjects, in the school's own printed row order (appendix E) — NOT the grouped
    /// order <see cref="All"/> lists them in. <c>POST /subject-mappings/prefill</c> writes
    /// <c>display_order</c> from this list's index for every Nursery-section level.
    /// </summary>
    public static readonly IReadOnlyList<string> NurserySheetOrder =
    [
        "Number work", "Letter work", "Phonics", "Quantitative Reasoning", "Verbal Reasoning",
        "Literature", "Pre Science", "Social habit", "Health habit", "Handwriting",
        "Christian Religious Knowledge", "Computer Science", "Creative skills", "Rhyme",
    ];

    /// <summary>
    /// The 19 primary subjects, in the school's own printed row order (appendix F) — NOT the grouped
    /// order <see cref="All"/> lists them in. <c>POST /subject-mappings/prefill</c> writes
    /// <c>display_order</c> from this list's index for every Primary-section level.
    /// </summary>
    /// <remarks>
    /// The five subjects shared with <see cref="NurserySheetOrder"/> happen to sit at identical
    /// positions in both lists (Quantitative Reasoning 4, Verbal Reasoning 5, Literature 6, Christian
    /// Religious Knowledge 11, Computer Science 12) — a coincidence of these two lists, not a rule.
    /// Both lists are kept independent and neither is derived from the other, so a future reordering
    /// of one sheet cannot silently move the other.
    /// </remarks>
    public static readonly IReadOnlyList<string> PrimarySheetOrder =
    [
        "Mathematics", "English Language", "Phonics/Diction", "Quantitative Reasoning",
        "Verbal Reasoning", "Literature", "Hand writing", "Basic Science/Tech", "Social Studies",
        "Health Education", "Christian Religious Knowledge", "Computer Science", "Creative Art",
        "Agric Science", "Home Economics", "Civic Education", "History", "French", "Igbo",
    ];
}
