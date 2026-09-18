using SchoolManagement.Application.Results.Computation;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Results.Computation;

/// <summary>
/// Unit tests for <see cref="ResultComputationEngine"/> — spec 8.2's ordered algorithm and 8.3's
/// formulas, over plain inputs, no database. The §8.4 end-to-end fixture is TASK-0071 stage 3's own
/// integration test; these are the boundary and rule-specific cases the card names explicitly.
/// </summary>
public sealed class ResultComputationEngineTests
{
    private static readonly Guid Ca1 = Guid.Parse("00000000-0000-0000-0000-000000000c01");
    private static readonly Guid Ca2 = Guid.Parse("00000000-0000-0000-0000-000000000c02");
    private static readonly Guid ExamComponentId = Guid.Parse("00000000-0000-0000-0000-000000000c03");

    private static readonly IReadOnlyList<ComputationComponent> Components =
    [
        new(Ca1, IsExamination: false),
        new(Ca2, IsExamination: false),
        new(ExamComponentId, IsExamination: true),
    ];

    private static readonly IReadOnlyList<ComputationGradingBand> Bands = GradingScaleSeed.SeededBands
        .Select(band => new ComputationGradingBand(band.LowerBound, band.UpperBound, band.GradeLetter, band.Remark))
        .ToList();

    private static readonly Guid English = Guid.Parse("00000000-0000-0000-0000-0000000e0001");
    private static readonly Guid Maths = Guid.Parse("00000000-0000-0000-0000-0000000e0002");

    private static ComputationRules Rules(
        TieBreakRule tieBreak = TieBreakRule.SharedPosition, int passMark = 40, int minSubjects = 1) =>
        new(tieBreak, passMark, minSubjects);

    private static ComputationMarkRow Row(Guid pupilId, Guid subjectId, int? ca1, int? ca2, int? examMark, bool examAbsent = false) =>
        new(pupilId, subjectId, new Dictionary<Guid, int?> { [Ca1] = ca1, [Ca2] = ca2 }, examMark, examAbsent);

    private static ArmMarksInput SingleSubjectArm(
        Guid armId, IReadOnlyList<ComputationPupil> pupils, IReadOnlyList<ComputationMarkRow> marks, Guid? subjectId = null) =>
        new(armId, pupils, [new ComputationSubject(subjectId ?? Maths, "Mathematics")], marks);

    private static ComputationPupil Pupil(string surname) => new(Guid.CreateVersion7(), $"GRAS/{surname}", surname);

    private static ComputeResultSetInput BasicInput(
        ArmMarksInput arm, ComputationRules? rules = null, bool writeLevelPosition = false, IReadOnlyList<ArmMarksInput>? otherArms = null) =>
        new(arm, Components, Bands, rules ?? Rules(), writeLevelPosition, otherArms);

    // ---- §8.2 order / §8.3 formulas, happy path ------------------------------------------------

