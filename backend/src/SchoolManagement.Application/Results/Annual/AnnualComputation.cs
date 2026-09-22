using SchoolManagement.Application.Results.Computation;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results.Annual;

/// <summary>One subject's total in one term.</summary>
/// <param name="SubjectId">The subject.</param>
/// <param name="Total">The stored <c>subject_total</c>.</param>
public sealed record AnnualSubjectTotal(Guid SubjectId, int Total);

/// <summary>One published term result for a pupil. Term results travel with the pupil across arm moves (6.7.10).</summary>
/// <param name="Ordinal">1, 2 or 3.</param>
/// <param name="Average">The stored term average.</param>
/// <param name="TotalObtained">The stored term total.</param>
/// <param name="Subjects">The term's subject totals.</param>
public sealed record AnnualTermInput(int Ordinal, decimal Average, int TotalObtained, IReadOnlyList<AnnualSubjectTotal> Subjects);

/// <summary>A pupil of the final arm, with every published term result they have in the session.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Terms">Zero to three terms.</param>
public sealed record AnnualPupilInput(Guid PupilId, IReadOnlyList<AnnualTermInput> Terms);

/// <summary>The result rules the annual computation reads (spec 6.2.8).</summary>
public sealed record AnnualRules(
    AnnualMethod Method,
    int? WeightFirst,
    int? WeightSecond,
    int? WeightThird,
    int PassMark,
    int PromotionThreshold,
    bool RequireCorePass,
    IReadOnlyList<Guid> CoreSubjectIds);

/// <summary>Everything one arm's annual computation needs.</summary>
/// <param name="Pupils">The pupils of the arm they ended the session in.</param>
/// <param name="Bands">The grading bands frozen on the Third Term result set's snapshot.</param>
/// <param name="Rules">The result rules.</param>
public sealed record AnnualInput(IReadOnlyList<AnnualPupilInput> Pupils, IReadOnlyList<ComputationGradingBand> Bands, AnnualRules Rules);

/// <summary>A pupil's annual result, ready to store.</summary>
public sealed record AnnualPupilOutput(
    Guid PupilId,
    int TermsCounted,
    IReadOnlyList<decimal?> TermAverages,
    IReadOnlyList<int?> TermTotals,
    int GrandTotal,
    decimal CumulativeAverage,
    string CumulativeGrade,
    string CumulativeRemark,
    int? Position,
    bool PositionTied,
    IReadOnlyList<AnnualSubjectResult> Subjects,
    PromotionOutcome ProposedOutcome);

/// <summary>
/// Spec 6.7.10's annual computation for one arm. Pure: the handler loads, this computes, the handler stores. Rounding is
/// half up to two places (8.3) and grades use the term engine's continuous band reading, so a cumulative 84.33 finds a band.
/// </summary>
public static class AnnualComputation
{
    /// <summary>Terms in a session.</summary>
    public const int TermsInSession = 3;

    /// <summary>Returned when a cumulative average falls outside every band.</summary>
    public const string GradingBandNotFoundCode = "annual_result.grading_band_not_found";

