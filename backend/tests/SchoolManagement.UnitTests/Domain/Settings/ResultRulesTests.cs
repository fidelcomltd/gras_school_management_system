using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="ResultRules"/>: the seeded defaults (spec 6.2.8) and the whole-row replace.</summary>
public sealed class ResultRulesTests
{
    [Fact]
    public void CreateSeed_ReturnsSpec628SDefaults()
    {
        var resultRules = ResultRules.CreateSeed(Guid.CreateVersion7());

        resultRules.AnnualMethod.ShouldBe(AnnualMethod.SimpleAverage);
        resultRules.WeightFirst.ShouldBeNull();
        resultRules.WeightSecond.ShouldBeNull();
        resultRules.WeightThird.ShouldBeNull();
        resultRules.PrimaryPositionScope.ShouldBe(PrimaryPositionScope.Arm);
        resultRules.ShowLevelPosition.ShouldBeTrue();
        resultRules.TieBreakRule.ShouldBe(TieBreakRule.SharedPosition);
        resultRules.PassMark.ShouldBe(40);
        resultRules.PromotionThreshold.ShouldBe(40);
        resultRules.RequireCorePass.ShouldBeTrue();
        resultRules.CoreSubjectIds.ShouldBeEmpty(); // Never guessed from a subject name — see the entity's remarks.
        resultRules.MinSubjectsForPosition.ShouldBe(1);
    }

    [Fact]
    public void Update_ReplacesEveryField()
    {
        var resultRules = ResultRules.CreateSeed(Guid.CreateVersion7());
        var coreSubjectIds = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() };

        resultRules.Update(
            AnnualMethod.Weighted,
            weightFirst: 30,
            weightSecond: 30,
            weightThird: 40,
            PrimaryPositionScope.Level,
            showLevelPosition: false,
            TieBreakRule.ExamThenAlphabetical,
            passMark: 45,
            promotionThreshold: 50,
            requireCorePass: true,
            coreSubjectIds,
            minSubjectsForPosition: 4);

        resultRules.AnnualMethod.ShouldBe(AnnualMethod.Weighted);
        resultRules.WeightFirst.ShouldBe(30);
        resultRules.WeightSecond.ShouldBe(30);
        resultRules.WeightThird.ShouldBe(40);
        resultRules.PrimaryPositionScope.ShouldBe(PrimaryPositionScope.Level);
        resultRules.ShowLevelPosition.ShouldBeFalse();
        resultRules.TieBreakRule.ShouldBe(TieBreakRule.ExamThenAlphabetical);
        resultRules.PassMark.ShouldBe(45);
        resultRules.PromotionThreshold.ShouldBe(50);
        resultRules.RequireCorePass.ShouldBeTrue();
        resultRules.CoreSubjectIds.ShouldBe(coreSubjectIds);
        resultRules.MinSubjectsForPosition.ShouldBe(4);
    }

    [Fact]
    public void Update_CalledTwice_ReplacesCoreSubjectIdsRatherThanAccumulatingThem()
    {
        var resultRules = ResultRules.CreateSeed(Guid.CreateVersion7());
        var firstSubjectId = Guid.CreateVersion7();
        var secondSubjectId = Guid.CreateVersion7();

        resultRules.Update(
            AnnualMethod.SimpleAverage, null, null, null,
            PrimaryPositionScope.Arm, true, TieBreakRule.SharedPosition,
            40, 40, true, [firstSubjectId], 1);

        resultRules.Update(
            AnnualMethod.SimpleAverage, null, null, null,
            PrimaryPositionScope.Arm, true, TieBreakRule.SharedPosition,
            40, 40, true, [secondSubjectId], 1);

        resultRules.CoreSubjectIds.ShouldBe([secondSubjectId]);
    }
}
