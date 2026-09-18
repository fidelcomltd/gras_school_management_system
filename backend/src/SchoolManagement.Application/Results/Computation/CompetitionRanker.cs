using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results.Computation;

/// <summary>One item to rank (spec 6.7.6's tie-breaking table).</summary>
/// <param name="Id">The item's identity (a pupil id, in every caller today).</param>
/// <param name="PrimarySort">Descending sort key — <c>subject_total</c>, <c>total_obtained</c> or <c>average</c> depending on the caller.</param>
/// <param name="TieExam">
/// Tie-break value for <see cref="TieBreakRule.ExamThenCa"/>/<see cref="TieBreakRule.ExamThenAlphabetical"/>
/// — an absentee's exam counts 0 (spec/AC: "An absentee's exam counts 0 for tie-break").
/// </param>
/// <param name="TieCa">Second tie-break value for the same two rules.</param>
/// <param name="Surname">Final tie-break for <see cref="TieBreakRule.ExamThenAlphabetical"/>, ascending.</param>
/// <param name="Eligible">
/// False excludes the item from ranking entirely (spec 6.7.6: <c>min_subjects_for_position</c>) — it
/// receives a null position and is not counted in the denominator. Subject-level ranking (spec 8.2
/// step 6) never excludes on this basis; only arm/level position does.
/// </param>
public sealed record RankItem<TId>(TId Id, decimal PrimarySort, int TieExam, int TieCa, string Surname, bool Eligible)
    where TId : notnull;

/// <summary>One item's ranking outcome.</summary>
/// <param name="Id">Matches the input <see cref="RankItem{TId}.Id"/>.</param>
/// <param name="Position">Null when the item was ineligible.</param>
/// <param name="Tied">True when at least one other item shares <see cref="Position"/>.</param>
public sealed record RankResult<TId>(TId Id, int? Position, bool Tied)
    where TId : notnull;

/// <summary>
/// Spec 6.7.6's competition ("1224") ranking: tied items share a position and the next position skips
/// by the number tied. The three <see cref="TieBreakRule"/> values differ only in what breaks a tie on
/// <see cref="RankItem{TId}.PrimarySort"/> — <see cref="TieBreakRule.SharedPosition"/> never looks
/// past it, so two equal primary sorts always share; <see cref="TieBreakRule.ExamThenCa"/> and
/// <see cref="TieBreakRule.ExamThenAlphabetical"/> look further, and the latter "never shares a
/// position" (spec 6.7.6) because surname is (in practice) unique enough to always resolve.
/// </summary>
public static class CompetitionRanker
{
    /// <summary>Ranks <paramref name="items"/> under <paramref name="rule"/>. Order of the result is unspecified; look up by <c>Id</c>.</summary>
    public static IReadOnlyList<RankResult<TId>> Rank<TId>(IReadOnlyList<RankItem<TId>> items, TieBreakRule rule)
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(items);

        var comparer = BuildComparer<TId>(rule);

        var eligible = items.Where(item => item.Eligible)
            .OrderBy(item => item, Comparer<RankItem<TId>>.Create(comparer))
            .ToList();

        var results = new List<RankResult<TId>>(items.Count);

        for (var index = 0; index < eligible.Count; index++)
        {
            if (index > 0 && comparer(eligible[index - 1], eligible[index]) == 0)
            {
                var previous = results[index - 1];
                results.Add(new RankResult<TId>(eligible[index].Id, previous.Position, true));
                results[index - 1] = previous with { Tied = true };
            }
            else
            {
                results.Add(new RankResult<TId>(eligible[index].Id, index + 1, false));
            }
        }

        foreach (var item in items)
        {
            if (!item.Eligible)
            {
                results.Add(new RankResult<TId>(item.Id, null, false));
            }
        }

        return results;
    }

    private static Comparison<RankItem<TId>> BuildComparer<TId>(TieBreakRule rule)
        where TId : notnull
    {
        return rule switch
        {
            TieBreakRule.SharedPosition => CompareSharedPosition,
            TieBreakRule.ExamThenCa => CompareExamThenCa,
            TieBreakRule.ExamThenAlphabetical => CompareExamThenAlphabetical,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown tie-break rule."),
        };

        static int CompareSharedPosition(RankItem<TId> a, RankItem<TId> b) =>
            b.PrimarySort.CompareTo(a.PrimarySort);

        static int CompareExamThenCa(RankItem<TId> a, RankItem<TId> b)
        {
            var byPrimary = b.PrimarySort.CompareTo(a.PrimarySort);
            if (byPrimary != 0)
            {
                return byPrimary;
            }

            var byExam = b.TieExam.CompareTo(a.TieExam);
            return byExam != 0 ? byExam : b.TieCa.CompareTo(a.TieCa);
        }

        static int CompareExamThenAlphabetical(RankItem<TId> a, RankItem<TId> b)
        {
            var byExamThenCa = CompareExamThenCa(a, b);
            return byExamThenCa != 0 ? byExamThenCa : string.CompareOrdinal(a.Surname, b.Surname);
        }
    }
}