    /// <summary>Computes every pupil with at least one term. A pupil with none gets no row (the promotion screen flags them).</summary>
    public static Result<IReadOnlyList<AnnualPupilOutput>> Compute(AnnualInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var rules = input.Rules;
        var computed = new List<(AnnualPupilInput Pupil, decimal Average, ComputationGradingBand Band, List<AnnualSubjectResult> Subjects)>();

        foreach (var pupil in input.Pupils.Where(pupil => pupil.Terms.Count > 0))
        {
            var average = CumulativeAverage(pupil.Terms, rules);
            if (FindBand(input.Bands, average) is not { } band)
            {
                return Result.Failure<IReadOnlyList<AnnualPupilOutput>>(Error.Validation(
                    GradingBandNotFoundCode, $"A cumulative average of {average:0.00} falls outside every grading band. Check the grading scale on the Third Term result."));
            }

            var subjects = pupil.Terms
                .SelectMany(term => term.Subjects.Select(subject => (term.Ordinal, subject.SubjectId, subject.Total)))
                .GroupBy(entry => entry.SubjectId)
                .Select(group =>
                {
                    var mean = RoundHalfUp((decimal)group.Sum(entry => entry.Total) / group.Count());
                    var totals = Enumerable.Range(1, TermsInSession)
                        .Select(ordinal => group.Where(entry => entry.Ordinal == ordinal).Select(entry => (int?)entry.Total).FirstOrDefault())
                        .ToList();
                    return new AnnualSubjectResult(group.Key, totals, mean, FindBand(input.Bands, mean)?.GradeLetter, group.Count());
                })
                .ToList();
            computed.Add((pupil, average, band, subjects));
        }

        // Ranked on the cumulative average; one term is not an annual result, so a Third-Term joiner is not ranked (6.7.10).
        var ranks = CompetitionRanker.Rank(
                computed.Select(entry => new RankItem<Guid>(entry.Pupil.PupilId, entry.Average, 0, 0, string.Empty, entry.Pupil.Terms.Count > 1)).ToList(),
                TieBreakRule.SharedPosition)
            .ToDictionary(rank => rank.Id);

        return Result.Success<IReadOnlyList<AnnualPupilOutput>>(computed
            .Select(entry =>
            {
                var terms = entry.Pupil.Terms;
                var rank = ranks[entry.Pupil.PupilId];
                return new AnnualPupilOutput(
                    entry.Pupil.PupilId,
                    terms.Count,
                    Enumerable.Range(1, TermsInSession).Select(ordinal => terms.Where(term => term.Ordinal == ordinal).Select(term => (decimal?)term.Average).FirstOrDefault()).ToList(),
                    Enumerable.Range(1, TermsInSession).Select(ordinal => terms.Where(term => term.Ordinal == ordinal).Select(term => (int?)term.TotalObtained).FirstOrDefault()).ToList(),
                    terms.Sum(term => term.TotalObtained),
                    entry.Average,
                    entry.Band.GradeLetter,
                    entry.Band.Remark,
                    rank.Position,
                    rank.Tied,
                    entry.Subjects,
                    Propose(entry.Average, entry.Subjects, rules));
            })
            .ToList());
    }

    /// <summary>
    /// Spec 8.3: simple is the mean of the term averages sat; weighted divides by the weights of the terms sat, which is
    /// the same as rescaling them to 100 (6.7.10, first row of the edge cases).
    /// </summary>
    public static decimal CumulativeAverage(IReadOnlyList<AnnualTermInput> terms, AnnualRules rules)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Method != AnnualMethod.Weighted)
        {
            return RoundHalfUp(terms.Sum(term => term.Average) / terms.Count);
        }

        var weights = terms.Select(term => (decimal)(term.Ordinal switch
        {
            1 => rules.WeightFirst,
            2 => rules.WeightSecond,
            _ => rules.WeightThird,
        } ?? 0)).ToList();
        var totalWeight = weights.Sum();
        return totalWeight == 0
            ? RoundHalfUp(terms.Sum(term => term.Average) / terms.Count)
            : RoundHalfUp(terms.Select((term, index) => term.Average * weights[index]).Sum() / totalWeight);
    }

    /// <summary>
    /// Spec 6.3.7: at or above the promotion threshold, and when required a pass in every core subject. A core subject the
    /// pupil never took does not count against them; the school removes it from the core list or decides by hand.
    /// Promoted on trial is never proposed.
    /// </summary>
    private static PromotionOutcome Propose(decimal average, IReadOnlyList<AnnualSubjectResult> subjects, AnnualRules rules)
    {
        if (average < rules.PromotionThreshold)
        {
            return PromotionOutcome.Repeat;
        }

        var failedCore = rules.RequireCorePass && subjects.Any(subject => rules.CoreSubjectIds.Contains(subject.SubjectId) && subject.Mean < rules.PassMark);
        return failedCore ? PromotionOutcome.Repeat : PromotionOutcome.Promoted;
    }

    // Same reading as ResultComputationEngine.FindBand: integer bounds, continuous values, upper edge exclusive at +1.
    private static ComputationGradingBand? FindBand(IReadOnlyList<ComputationGradingBand> bands, decimal value) =>
        bands.FirstOrDefault(band => band.LowerBound <= value && value < band.UpperBound + 1);

    private static decimal RoundHalfUp(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