    [Fact]
    public void Compute_TwoSubjectsTwoPupils_ProducesCorrectTotalsGradesAndPositions()
    {
        var ada = Pupil("Okafor");
        var musa = Pupil("Ibrahim");
        var arm = new ArmMarksInput(
            Guid.CreateVersion7(),
            [ada, musa],
            [new ComputationSubject(English, "English Studies"), new ComputationSubject(Maths, "Mathematics")],
            [
                Row(ada.PupilId, English, 18, 16, 52),
                Row(ada.PupilId, Maths, 17, 16, 45),
                Row(musa.PupilId, English, 10, 10, 20),
                Row(musa.PupilId, Maths, 13, 11, null, examAbsent: true),
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsSuccess.ShouldBeTrue();
        var output = result.Value;

        var adaEnglish = output.SubjectLines.Single(line => line.PupilId == ada.PupilId && line.SubjectId == English);
        adaEnglish.CaTotal.ShouldBe(34);
        adaEnglish.SubjectTotal.ShouldBe(86);
        adaEnglish.Grade.ShouldBe("A");
        adaEnglish.IsPass.ShouldBeTrue();

        var adaMaths = output.SubjectLines.Single(line => line.PupilId == ada.PupilId && line.SubjectId == Maths);
        adaMaths.SubjectTotal.ShouldBe(78);
        adaMaths.Grade.ShouldBe("B");

        var musaMaths = output.SubjectLines.Single(line => line.PupilId == musa.PupilId && line.SubjectId == Maths);
        musaMaths.SubjectTotal.ShouldBe(24);
        musaMaths.Grade.ShouldBe("E");
        musaMaths.ExamMark.ShouldBeNull();

        var adaResult = output.PupilResults.Single(pupil => pupil.PupilId == ada.PupilId);
        adaResult.SubjectsTaken.ShouldBe(2);
        adaResult.TotalObtainable.ShouldBe(200);
        adaResult.TotalObtained.ShouldBe(164);
        adaResult.Average.ShouldBe(82.00m);
        adaResult.ArmPosition.ShouldBe(1);
        adaResult.ArmPositionTied.ShouldBeFalse();
    }

    // ---- §8.3 rounding, half up -----------------------------------------------------------------

    [Fact]
    public void ClassAverage_RoundsHalfUpToOneDecimalPlace()
    {
        // Three counted totals summing to 100: 100/3 = 33.333... which must round to 33.3, not 33.
        var p1 = Pupil("Adeyemi");
        var p2 = Pupil("Bello");
        var p3 = Pupil("Chukwu");
        var arm = SingleSubjectArm(
            Guid.CreateVersion7(), [p1, p2, p3],
            [
                Row(p1.PupilId, Maths, 20, 20, 0), // 40
                Row(p2.PupilId, Maths, 20, 20, 0), // 40
                Row(p3.PupilId, Maths, 20, 0, 0), // 20 -> sum 100
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsSuccess.ShouldBeTrue();
        result.Value.SubjectStatistics.Single().ClassAverage.ShouldBe(33.3m);
    }

    [Fact]
    public void PupilAverage_RoundsHalfUpToTwoDecimalPlaces()
    {
        // total_obtained 245 over 3 subjects = 81.6666... -> 81.67, half-up.
        var pupil = Pupil("Danladi");
        var arm = new ArmMarksInput(
            Guid.CreateVersion7(),
            [pupil],
            [new ComputationSubject(Guid.CreateVersion7(), "S1"), new ComputationSubject(Guid.CreateVersion7(), "S2"), new ComputationSubject(Guid.CreateVersion7(), "S3")],
            []);

        // Build marks against the three ad-hoc subjects above.
        var subjects = arm.Subjects;
        var marks = new List<ComputationMarkRow>
        {
            Row(pupil.PupilId, subjects[0].SubjectId, 20, 20, 40), // 80
            Row(pupil.PupilId, subjects[1].SubjectId, 20, 20, 45), // 85
            Row(pupil.PupilId, subjects[2].SubjectId, 20, 20, 40), // 80  -> sum 245
        };
        var withMarks = arm with { Marks = marks };

        var result = ResultComputationEngine.Compute(BasicInput(withMarks));

        result.IsSuccess.ShouldBeTrue();
        result.Value.PupilResults.Single().Average.ShouldBe(81.67m);
    }

    [Fact]
    public void Grade_ResolvedOnUnroundedSubjectTotal_OverallGradeOnRoundedAverage()
    {
        // subject_total is always an integer, so "unrounded" is trivially satisfied; the real
        // assertion is that OVERALL grade comes from the ROUNDED two-decimal average, not a raw
        // division truncated differently.
        var pupil = Pupil("Emeka");
        var arm = new ArmMarksInput(
            Guid.CreateVersion7(),
            [pupil],
            [new ComputationSubject(Guid.CreateVersion7(), "S1"), new ComputationSubject(Guid.CreateVersion7(), "S2"), new ComputationSubject(Guid.CreateVersion7(), "S3")],
            []);
        var subjects = arm.Subjects;
        // total 253 / 3 = 84.333... -> 84.33, which is band B (75-84), not band A (85-89) even
        // though the un-rounded value truncates to 84 either way — the point is the STORED average
        // (84.33) is what resolves the band, never a re-derivation from total_obtained/subjects_taken.
        var marks = new List<ComputationMarkRow>
        {
            Row(pupil.PupilId, subjects[0].SubjectId, 20, 20, 45), // 85
            Row(pupil.PupilId, subjects[1].SubjectId, 20, 20, 44), // 84
            Row(pupil.PupilId, subjects[2].SubjectId, 20, 20, 44), // 84 -> sum 253
        };
        var withMarks = arm with { Marks = marks };

        var result = ResultComputationEngine.Compute(BasicInput(withMarks));

        result.IsSuccess.ShouldBeTrue();
        var pupilResult = result.Value.PupilResults.Single();
        pupilResult.Average.ShouldBe(84.33m);
        pupilResult.OverallGrade.ShouldBe("B");
    }

    // ---- Tie-break rules -------------------------------------------------------------------------

    [Fact]
    public void TieBreak_SharedPosition_TiedPupilsShareAndNextPositionSkips()
    {
        var p1 = Pupil("Ada");
        var p2 = Pupil("Bola");
        var p3 = Pupil("Chidi");
        var arm = SingleSubjectArm(
            Guid.CreateVersion7(), [p1, p2, p3],
            [
                Row(p1.PupilId, Maths, 20, 20, 50), // 90
                Row(p2.PupilId, Maths, 20, 18, 40), // 78
                Row(p3.PupilId, Maths, 18, 20, 40), // 78 tie with p2
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm, Rules(TieBreakRule.SharedPosition)));

        result.IsSuccess.ShouldBeTrue();
        var lines = result.Value.SubjectLines.ToDictionary(line => line.PupilId);
        lines[p1.PupilId].SubjectPosition.ShouldBe(1);
        lines[p2.PupilId].SubjectPosition.ShouldBe(2);
        lines[p2.PupilId].SubjectPositionTied.ShouldBeTrue();
        lines[p3.PupilId].SubjectPosition.ShouldBe(2);
        lines[p3.PupilId].SubjectPositionTied.ShouldBeTrue();
    }

    [Fact]
    public void TieBreak_ExamThenCa_BreaksSubjectPositionOnThatSubjectsExamThenCa()
    {
        var p1 = Pupil("Adaeze");
        var p2 = Pupil("Chidi");
        // Same subject total (78), same exam-then-ca order as the §8.4.5 fixture:
        // Adaeze exam 45, Chidi exam 41 -> Adaeze ranks above Chidi under exam_then_ca.
        var arm = SingleSubjectArm(
            Guid.CreateVersion7(), [p1, p2],
            [
                Row(p1.PupilId, Maths, 17, 16, 45), // 78, exam 45
                Row(p2.PupilId, Maths, 20, 17, 41), // 78, exam 41
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm, Rules(TieBreakRule.ExamThenCa)));

        result.IsSuccess.ShouldBeTrue();
        var lines = result.Value.SubjectLines.ToDictionary(line => line.PupilId);
        lines[p1.PupilId].SubjectPosition.ShouldBe(1);
        lines[p1.PupilId].SubjectPositionTied.ShouldBeFalse();
        lines[p2.PupilId].SubjectPosition.ShouldBe(2);
    }

    [Fact]
    public void TieBreak_ExamThenAlphabetical_NeverSharesAPosition()
    {
        // Equal total, equal exam, equal CA -> only surname breaks it, and it must NOT share.
        var p1 = Pupil("Zulu");
        var p2 = Pupil("Adamu");
        var arm = SingleSubjectArm(
            Guid.CreateVersion7(), [p1, p2],
            [
                Row(p1.PupilId, Maths, 20, 20, 40),
                Row(p2.PupilId, Maths, 20, 20, 40),
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm, Rules(TieBreakRule.ExamThenAlphabetical)));

        result.IsSuccess.ShouldBeTrue();
        var lines = result.Value.SubjectLines.ToDictionary(line => line.PupilId);
        lines[p1.PupilId].SubjectPositionTied.ShouldBeFalse();
        lines[p2.PupilId].SubjectPositionTied.ShouldBeFalse();
        // Adamu sorts before Zulu ordinally, so Adamu is 1st.
        lines[p2.PupilId].SubjectPosition.ShouldBe(1);
        lines[p1.PupilId].SubjectPosition.ShouldBe(2);
    }

    [Fact]
    public void ArmPosition_TieBreaksOnSummedExamThenSummedCaAcrossSubjects()
    {
        var p1 = Pupil("First");
        var p2 = Pupil("Second");
        var subjectA = Guid.CreateVersion7();
        var subjectB = Guid.CreateVersion7();
        var arm = new ArmMarksInput(
            Guid.CreateVersion7(), [p1, p2],
            [new ComputationSubject(subjectA, "A"), new ComputationSubject(subjectB, "B")],
            [
                // p1: totals 80 + 80 = 160, exam sum 40+40=80, ca sum 40+40=80
                Row(p1.PupilId, subjectA, 20, 20, 40),
                Row(p1.PupilId, subjectB, 20, 20, 40),
                // p2: totals 90 + 70 = 160 (tied on total), exam sum 50+30=80 (still tied), ca sum 40+40=80 (still tied)
                Row(p2.PupilId, subjectA, 20, 20, 50),
                Row(p2.PupilId, subjectB, 20, 20, 30),
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm, Rules(TieBreakRule.ExamThenCa)));

        result.IsSuccess.ShouldBeTrue();
        // Every tie-break level equal -> shares under exam_then_ca (spec: "a tie surviving both is shared").
        var results = result.Value.PupilResults.ToDictionary(pupil => pupil.PupilId);
        results[p1.PupilId].ArmPositionTied.ShouldBeTrue();
        results[p2.PupilId].ArmPositionTied.ShouldBeTrue();
        results[p1.PupilId].ArmPosition.ShouldBe(1);
        results[p2.PupilId].ArmPosition.ShouldBe(1);
    }

    // ---- §8.5's two coverage gaps: band D and band F, both boundaries of F ----------------------

    [Theory]
    [InlineData(40, "D")]
    [InlineData(49, "D")]
    [InlineData(0, "F")]
    [InlineData(19, "F")]
    public void SubjectTotal_InBandDOrF_ResolvesTheCorrectGrade(int total, string expectedGrade)
    {
        var pupil = Pupil("Grace");
        // total = ca(0) + exam(total) so subject_total == total exactly, ca kept at 0/0.
        var arm = SingleSubjectArm(Guid.CreateVersion7(), [pupil], [Row(pupil.PupilId, Maths, 0, 0, total)]);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsSuccess.ShouldBeTrue();
        result.Value.SubjectLines.Single().Grade.ShouldBe(expectedGrade);
    }

    // ---- minSubjectsForPosition --------------------------------------------------------------

    [Fact]
    public void MinSubjectsForPosition_ExcludesAPupilFromRankingAndTheDenominator()
    {
        var full = Pupil("Full");
        var partial = Pupil("Partial");
        var subjectA = Guid.CreateVersion7();
        var subjectB = Guid.CreateVersion7();
        var arm = new ArmMarksInput(
            Guid.CreateVersion7(), [full, partial],
            [new ComputationSubject(subjectA, "A"), new ComputationSubject(subjectB, "B")],
            [
                Row(full.PupilId, subjectA, 20, 20, 40),
                Row(full.PupilId, subjectB, 20, 20, 40),
                Row(partial.PupilId, subjectA, 20, 20, 40), // only 1 complete subject
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm, Rules(minSubjects: 2)));

        result.IsSuccess.ShouldBeTrue();
        var results = result.Value.PupilResults.ToDictionary(pupil => pupil.PupilId);
        results[partial.PupilId].ArmPosition.ShouldBeNull();
        results[full.PupilId].ArmPosition.ShouldBe(1);
        results[full.PupilId].ArmPupilCount.ShouldBe(1);
    }

    // ---- is_pass -------------------------------------------------------------------------------

    [Theory]
    [InlineData(40, true)]
    [InlineData(39, false)]
    public void IsPass_FromPassMark(int total, bool expectedPass)
    {
        var pupil = Pupil("Halima");
        var arm = SingleSubjectArm(Guid.CreateVersion7(), [pupil], [Row(pupil.PupilId, Maths, 0, 0, total)]);

        var result = ResultComputationEngine.Compute(BasicInput(arm, Rules(passMark: 40)));

        result.IsSuccess.ShouldBeTrue();
        result.Value.SubjectLines.Single().IsPass.ShouldBe(expectedPass);
    }

    // ---- Grading band not found: stops everything, no partial output -------------------------

    [Fact]
    public void SubjectTotal_MatchingNoBand_FailsWithGradingBandNotFoundAndWritesNothing()
    {
        var pupil = Pupil("Ibrahim");
        var gapBands = new List<ComputationGradingBand> { new(0, 50, "P", "Pass") }; // 51-100 uncovered
        var arm = SingleSubjectArm(Guid.CreateVersion7(), [pupil], [Row(pupil.PupilId, Maths, 20, 20, 60)]); // total 100

        var result = ResultComputationEngine.Compute(
            new ComputeResultSetInput(arm, Components, gapBands, Rules(), WriteLevelPosition: false, OtherLevelArms: null));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ResultComputationEngine.GradingBandNotFoundCode);
        result.Error.Description.ShouldContain("100");
        result.Error.Description.ShouldContain("Mathematics");
    }

    // ---- §6.7.12 edge cases -------------------------------------------------------------------

    [Fact]
    public void NoActivePupils_Fails()
    {
        var arm = new ArmMarksInput(Guid.CreateVersion7(), [], [new ComputationSubject(Maths, "Mathematics")], []);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ResultComputationEngine.NoActivePupilsCode);
    }

    [Fact]
    public void NoSubjectsInEffect_Fails()
    {
        var arm = new ArmMarksInput(Guid.CreateVersion7(), [Pupil("Solo")], [], []);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ResultComputationEngine.NoSubjectsInEffectCode);
    }

    [Fact]
    public void NoCompleteScoreRows_Fails()
    {
        var pupil = Pupil("Blank");
        var arm = SingleSubjectArm(Guid.CreateVersion7(), [pupil], []); // no marks at all

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ResultComputationEngine.NoCompleteScoresCode);
    }

