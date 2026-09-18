namespace SchoolManagement.IntegrationTests.Fixtures;

/// <summary>
/// The §8.4 regression fixture (TASK-0071 stage 3; product-specification/13-result-computation-rules.md
/// §8.4/§8.5, restated in the card's own table on 2026-09-16 against 20/20/60 and the nine-band scale
/// — use these numbers, not any found elsewhere). Primary 3A (28 pupils) and Primary 3B (26), four
/// subjects (English Studies, Mathematics, Basic Science and Technology, Cultural and Creative Arts).
/// </summary>
/// <remarks>
/// §8.4 gives AGGREGATES, not 54 pupils' marks — constructing them is this card's own job. Every named
/// pupil's marks are exact from the spec table; every unnamed "Pupil3A_xx"/"Pupil3B_xx" is a FILLER
/// whose marks exist only to make the stated sums, highs, lows and positions come out exactly, while
/// keeping every filler's total_obtained clear of every named pupil's total (nobody else reaches
/// Adaeze's 320 in 3A, or Funmi's 322/Emeka's 333 in 3B) so no filler can shift a named rank or create
/// an unintended tie. Plain data, not calculation — <see cref="Mark.SubjectTotal"/> is spec 8.3's own
/// formula (ca_total plus exam, or ca_total alone when absent), never the engine's.
///
/// Linked (not project-referenced) into SchoolManagement.UnitTests so the self-check test
/// (ResultComputationFixtureSelfCheckTests) can assert these aggregates with no database and no engine
/// — same physical file, same data, both projects; see SchoolManagement.UnitTests.csproj.
///
/// The CA-total column has no maximum anywhere in this file, per spec 6.2.13: it is the sum of every
/// non-examination component, which happens to be 40 under the seeded 1st-CA-20/2nd-CA-20 structure
/// this fixture uses, but that number is never written here as a heading, a label or a constant.
/// </remarks>
public static class ResultComputationFixtureData
{
    public const string Arm3A = "3A";
    public const string Arm3B = "3B";

    public const string English = "English Studies";
    public const string Mathematics = "Mathematics";
    public const string BasicScienceAndTechnology = "Basic Science and Technology";
    public const string CulturalAndCreativeArts = "Cultural and Creative Arts";

    /// <summary>Print order, matching spec 8.4.2's table.</summary>
    public static readonly IReadOnlyList<string> Subjects =
        [English, Mathematics, BasicScienceAndTechnology, CulturalAndCreativeArts];

    // Pupils the card's and spec's assertions name directly.
    public const string Adaeze = "Adaeze Okafor";
    public const string Musa = "Musa Ibrahim";
    public const string Chidi = "Chidi Nwosu";
    public const string Ngozi = "Ngozi Eze";
    public const string Emeka = "Emeka Adigwe";
    public const string Funmi = "Funmi Ajayi";

    /// <summary>The filler who lands the Mathematics tie-break's "next position 8th" (spec 8.4.5), on 74.</summary>
    public const string MathsRank8Pupil = "Pupil3A_02";

