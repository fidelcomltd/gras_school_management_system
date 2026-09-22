using SchoolManagement.Application.Results.Annual;
using SchoolManagement.Application.Results.Computation;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>Spec 6.7.10 and the 8.4.9 worked example.</summary>
public sealed class AnnualComputationTests
{
    private static readonly Guid English = Guid.Parse("22222222-0000-7000-8000-000000000001");
    private static readonly Guid Maths = Guid.Parse("22222222-0000-7000-8000-000000000002");
    private static readonly Guid Adaeze = Guid.Parse("33333333-0000-7000-8000-000000000001");

    private static readonly ComputationGradingBand[] Bands =
    [
        new(90, 100, "A+", "Distinction"),
        new(80, 89, "B", "Very good"),
        new(40, 79, "C", "Credit"),
        new(0, 39, "F", "Fail"),
    ];

    [Fact]
    public void WorkedExample_SimpleAverage_Is80Point25_GradeB_Promoted()
    {
        var output = Single(Compute([AdaezeInput()], Rules()));

        output.CumulativeAverage.ShouldBe(80.25m);
        output.CumulativeGrade.ShouldBe("B");
        output.TermsCounted.ShouldBe(3);
        output.TermAverages.ShouldBe([80.00m, 78.25m, 82.50m]);
        output.GrandTotal.ShouldBe(320 + 313 + 330);
        output.Subjects.Single(subject => subject.SubjectId == English).Mean.ShouldBe(85.33m);
        output.Subjects.Single(subject => subject.SubjectId == Maths).Mean.ShouldBe(76.33m);
        output.ProposedOutcome.ShouldBe(PromotionOutcome.Promoted);
        output.Position.ShouldBe(1);
    }

    [Fact]
    public void WorkedExample_Weighted20_30_50_Is80Point73() =>
        AnnualComputation.CumulativeAverage(AdaezeInput().Terms, Rules() with { Method = AnnualMethod.Weighted, WeightFirst = 20, WeightSecond = 30, WeightThird = 50 })
            .ShouldBe(80.73m);

    [Fact]
    public void TwoTermsWeighted_RescalesTheWeightsOfTheTermsSat()
    {
        AnnualTermInput[] terms = [Term(2, 70.00m, []), Term(3, 80.00m, [])];

        // (70 * 30 + 80 * 50) / 80 = 76.25
        AnnualComputation.CumulativeAverage(terms, Rules() with { Method = AnnualMethod.Weighted, WeightFirst = 20, WeightSecond = 30, WeightThird = 50 })
            .ShouldBe(76.25m);
    }

    [Fact]
    public void Ranking_SharesTies_SkipsTheNext_AndLeavesASingleTermUnranked()
    {
        var outputs = Compute(
            [
                Pupil(1, [Term(1, 80m, []), Term(2, 80m, []), Term(3, 80m, [])]),
                Pupil(2, [Term(1, 80m, []), Term(2, 80m, []), Term(3, 80m, [])]),
                Pupil(3, [Term(2, 70m, []), Term(3, 72m, [])]),
                Pupil(4, [Term(3, 95m, [])]),
            ],
            Rules());

        outputs.Single(output => output.PupilId == Id(1)).Position.ShouldBe(1);
        outputs.Single(output => output.PupilId == Id(1)).PositionTied.ShouldBeTrue();
        outputs.Single(output => output.PupilId == Id(3)).Position.ShouldBe(3);
        outputs.Single(output => output.PupilId == Id(4)).Position.ShouldBeNull();
        outputs.Single(output => output.PupilId == Id(4)).CumulativeAverage.ShouldBe(95m);
    }

    [Fact]
    public void AFailedCoreSubject_ProposesRepeat_EvenAboveTheThreshold()
    {
        var pupil = Pupil(1, [Term(1, 60m, [new(Maths, 30), new(English, 90)]), Term(2, 60m, [new(Maths, 35), new(English, 85)])]);

        Single(Compute([pupil], Rules() with { RequireCorePass = true, CoreSubjectIds = [Maths] })).ProposedOutcome.ShouldBe(PromotionOutcome.Repeat);
        Single(Compute([pupil], Rules())).ProposedOutcome.ShouldBe(PromotionOutcome.Promoted);
        Single(Compute([pupil], Rules() with { PromotionThreshold = 61 })).ProposedOutcome.ShouldBe(PromotionOutcome.Repeat);
    }

    [Fact]
    public void APupilWithNoTerms_GetsNoRow()
    {
        var outputs = Compute([Pupil(1, []), AdaezeInput()], Rules());

        outputs.Select(output => output.PupilId).ShouldBe([Adaeze]);
    }

    [Fact]
    public void ACumulativeBetweenIntegerBands_StillFindsItsBand() =>
        Single(Compute([Pupil(1, [Term(1, 89.50m, []), Term(2, 89.49m, [])])], Rules())).CumulativeGrade.ShouldBe("B"); // 89.50 < 90

    private static AnnualPupilInput AdaezeInput() => new(
        Adaeze,
        [
            Term(1, 80.00m, [new(English, 86), new(Maths, 78)], total: 320),
            Term(2, 78.25m, [new(English, 82), new(Maths, 71)], total: 313),
            Term(3, 82.50m, [new(English, 88), new(Maths, 80)], total: 330),
        ]);

    private static AnnualTermInput Term(int ordinal, decimal average, AnnualSubjectTotal[] subjects, int total = 0) => new(ordinal, average, total, subjects);

    private static AnnualPupilInput Pupil(int id, AnnualTermInput[] terms) => new(Id(id), terms);

    private static Guid Id(int id) => Guid.Parse($"44444444-0000-7000-8000-{id:D12}");

    private static AnnualRules Rules() => new(AnnualMethod.SimpleAverage, null, null, null, 40, 40, false, []);

    private static IReadOnlyList<AnnualPupilOutput> Compute(AnnualPupilInput[] pupils, AnnualRules rules)
    {
        var result = AnnualComputation.Compute(new AnnualInput(pupils, Bands, rules));
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static AnnualPupilOutput Single(IReadOnlyList<AnnualPupilOutput> outputs) => outputs.ShouldHaveSingleItem();
}