    [Fact]
    public void OneActivePupilInTheArm_RanksFirstOfOne_NoSpecialCasing()
    {
        var pupil = Pupil("Solo");
        var arm = SingleSubjectArm(Guid.CreateVersion7(), [pupil], [Row(pupil.PupilId, Maths, 20, 20, 40)]); // 80

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsSuccess.ShouldBeTrue();
        var stat = result.Value.SubjectStatistics.Single();
        stat.HighestScore.ShouldBe(80);
        stat.LowestScore.ShouldBe(80);
        stat.ClassAverage.ShouldBe(80.0m);
        var pupilResult = result.Value.PupilResults.Single();
        pupilResult.ArmPosition.ShouldBe(1);
        pupilResult.ArmPupilCount.ShouldBe(1);
    }

    // ---- Steps 5 and 6 as separate passes: an examination absentee -----------------------------

    [Fact]
    public void ExaminationAbsentee_ExcludedFromClassAverage_ButIncludedInRanking()
    {
        var top = Pupil("Top");
        var absentee = Pupil("Absentee");
        var arm = SingleSubjectArm(
            Guid.CreateVersion7(), [top, absentee],
            [
                Row(top.PupilId, Maths, 20, 20, 40), // 80
                Row(absentee.PupilId, Maths, 13, 11, null, examAbsent: true), // 24, ca-only
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsSuccess.ShouldBeTrue();
        var stat = result.Value.SubjectStatistics.Single();
        stat.CountedPupils.ShouldBe(1); // absentee excluded from the average population
        stat.RankedPupils.ShouldBe(2); // but included in the ranked population
        stat.ClassAverage.ShouldBe(80.0m);
        stat.LowestScore.ShouldBe(80); // NOT 24 — the absentee's low score must not drag this down

        var lines = result.Value.SubjectLines.ToDictionary(line => line.PupilId);
        lines[absentee.PupilId].SubjectPosition.ShouldBe(2); // still ranked, last of 2
    }

    // ---- Human ruling 2: a pupil with one missing and one incomplete row -----------------------

    [Fact]
    public void MissingAndIncompleteRows_WriteNoSubjectLine_AddZero_AndAreExcludedFromCounts()
    {
        var pupil = Pupil("Gaps");
        var missingSubject = Guid.CreateVersion7(); // no row entered at all
        var incompleteSubject = Guid.CreateVersion7(); // row entered, one CA cell blank
        var completeSubject = Guid.CreateVersion7();
        var arm = new ArmMarksInput(
            Guid.CreateVersion7(), [pupil],
            [
                new ComputationSubject(missingSubject, "Missing"),
                new ComputationSubject(incompleteSubject, "Incomplete"),
                new ComputationSubject(completeSubject, "Complete"),
            ],
            [
                Row(pupil.PupilId, incompleteSubject, 15, null, 40), // CA2 blank -> incomplete
                Row(pupil.PupilId, completeSubject, 20, 20, 40), // 80
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsSuccess.ShouldBeTrue();
        var output = result.Value;

        output.SubjectLines.ShouldNotContain(line => line.SubjectId == missingSubject);
        output.SubjectLines.ShouldNotContain(line => line.SubjectId == incompleteSubject);
        output.SubjectLines.Single().SubjectId.ShouldBe(completeSubject);

        var pupilResult = output.PupilResults.Single();
        pupilResult.SubjectsTaken.ShouldBe(3); // literal in-effect count, human ruling 2
        pupilResult.TotalObtained.ShouldBe(80); // missing/incomplete both add 0
        pupilResult.TotalObtainable.ShouldBe(300);

        // Both the missing and incomplete subject's statistic rows show zero counted/ranked pupils.
        var missingStat = output.SubjectStatistics.Single(stat => stat.SubjectId == missingSubject);
        missingStat.CountedPupils.ShouldBe(0);
        missingStat.RankedPupils.ShouldBe(0);
        var incompleteStat = output.SubjectStatistics.Single(stat => stat.SubjectId == incompleteSubject);
        incompleteStat.CountedPupils.ShouldBe(0);
        incompleteStat.RankedPupils.ShouldBe(0);
    }

    [Fact]
    public void MinSubjectsForPosition_CountsOnlyCompleteScoredSubjects_NotSubjectsTaken()
    {
        var pupil = Pupil("OneScored");
        var scoredSubject = Guid.CreateVersion7();
        var missingSubject = Guid.CreateVersion7();
        var arm = new ArmMarksInput(
            Guid.CreateVersion7(), [pupil],
            [new ComputationSubject(scoredSubject, "Scored"), new ComputationSubject(missingSubject, "Missing")],
            [Row(pupil.PupilId, scoredSubject, 20, 20, 40)]);

        var result = ResultComputationEngine.Compute(BasicInput(arm, Rules(minSubjects: 2)));

        result.IsSuccess.ShouldBeTrue();
        // subjects_taken is 2 (in-effect), but only 1 subject was actually SCORED, which is below the
        // minimum of 2 -> excluded from ranking even though subjects_taken alone would suggest otherwise.
        result.Value.PupilResults.Single().ArmPosition.ShouldBeNull();
    }

    // ---- Flags ------------------------------------------------------------------------------

    [Fact]
    public void Flag_NoExaminationSat_WhenEveryCompleteRowForASubjectIsExamAbsent()
    {
        var p1 = Pupil("A");
        var p2 = Pupil("B");
        var arm = SingleSubjectArm(
            Guid.CreateVersion7(), [p1, p2],
            [
                Row(p1.PupilId, Maths, 15, 15, null, examAbsent: true),
                Row(p2.PupilId, Maths, 10, 10, null, examAbsent: true),
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Flags.ShouldContain(flag => flag.Code == ComputationFlagCodes.NoExaminationSat && flag.SubjectId == Maths);
        var stat = result.Value.SubjectStatistics.Single();
        stat.CountedPupils.ShouldBe(0);
        stat.ClassAverage.ShouldBeNull();
        stat.HighestScore.ShouldBeNull();
    }

    [Fact]
    public void Flag_AbsentAllExaminations_WhenAPupilIsExamAbsentInEveryCompleteSubject()
    {
        var pupil = Pupil("Chronic");
        var subjectA = Guid.CreateVersion7();
        var subjectB = Guid.CreateVersion7();
        var arm = new ArmMarksInput(
            Guid.CreateVersion7(), [pupil],
            [new ComputationSubject(subjectA, "A"), new ComputationSubject(subjectB, "B")],
            [
                Row(pupil.PupilId, subjectA, 15, 15, null, examAbsent: true),
                Row(pupil.PupilId, subjectB, 10, 10, null, examAbsent: true),
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Flags.ShouldContain(flag => flag.Code == ComputationFlagCodes.AbsentAllExaminations && flag.PupilId == pupil.PupilId);
    }

    [Fact]
    public void Flag_AbsentAllExaminations_NotRaised_WhenOnlySomeSubjectsAreExamAbsent()
    {
        var pupil = Pupil("Musa");
        var subjectA = Guid.CreateVersion7();
        var subjectB = Guid.CreateVersion7();
        var arm = new ArmMarksInput(
            Guid.CreateVersion7(), [pupil],
            [new ComputationSubject(subjectA, "A"), new ComputationSubject(subjectB, "B")],
            [
                Row(pupil.PupilId, subjectA, 20, 20, 40),
                Row(pupil.PupilId, subjectB, 15, 15, null, examAbsent: true),
            ]);

        var result = ResultComputationEngine.Compute(BasicInput(arm));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Flags.ShouldNotContain(flag => flag.Code == ComputationFlagCodes.AbsentAllExaminations);
    }

    // ---- Human ruling 1: level position from every arm's live marks, written for this arm only --

    [Fact]
    public void LevelPosition_RanksAcrossEveryArmOfTheLevel_WrittenOnlyForThisArmsPupils()
    {
        var thisArmPupil = Pupil("ThisArm");
        var siblingPupilHigher = Pupil("SiblingHigher");
        var siblingPupilLower = Pupil("SiblingLower");

        var thisArm = SingleSubjectArm(
            Guid.CreateVersion7(), [thisArmPupil], [Row(thisArmPupil.PupilId, Maths, 20, 20, 40)]); // average 80

        var siblingArm = SingleSubjectArm(
            Guid.CreateVersion7(), [siblingPupilHigher, siblingPupilLower],
            [
                Row(siblingPupilHigher.PupilId, Maths, 20, 20, 50), // 90
                Row(siblingPupilLower.PupilId, Maths, 10, 10, 20), // 40
            ]);

        var result = ResultComputationEngine.Compute(
            BasicInput(thisArm, writeLevelPosition: true, otherArms: [siblingArm]));

        result.IsSuccess.ShouldBeTrue();
        var pupilResult = result.Value.PupilResults.Single();
        pupilResult.LevelPosition.ShouldBe(2); // behind the sibling's 90, ahead of the sibling's 40
        pupilResult.LevelPupilCount.ShouldBe(3);

        // Human ruling 1: never write or flag sibling sets — the output carries THIS arm's pupils only.
        result.Value.PupilResults.Count.ShouldBe(1);
        result.Value.PupilResults.ShouldNotContain(pupil => pupil.PupilId == siblingPupilHigher.PupilId);
    }

    [Fact]
    public void LevelPosition_NotComputed_WhenWriteLevelPositionIsFalse()
    {
        var pupil = Pupil("Solo");
        var arm = SingleSubjectArm(Guid.CreateVersion7(), [pupil], [Row(pupil.PupilId, Maths, 20, 20, 40)]);

        var result = ResultComputationEngine.Compute(BasicInput(arm, writeLevelPosition: false));

        result.IsSuccess.ShouldBeTrue();
        var pupilResult = result.Value.PupilResults.Single();
        pupilResult.LevelPosition.ShouldBeNull();
        pupilResult.LevelPupilCount.ShouldBeNull();
    }

    // ---- Idempotence: step 11's "running twice gives identical rows" is a persistence claim, but
    // the pure engine itself must be a deterministic function of its inputs for that to hold. -------

    [Fact]
    public void Compute_IsDeterministic_RunningTwiceOnTheSameInputGivesIdenticalOutput()
    {
        var pupil = Pupil("Repeat");
        var arm = SingleSubjectArm(Guid.CreateVersion7(), [pupil], [Row(pupil.PupilId, Maths, 20, 20, 40)]);
        var input = BasicInput(arm);

        var first = ResultComputationEngine.Compute(input);
        var second = ResultComputationEngine.Compute(input);

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        first.Value.PupilResults.Single().ShouldBe(second.Value.PupilResults.Single());
        first.Value.SubjectLines.Single().ShouldBe(second.Value.SubjectLines.Single());
    }
}
