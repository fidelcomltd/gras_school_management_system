using SchoolManagement.IntegrationTests.Fixtures;
using static SchoolManagement.IntegrationTests.Fixtures.ResultComputationFixtureData;

namespace SchoolManagement.UnitTests.Application.Results.Computation;

/// <summary>
/// Proves TASK-0071 stage 3's §8.4 fixture (<see cref="ResultComputationFixtureData"/>) is itself
/// correct — every aggregate spec 8.4/8.5 states, recomputed HERE directly from the raw marks, with NO
/// <c>ResultComputationEngine</c> involved. If this fails, the fixture is wrong. If this passes and the
/// engine (the fixture-driven integration test) still disagrees, the engine is wrong — the two must
/// never be conflated by "fixing" one to match the other.
/// </summary>
/// <remarks>
/// Namespaced alongside <c>ResultComputationEngineTests</c> (<c>Application/Results/Computation</c>),
/// not under a bare <c>SchoolManagement.UnitTests.Results</c> — this assembly also references
/// <c>Microsoft.AspNetCore.Http</c> transitively (via the Api project), whose unqualified
/// <c>Results.Ok()</c> calls in <c>PrivilegeDeclarationGuardTests</c>/<c>CsrfDeclarationGuardTests</c>
/// would otherwise resolve to a sibling <c>SchoolManagement.UnitTests.Results</c> namespace instead of
/// <c>Microsoft.AspNetCore.Http.Results</c> and fail to build (CS0234) — found the hard way while
/// verifying this file.
/// </remarks>
public sealed class ResultComputationFixtureSelfCheckTests
{
    // Qualified explicitly: this file's namespace (SchoolManagement.UnitTests.Application.Results.Computation)
    // sits under SchoolManagement.UnitTests.Application, which ALSO has a Pupils child namespace (mirroring
    // Application/Pupils/*Tests.cs) — the same unqualified-identifier collision documented on this class's
    // own remarks, one level down. An unqualified `Pupils` here binds to that namespace, not the static-using'd field.
    private static readonly IReadOnlyList<PupilRow> All = ResultComputationFixtureData.Pupils;
    private static readonly List<PupilRow> Arm3APupils = All.Where(pupil => pupil.Arm == Arm3A).ToList();
    private static readonly List<PupilRow> Arm3BPupils = All.Where(pupil => pupil.Arm == Arm3B).ToList();

    [Fact]
    public void Fixture_HasTheStatedPupilCounts()
    {
        Arm3APupils.Count.ShouldBe(28);
        Arm3BPupils.Count.ShouldBe(26);
        All.Count.ShouldBe(54);
    }

    // ---- Adaeze: four subject totals and grades, spec 8.4.2 ---------------------------------------
    [Theory]
    [InlineData(English, 86, "A")]
    [InlineData(Mathematics, 78, "B")]
    [InlineData(BasicScienceAndTechnology, 64, "C+")]
    [InlineData(CulturalAndCreativeArts, 92, "A+")]
    public void Adaeze_SubjectTotalsAndGrades_MatchSpec842(string subject, int expectedTotal, string expectedGrade)
    {
        var mark = Row(Adaeze)[subject];
        mark.SubjectTotal.ShouldBe(expectedTotal);
        Grade(mark.SubjectTotal).ShouldBe(expectedGrade);
    }

    [Fact]
    public void Adaeze_TotalObtainedAverageAndOverallGrade_MatchSpec842()
    {
        var adaeze = Row(Adaeze);
        adaeze.TotalObtained.ShouldBe(320);
        adaeze.Average.ShouldBe(80.00m);
        Grade((int)adaeze.Average).ShouldBe("B");
    }

    // ---- Musa: exam-absent Mathematics, spec 8.4.3 -------------------------------------------------
    [Fact]
    public void Musa_MathematicsRow_IsCaOnlyAndAbsent()
    {
        var musaMaths = Row(Musa)[Mathematics];
        musaMaths.ExamAbsent.ShouldBeTrue();
        musaMaths.Exam.ShouldBeNull();
        musaMaths.SubjectTotal.ShouldBe(24);
        Grade(musaMaths.SubjectTotal).ShouldBe("E");
    }

    // ---- Chidi: Mathematics exam 41 (tie-break value), total 78 tied with Adaeze; overall 329 ------
    [Fact]
    public void Chidi_MathematicsExamAndTotals_MatchSpec()
    {
        var chidiMaths = Row(Chidi)[Mathematics];
        chidiMaths.Exam.ShouldBe(41);
        chidiMaths.SubjectTotal.ShouldBe(78);
        Row(Chidi).TotalObtained.ShouldBe(329);
        Row(Chidi).Average.ShouldBe(82.25m);
    }

    // ---- Named pupils' totals/averages, spec 8.4.6/8.4.7 --------------------------------------------
    [Theory]
    [InlineData(Ngozi, 337, 84.25)]
    [InlineData(Emeka, 333, 83.25)]
    [InlineData(Funmi, 322, 80.50)]
    public void NamedPupils_TotalsAndAverages_MatchSpec(string pupil, int expectedTotal, decimal expectedAverage)
    {
        var row = Row(pupil);
        row.TotalObtained.ShouldBe(expectedTotal);
        row.Average.ShouldBe(expectedAverage);
    }

