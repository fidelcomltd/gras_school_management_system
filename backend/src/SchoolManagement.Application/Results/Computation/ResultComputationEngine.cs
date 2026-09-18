using System.Globalization;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results.Computation;

/// <summary>
/// The pure calculator behind <c>POST /result-sets/{resultSetId}/compute</c> (spec 8.2's ordered
/// algorithm, 8.3's formulas). Takes plain inputs, returns plain outputs — no database, no I/O. The
/// handler (TASK-0071 stage 2) loads these inputs, calls <see cref="Compute"/>, and persists the
/// result inside one transaction (spec 8.2 step 11).
/// </summary>
public static class ResultComputationEngine
{
    /// <summary>Spec 6.7.12: "An arm with zero active pupils at computation time."</summary>
    public const string NoActivePupilsCode = "result_set.no_active_pupils";

    /// <summary>Spec 8.2 step 1: the subject set is empty.</summary>
    public const string NoSubjectsInEffectCode = "result_set.no_subjects_in_effect";

    /// <summary>Spec 6.7.12: "Computation run on a set with no complete score rows."</summary>
    public const string NoCompleteScoresCode = "result_set.no_complete_scores";

    /// <summary>Spec 6.7.6: a total (subject or rounded average) matches no band.</summary>
    public const string GradingBandNotFoundCode = "result_set.grading_band_not_found";