    /// <summary>All 54 pupils: 28 in Primary 3A, 26 in Primary 3B.</summary>
    public static readonly IReadOnlyList<PupilRow> Pupils =
    [
        new(Adaeze, Arm3A, new(18,16,52), new(17,16,45), new(14,12,38), new(19,18,55)),
        new(Musa, Arm3A, new(20,20,45), new(13,11,null,true), new(20,20,1), new(20,20,14)),
        new(Chidi, Arm3A, new(20,20,48), new(20,17,41), new(20,20,39), new(20,20,44)),
        new(Ngozi, Arm3A, new(20,20,51), new(20,20,48), new(20,20,42), new(20,20,36)),
        new("Pupil3A_01", Arm3A, new(20,20,45), new(20,20,33), new(20,20,41), new(20,20,14)),
        new("Pupil3A_02", Arm3A, new(20,20,4), new(20,20,34), new(20,20,23), new(20,20,51)),
        new("Pupil3A_03", Arm3A, new(20,20,4), new(20,11,0), new(20,20,1), new(20,20,14)),
        new("Pupil3A_04", Arm3A, new(20,20,4), new(20,11,0), new(20,20,41), new(20,20,14)),
        new("Pupil3A_05", Arm3A, new(20,20,45), new(20,20,33), new(20,20,1), new(20,20,14)),
        new("Pupil3A_06", Arm3A, new(20,20,45), new(20,11,0), new(20,20,41), new(20,20,15)),
        new("Pupil3A_07", Arm3A, new(20,20,4), new(20,20,33), new(20,20,41), new(20,20,51)),
        new("Pupil3A_08", Arm3A, new(20,20,4), new(20,20,44), new(20,20,41), new(20,20,51)),
        new("Pupil3A_09", Arm3A, new(20,20,21), new(20,11,0), new(20,20,1), new(20,20,51)),
        new("Pupil3A_10", Arm3A, new(20,20,4), new(20,11,0), new(20,20,41), new(20,20,51)),
        new("Pupil3A_11", Arm3A, new(20,20,45), new(20,20,33), new(20,19,0), new(20,20,51)),
        new("Pupil3A_12", Arm3A, new(20,20,4), new(20,20,40), new(20,20,1), new(20,20,14)),
        new("Pupil3A_13", Arm3A, new(20,20,45), new(20,20,30), new(20,20,41), new(20,20,14)),
        new("Pupil3A_14", Arm3A, new(20,20,4), new(20,20,45), new(20,20,1), new(20,20,14)),
        new("Pupil3A_15", Arm3A, new(20,20,45), new(20,11,0), new(20,20,23), new(20,20,51)),
        new("Pupil3A_16", Arm3A, new(20,20,4), new(20,11,0), new(20,20,41), new(20,20,51)),
        new("Pupil3A_17", Arm3A, new(20,20,45), new(20,11,0), new(20,20,1), new(20,20,51)),
        new("Pupil3A_18", Arm3A, new(20,20,45), new(20,20,41), new(20,20,41), new(20,20,17)),
        new("Pupil3A_19", Arm3A, new(20,20,4), new(20,20,33), new(20,20,1), new(20,20,51)),
        new("Pupil3A_20", Arm3A, new(20,20,45), new(20,20,33), new(20,20,23), new(20,20,14)),
        new("Pupil3A_21", Arm3A, new(20,20,45), new(20,11,0), new(20,20,10), new(20,20,11)),
        new("Pupil3A_22", Arm3A, new(20,20,4), new(20,20,33), new(20,20,1), new(20,20,14)),
        new("Pupil3A_23", Arm3A, new(20,20,45), new(20,11,0), new(20,20,1), new(20,20,51)),
        new("Pupil3A_24", Arm3A, new(20,20,45), new(20,20,33), new(20,20,1), new(20,20,14)),
        new(Emeka, Arm3B, new(20,20,44), new(20,20,43), new(20,20,43), new(20,20,43)),
        new(Funmi, Arm3B, new(20,20,41), new(20,20,41), new(20,20,40), new(20,20,40)),
        new("Pupil3B_01", Arm3B, new(20,20,40), new(20,20,40), new(20,20,40), new(20,20,39)),
        new("Pupil3B_02", Arm3B, new(20,20,39), new(20,20,38), new(20,20,38), new(20,20,38)),
        new("Pupil3B_03", Arm3B, new(20,20,37), new(20,20,37), new(20,20,37), new(20,20,36)),
        new("Pupil3B_04", Arm3B, new(20,20,36), new(20,20,35), new(20,20,35), new(20,20,35)),
        new("Pupil3B_05", Arm3B, new(20,20,34), new(20,20,34), new(20,20,34), new(20,20,33)),
        new("Pupil3B_06", Arm3B, new(20,20,33), new(20,20,32), new(20,20,32), new(20,20,32)),
        new("Pupil3B_07", Arm3B, new(20,20,31), new(20,20,31), new(20,20,31), new(20,20,30)),
        new("Pupil3B_08", Arm3B, new(20,20,30), new(20,20,29), new(20,20,29), new(20,20,29)),
        new("Pupil3B_09", Arm3B, new(20,20,28), new(20,20,28), new(20,20,28), new(20,20,27)),
        new("Pupil3B_10", Arm3B, new(20,20,27), new(20,20,26), new(20,20,26), new(20,20,26)),
        new("Pupil3B_11", Arm3B, new(20,20,25), new(20,20,25), new(20,20,25), new(20,20,24)),
        new("Pupil3B_12", Arm3B, new(20,20,24), new(20,20,23), new(20,20,23), new(20,20,23)),
        new("Pupil3B_13", Arm3B, new(20,20,22), new(20,20,22), new(20,20,22), new(20,20,21)),
        new("Pupil3B_14", Arm3B, new(20,20,21), new(20,20,20), new(20,20,20), new(20,20,20)),
        new("Pupil3B_15", Arm3B, new(20,20,19), new(20,20,19), new(20,20,19), new(20,20,18)),
        new("Pupil3B_16", Arm3B, new(20,20,18), new(20,20,17), new(20,20,17), new(20,20,17)),
        new("Pupil3B_17", Arm3B, new(20,20,16), new(20,20,16), new(20,20,16), new(20,20,15)),
        new("Pupil3B_18", Arm3B, new(20,20,15), new(20,20,14), new(20,20,14), new(20,20,14)),
        new("Pupil3B_19", Arm3B, new(20,20,13), new(20,20,13), new(20,20,13), new(20,20,12)),
        new("Pupil3B_20", Arm3B, new(20,20,12), new(20,20,11), new(20,20,11), new(20,20,11)),
        new("Pupil3B_21", Arm3B, new(20,20,10), new(20,20,10), new(20,20,10), new(20,20,9)),
        new("Pupil3B_22", Arm3B, new(20,20,9), new(20,20,8), new(20,20,8), new(20,20,8)),
        new("Pupil3B_23", Arm3B, new(20,20,7), new(20,20,7), new(20,20,7), new(20,20,6)),
        new("Pupil3B_24", Arm3B, new(20,20,6), new(20,20,5), new(20,20,5), new(20,20,5)),
    ];
}