    // ---- Class statistics for Primary 3A, spec 8.4.4 (Mathematics is separate: Musa's absence) ------
    [Theory]
    [InlineData(English, 28, 1915, 91, 44)]
    [InlineData(BasicScienceAndTechnology, 28, 1683, 82, 39)]
    [InlineData(CulturalAndCreativeArts, 28, 2010, 92, 51)]
    public void Arm3A_ClassStatistics_MatchSpec844(string subject, int expectedCounted, int expectedSum, int expectedHigh, int expectedLow)
    {
        var counted = Arm3APupils.Select(pupil => pupil[subject]).Where(mark => !mark.ExamAbsent).ToList();
        counted.Count.ShouldBe(expectedCounted);
        counted.Sum(mark => mark.SubjectTotal).ShouldBe(expectedSum);
        counted.Max(mark => mark.SubjectTotal).ShouldBe(expectedHigh);
        counted.Min(mark => mark.SubjectTotal).ShouldBe(expectedLow);
    }

    [Fact]
    public void Arm3A_Mathematics_CountedPopulationExcludesMusa_LowestExcludesHis24()
    {
        var ranked = Arm3APupils.Select(pupil => pupil[Mathematics]).ToList();
        ranked.Count.ShouldBe(28); // ranked population INCLUDES Musa (spec 8.4.3)

        var counted = ranked.Where(mark => !mark.ExamAbsent).ToList();
        counted.Count.ShouldBe(27); // counted population EXCLUDES Musa
        counted.Sum(mark => mark.SubjectTotal).ShouldBe(1612);
        counted.Max(mark => mark.SubjectTotal).ShouldBe(88);
        counted.Min(mark => mark.SubjectTotal).ShouldBe(31); // NOT Musa's 24 -- he is excluded from this population
    }

    // ---- Mathematics tie, spec 8.4.5: Adaeze and Chidi share 6th, next position (74) is 8th --------
    [Fact]
    public void Arm3A_MathematicsTie_SharesPositionAndSkipsToEighth()
    {
        var ranked = Arm3APupils.Select(pupil => (pupil.Pupil, Total: pupil[Mathematics].SubjectTotal)).ToList();
        var ranks = SharedPositionRank(ranked);

        ranks[Adaeze].ShouldBe(6);
        ranks[Chidi].ShouldBe(6);
        ranks[MathsRank8Pupil].ShouldBe(8); // the skip: nobody is 7th
        ranked.Single(item => item.Pupil == MathsRank8Pupil).Total.ShouldBe(74);
    }

    // ---- Nobody else reaches a named total (guards against an unintended tie/rank shift) ------------
    [Fact]
    public void Arm3A_NoOtherPupilReachesAdaezesTotal()
    {
        Arm3APupils.Where(pupil => pupil.Pupil is not (Ngozi or Chidi or Adaeze))
            .ShouldAllBe(pupil => pupil.TotalObtained < 320);
    }

    [Fact]
    public void Arm3B_NoOtherPupilReachesAn80Average()
    {
        Arm3BPupils.Where(pupil => pupil.Pupil is not (Emeka or Funmi))
            .ShouldAllBe(pupil => pupil.Average < 80.00m);
    }

    // ---- Arm position within Primary 3A, spec 8.4.6 --------------------------------------------------
    [Fact]
    public void Arm3A_OverallPositions_MatchSpec846()
    {
        var ranked = Arm3APupils.OrderByDescending(pupil => pupil.TotalObtained).ToList();
        ranked[0].Pupil.ShouldBe(Ngozi);
        ranked[1].Pupil.ShouldBe(Chidi);
        ranked[2].Pupil.ShouldBe(Adaeze);
    }

    // ---- Level position across Primary 3 (54 pupils), spec 8.4.7 -------------------------------------
    [Fact]
    public void LevelPosition_TopFive_MatchSpec847()
    {
        var ranked = All.OrderByDescending(pupil => pupil.Average).ToList();
        ranked[0].Pupil.ShouldBe(Ngozi);
        ranked[0].Average.ShouldBe(84.25m);
        ranked[1].Pupil.ShouldBe(Emeka);
        ranked[1].Average.ShouldBe(83.25m);
        ranked[2].Pupil.ShouldBe(Chidi);
        ranked[2].Average.ShouldBe(82.25m);
        ranked[3].Pupil.ShouldBe(Funmi);
        ranked[3].Average.ShouldBe(80.50m);
        ranked[4].Pupil.ShouldBe(Adaeze);
        ranked[4].Average.ShouldBe(80.00m);
    }

    // ---- helpers --------------------------------------------------------------------------------------
    private static PupilRow Row(string pupil) => All.Single(candidate => candidate.Pupil == pupil);

    /// <summary>The nine seeded bands (spec 6.2.13), duplicated here as plain literal data so this
    /// self-check never depends on <c>GradingScaleSeed</c> or the engine's own band resolution.</summary>
    private static string Grade(int total) => total switch
    {
        >= 90 and <= 100 => "A+",
        >= 85 and <= 89 => "A",
        >= 75 and <= 84 => "B",
        >= 70 and <= 74 => "B-",
        >= 60 and <= 69 => "C+",
        >= 50 and <= 59 => "C",
        >= 40 and <= 49 => "D",
        >= 20 and <= 39 => "E",
        >= 0 and <= 19 => "F",
        _ => throw new ArgumentOutOfRangeException(nameof(total), total, "No band."),
    };

    /// <summary>Spec 6.7.6's "1224" competition rank, shared-position (the fixture's default rule):
    /// tied items share a rank, the next rank skips by the number tied. Deliberately independent of
    /// <c>CompetitionRanker</c>.</summary>
    private static Dictionary<string, int> SharedPositionRank(IReadOnlyList<(string Pupil, int Total)> items)
    {
        var sorted = items.OrderByDescending(item => item.Total).ToList();
        var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < sorted.Count; index++)
        {
            ranks[sorted[index].Pupil] = index > 0 && sorted[index - 1].Total == sorted[index].Total
                ? ranks[sorted[index - 1].Pupil]
                : index + 1;
        }

        return ranks;
    }
}