    /// <summary>Runs the whole algorithm (spec 8.2 steps 1-10; step 11's persistence is the caller's job).</summary>
    public static Result<ComputeResultSetOutput> Compute(ComputeResultSetInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var arm = input.ThisArm;

        // Step 1: resolve the pupil set and the subject set.
        if (arm.Pupils.Count == 0)
        {
            return Result.Failure<ComputeResultSetOutput>(Error.Validation(
                NoActivePupilsCode, "This class has no active pupils. Nothing to compute."));
        }

        if (arm.Subjects.Count == 0)
        {
            return Result.Failure<ComputeResultSetOutput>(Error.Validation(
                NoSubjectsInEffectCode, "This class has no subjects in effect this term. Nothing to compute."));
        }

        // Steps 2-4: ca_total, subject_total, grade — for every COMPLETE row only (human ruling 2: a
        // missing or incomplete row writes no subject line at all).
        var subjectTotals = ComputeSubjectTotals(arm, input.Components);

        if (subjectTotals.Count == 0)
        {
            return Result.Failure<ComputeResultSetOutput>(Error.Validation(
                NoCompleteScoresCode, "No complete marks have been entered for this class. Enter marks before computing."));
        }

        var pupilsById = arm.Pupils.ToDictionary(pupil => pupil.PupilId);
        var subjectsById = arm.Subjects.ToDictionary(subject => subject.SubjectId);
        var surnameByPupil = arm.Pupils.ToDictionary(pupil => pupil.PupilId, pupil => pupil.Surname);

        var enriched = new List<(SubjectTotalRow Row, ComputationGradingBand Band)>(subjectTotals.Count);
        foreach (var row in subjectTotals)
        {
            var band = FindBand(input.GradingBands, row.SubjectTotal);
            if (band is null)
            {
                return Result.Failure<ComputeResultSetOutput>(Error.Validation(
                    GradingBandNotFoundCode,
                    BandNotFoundMessage(row.SubjectTotal, subjectsById[row.SubjectId].SubjectName, pupilsById[row.PupilId].RegistrationNumber)));
            }

            enriched.Add((row, band));
        }

        // Steps 5-6, kept as SEPARATE passes over SEPARATE populations on purpose (spec 8.2's own
        // warning: collapsing them is "the mistake that produces a class average including an
        // absentee"). Step 6 (ranking, all complete rows including absentees) below; step 5 (stats,
        // counted rows only) inside the same per-subject loop but reading a differently-filtered list.
        var subjectLines = new List<SubjectResultLineOutput>();
        var subjectStatistics = new List<SubjectArmStatisticOutput>();
        var flags = new List<ComputationFlag>();

        foreach (var subject in arm.Subjects)
        {
            var rowsForSubject = enriched.Where(entry => entry.Row.SubjectId == subject.SubjectId).ToList();

            // Step 6: rank ALL pupils with a complete row, including absentees.
            var rankItems = rowsForSubject
                .Select(entry => new RankItem<Guid>(
                    entry.Row.PupilId,
                    entry.Row.SubjectTotal,
                    entry.Row.ExamMark ?? 0,
                    entry.Row.CaTotal,
                    surnameByPupil[entry.Row.PupilId],
                    Eligible: true))
                .ToList();
            var ranksBySubject = CompetitionRanker.Rank(rankItems, input.Rules.TieBreakRule)
                .ToDictionary(rank => rank.Id);

            foreach (var (row, band) in rowsForSubject)
            {
                var rank = ranksBySubject[row.PupilId];
                subjectLines.Add(new SubjectResultLineOutput(
                    row.PupilId,
                    row.SubjectId,
                    row.CaTotal,
                    row.ExamMark,
                    row.SubjectTotal,
                    band.GradeLetter,
                    band.Remark,
                    rank.Position,
                    rank.Tied,
                    row.SubjectTotal >= input.Rules.PassMark));
            }

            // Step 5: highest/lowest/class average over COUNTED pupils only — exam absentees excluded.
            var countedRows = rowsForSubject.Where(entry => !entry.Row.ExamAbsent).ToList();

            int? highest = countedRows.Count > 0 ? countedRows.Max(entry => entry.Row.SubjectTotal) : null;
            int? lowest = countedRows.Count > 0 ? countedRows.Min(entry => entry.Row.SubjectTotal) : null;
            decimal? classAverage = countedRows.Count > 0
                ? RoundHalfUp((decimal)countedRows.Sum(entry => entry.Row.SubjectTotal) / countedRows.Count, 1)
                : null;

            subjectStatistics.Add(new SubjectArmStatisticOutput(
                subject.SubjectId, highest, lowest, classAverage, countedRows.Count, rowsForSubject.Count));

            if (countedRows.Count == 0 && rowsForSubject.Count > 0)
            {
                flags.Add(new ComputationFlag(ComputationFlagCodes.NoExaminationSat, subject.SubjectId, null));
            }
        }

        // absent_all_examinations: a pupil with at least one complete subject, all of them exam-absent.
        var linesByPupil = subjectLines.GroupBy(line => line.PupilId).ToDictionary(group => group.Key, group => group.ToList());
        foreach (var pupil in arm.Pupils)
        {
            if (linesByPupil.TryGetValue(pupil.PupilId, out var lines) && lines.Count > 0 && lines.All(line => line.ExamMark is null))
            {
                flags.Add(new ComputationFlag(ComputationFlagCodes.AbsentAllExaminations, null, pupil.PupilId));
            }
        }

        // Steps 7-8: total_obtained, total_obtainable, average, overall_grade.
        var pupilAggregates = AggregatePupils(arm, subjectTotals);
        var aggregateByPupil = pupilAggregates.ToDictionary(aggregate => aggregate.PupilId);

        var overallGradeByPupil = new Dictionary<Guid, ComputationGradingBand>();
        foreach (var aggregate in pupilAggregates)
        {
            var band = FindBand(input.GradingBands, aggregate.Average);
            if (band is null)
            {
                return Result.Failure<ComputeResultSetOutput>(Error.Validation(
                    GradingBandNotFoundCode,
                    BandNotFoundMessage(aggregate.Average, subjectName: null, pupilsById[aggregate.PupilId].RegistrationNumber)));
            }

            overallGradeByPupil[aggregate.PupilId] = band;
        }

        // Step 9: arm_position.
        var armRankItems = pupilAggregates
            .Select(aggregate => new RankItem<Guid>(
                aggregate.PupilId, aggregate.TotalObtained, aggregate.ExamSum, aggregate.CaSum, aggregate.Surname,
                Eligible: aggregate.ScoredSubjects >= input.Rules.MinSubjectsForPosition))
            .ToList();
        var armRanks = CompetitionRanker.Rank(armRankItems, input.Rules.TieBreakRule).ToDictionary(rank => rank.Id);
        var armPupilCount = armRankItems.Count(item => item.Eligible);

        // Step 10: level_position (human ruling 1 — computed in memory from every arm's live marks;
        // never reads or writes a sibling's stored rows; written only for ThisArm's pupils).
        Dictionary<Guid, RankResult<Guid>>? levelRanks = null;
        var levelPupilCount = 0;
        if (input.WriteLevelPosition)
        {
            var levelRankItems = new List<RankItem<Guid>>();
            AppendLevelRankItems(levelRankItems, aggregatesForThisArm: pupilAggregates, rules: input.Rules);

            if (input.OtherLevelArms is { Count: > 0 })
            {
                foreach (var siblingArm in input.OtherLevelArms)
                {
                    if (siblingArm.Pupils.Count == 0 || siblingArm.Subjects.Count == 0)
                    {
                        continue;
                    }

                    var siblingTotals = ComputeSubjectTotals(siblingArm, input.Components);
                    var siblingAggregates = AggregatePupils(siblingArm, siblingTotals);
                    AppendLevelRankItems(levelRankItems, siblingAggregates, input.Rules);
                }
            }

            levelRanks = CompetitionRanker.Rank(levelRankItems, input.Rules.TieBreakRule).ToDictionary(rank => rank.Id);
            levelPupilCount = levelRankItems.Count(item => item.Eligible);
        }

        var pupilResults = new List<PupilTermResultOutput>(arm.Pupils.Count);
        foreach (var pupil in arm.Pupils)
        {
            var aggregate = aggregateByPupil[pupil.PupilId];
            var overallBand = overallGradeByPupil[pupil.PupilId];
            var armRank = armRanks[pupil.PupilId];

            int? levelPosition = null;
            var levelTied = false;
            int? levelCount = null;
            if (input.WriteLevelPosition && levelRanks is not null)
            {
                var levelRank = levelRanks[pupil.PupilId];
                levelPosition = levelRank.Position;
                levelTied = levelRank.Tied;
                levelCount = levelPupilCount;
            }

            pupilResults.Add(new PupilTermResultOutput(
                pupil.PupilId,
                aggregate.SubjectsTaken,
                aggregate.SubjectsTaken * 100,
                aggregate.TotalObtained,
                aggregate.Average,
                overallBand.GradeLetter,
                armRank.Position,
                armRank.Tied,
                armPupilCount,
                levelPosition,
                levelTied,
                levelCount));
        }

        return Result.Success(new ComputeResultSetOutput(subjectLines, subjectStatistics, pupilResults, flags));
    }