/// <summary>
/// One pupil's raw marks for one subject: what a class teacher would key into the score sheet.
/// <see cref="CaTotal"/>/<see cref="SubjectTotal"/> are spec 8.3's formula, computed from the raw
/// cells here, not asserted independently. Top-level, not nested in <see cref="ResultComputationFixtureData"/>
/// (CA1034) — it is still only ever used alongside that class.
/// </summary>
public sealed record Mark(int Ca1, int Ca2, int? Exam, bool ExamAbsent = false)
{
    public int CaTotal => Ca1 + Ca2;

    public int SubjectTotal => ExamAbsent ? CaTotal : CaTotal + (Exam ?? 0);
}

/// <summary>One pupil's four subject rows, in <see cref="ResultComputationFixtureData.Subjects"/> order. Top-level for the same reason as <see cref="Mark"/>.</summary>
public sealed record PupilRow(
    string Pupil, string Arm, Mark English, Mark Mathematics, Mark BasicScienceAndTechnology, Mark CulturalAndCreativeArts)
{
    public Mark this[string subject] => subject switch
    {
        ResultComputationFixtureData.English => English,
        ResultComputationFixtureData.Mathematics => Mathematics,
        ResultComputationFixtureData.BasicScienceAndTechnology => BasicScienceAndTechnology,
        ResultComputationFixtureData.CulturalAndCreativeArts => CulturalAndCreativeArts,
        _ => throw new ArgumentOutOfRangeException(nameof(subject), subject, "Unknown subject."),
    };

    /// <summary>Spec 8.3: sum of subject_total across the pupil's subjects (a missing/absent exam still contributes its ca_total).</summary>
    public int TotalObtained =>
        English.SubjectTotal + Mathematics.SubjectTotal + BasicScienceAndTechnology.SubjectTotal + CulturalAndCreativeArts.SubjectTotal;

    /// <summary>Spec 8.3: total_obtained divided by the subject count (always 4 here), unrounded — callers round half up to 2dp themselves.</summary>
    public decimal Average => TotalObtained / 4m;
}