    private static void AppendLevelRankItems(
        List<RankItem<Guid>> items, IReadOnlyList<PupilAggregateRow> aggregatesForThisArm, ComputationRules rules)
    {
        foreach (var aggregate in aggregatesForThisArm)
        {
            items.Add(new RankItem<Guid>(
                aggregate.PupilId, aggregate.Average, aggregate.ExamSum, aggregate.CaSum, aggregate.Surname,
                Eligible: aggregate.ScoredSubjects >= rules.MinSubjectsForPosition));
        }
    }

    /// <summary>One pupil's complete (non-voided, fully-filled) subject_total for one subject (spec 8.2 steps 2-4).</summary>
    private sealed record SubjectTotalRow(Guid PupilId, Guid SubjectId, int CaTotal, int? ExamMark, bool ExamAbsent, int SubjectTotal);

    /// <summary>One pupil's aggregate across every subject in effect for an arm (spec 8.2 steps 7-8).</summary>
    private sealed record PupilAggregateRow(
        Guid PupilId, string Surname, int TotalObtained, int SubjectsTaken, int ScoredSubjects, decimal Average, int ExamSum, int CaSum);

    /// <summary>
    /// Spec 8.2 steps 2-4 for one arm: ca_total, subject_total, per row — but ONLY for a row that is
    /// present AND complete (human ruling 2). A missing pupil/subject pair, or a present row with any
    /// blank CA cell or neither an exam mark nor exam_absent, is silently absent from the result.
    /// </summary>
    private static List<SubjectTotalRow> ComputeSubjectTotals(ArmMarksInput arm, IReadOnlyList<ComputationComponent> components)
    {
        var nonExamComponentIds = components.Where(component => !component.IsExamination)
            .Select(component => component.ComponentId)
            .ToHashSet();

        var marksByPupilSubject = arm.Marks.ToDictionary(mark => (mark.PupilId, mark.SubjectId));

        var rows = new List<SubjectTotalRow>();

        foreach (var subject in arm.Subjects)
        {
            foreach (var pupil in arm.Pupils)
            {
                if (!marksByPupilSubject.TryGetValue((pupil.PupilId, subject.SubjectId), out var mark))
                {
                    continue; // Missing — no row entered at all.
                }

                if (!IsComplete(mark, nonExamComponentIds))
                {
                    continue; // Incomplete — some CA cell blank, or neither an exam mark nor absent.
                }

                var caTotal = SumCa(mark, nonExamComponentIds);
                var subjectTotal = mark.ExamAbsent ? caTotal : caTotal + (mark.ExamMark ?? 0);

                rows.Add(new SubjectTotalRow(
                    pupil.PupilId, subject.SubjectId, caTotal, mark.ExamAbsent ? null : mark.ExamMark, mark.ExamAbsent, subjectTotal));
            }
        }

        return rows;
    }

    /// <summary>
    /// Spec 8.2 steps 7-8 for one arm: total_obtained (0 for a missing/incomplete subject, human
    /// ruling 2), subjects_taken (the literal in-effect count), average. Also carries the exam/CA sums
    /// <see cref="CompetitionRanker"/> needs for <see cref="Domain.Settings.TieBreakRule.ExamThenCa"/>/
    /// <see cref="Domain.Settings.TieBreakRule.ExamThenAlphabetical"/>, and the count of COMPLETE
    /// ("scored") subjects <c>min_subjects_for_position</c> reads.
    /// </summary>
    private static List<PupilAggregateRow> AggregatePupils(ArmMarksInput arm, IReadOnlyList<SubjectTotalRow> subjectTotals)
    {
        var bySubjectByPupil = subjectTotals.GroupBy(row => row.PupilId).ToDictionary(group => group.Key, group => group.ToList());
        var subjectsTaken = arm.Subjects.Count;

        var results = new List<PupilAggregateRow>(arm.Pupils.Count);
        foreach (var pupil in arm.Pupils)
        {
            var rows = bySubjectByPupil.TryGetValue(pupil.PupilId, out var list) ? list : [];
            var totalObtained = rows.Sum(row => row.SubjectTotal);
            var average = subjectsTaken == 0 ? 0m : RoundHalfUp((decimal)totalObtained / subjectsTaken, 2);
            var examSum = rows.Sum(row => row.ExamMark ?? 0);
            var caSum = rows.Sum(row => row.CaTotal);

            results.Add(new PupilAggregateRow(pupil.PupilId, pupil.Surname, totalObtained, subjectsTaken, rows.Count, average, examSum, caSum));
        }

        return results;
    }

    private static bool IsComplete(ComputationMarkRow mark, HashSet<Guid> nonExamComponentIds)
    {
        foreach (var componentId in nonExamComponentIds)
        {
            if (!mark.ComponentMarks.TryGetValue(componentId, out var value) || value is null)
            {
                return false;
            }
        }

        return mark.ExamAbsent || mark.ExamMark.HasValue;
    }

    private static int SumCa(ComputationMarkRow mark, HashSet<Guid> nonExamComponentIds)
    {
        var sum = 0;
        foreach (var componentId in nonExamComponentIds)
        {
            sum += mark.ComponentMarks.TryGetValue(componentId, out var value) ? value ?? 0 : 0;
        }

        return sum;
    }

    /// <summary>
    /// Resolves the band containing <paramref name="value"/>. <see cref="ComputationGradingBand.UpperBound"/>
    /// is an INTEGER (spec 6.2.5), but <paramref name="value"/> is a fractional average up to two
    /// decimal places (spec 8.3) — a strict <c>value &lt;= UpperBound</c> would leave real gaps between
    /// adjacent bands (84.33 matches neither B [75,84] nor A [85,89]) even though
    /// <c>GradingScaleRules</c> validates the WHOLE-NUMBER scale as gapless and contiguous
    /// (<c>left.UpperBound + 1 == right.LowerBound</c>). Extending the upper edge to
    /// <c>UpperBound + 1</c> (exclusive) is the continuous-real reading of that same contiguity rule,
    /// and is exact for an integer <paramref name="value"/> (a <c>subject_total</c>) since
    /// <c>value &lt;= UpperBound</c> and <c>value &lt; UpperBound + 1</c> agree on every whole number.
    /// This is this card's own judgement call — the spec never states it explicitly because every
    /// worked figure in §8.4 lands on a band boundary with no fractional ambiguity.
    /// </summary>
    private static ComputationGradingBand? FindBand(IReadOnlyList<ComputationGradingBand> bands, decimal value) =>
        bands.FirstOrDefault(band => band.LowerBound <= value && value < band.UpperBound + 1);

    private static decimal RoundHalfUp(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static string BandNotFoundMessage(decimal total, string? subjectName, string? registrationNumber)
    {
        var pupilLabel = registrationNumber ?? "this pupil";
        var totalText = total.ToString(CultureInfo.InvariantCulture);

        return subjectName is null
            ? $"Computation stopped. Average {totalText} for {pupilLabel} matches no grading band. Check the grading scale."
            : $"Computation stopped. Mark {totalText} in {subjectName} for {pupilLabel} matches no grading band. Check the grading scale.";
    }
}
