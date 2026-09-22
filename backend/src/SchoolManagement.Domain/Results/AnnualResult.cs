using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>Spec 6.3.7's outcomes. The system proposes only the first two; on trial is a human decision.</summary>
public enum PromotionOutcome
{
    /// <summary>Moves up (or graduates from the terminal level).</summary>
    Promoted,

    /// <summary>Stays at the same level.</summary>
    Repeat,

    /// <summary>Moves up despite falling short; never proposed, only chosen with a reason.</summary>
    PromotedOnTrial,
}

/// <summary>One subject across the year (6.7.10 per_subject_annual).</summary>
/// <param name="SubjectId">The subject.</param>
/// <param name="TermTotals">Index 0 is First Term; null where the subject was not taken that term.</param>
/// <param name="Mean">Mean of the terms taken, two places.</param>
/// <param name="Grade">Band letter for <paramref name="Mean"/>.</param>
/// <param name="TermsTaken">Below three prints a footnote.</param>
public sealed record AnnualSubjectResult(Guid SubjectId, IReadOnlyList<int?> TermTotals, decimal Mean, string? Grade, int TermsTaken);

/// <summary>
/// A pupil's annual cumulative result (spec 6.7.10), in the arm they ended the session in. Rewritten whole by each run of
/// the annual computation, so it is never edited in place; the promotion decision (6.3.7) will be stored beside it.
/// </summary>
public sealed class AnnualResult : Entity<Guid>
{
    private AnnualResult(Guid id)
        : base(id)
    {
        SubjectsJson = "[]";
        CumulativeGrade = string.Empty;
        CumulativeRemark = string.Empty;
    }

    // EF Core materialisation constructor.
    private AnnualResult()
        : this(Guid.Empty)
    {
    }

    /// <summary>The session.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>The arm the pupil ended the session in.</summary>
    public Guid ArmId { get; private set; }

    /// <summary>The pupil. Unique per session.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>1 to 3. Below 3 prints "based on n of 3 terms".</summary>
    public int TermsCounted { get; private set; }

    /// <summary>First Term average, when sat.</summary>
    public decimal? FirstTermAverage { get; private set; }

    /// <summary>Second Term average, when sat.</summary>
    public decimal? SecondTermAverage { get; private set; }

    /// <summary>Third Term average, when sat.</summary>
    public decimal? ThirdTermAverage { get; private set; }

    /// <summary>First Term total obtained, when sat.</summary>
    public int? FirstTermTotal { get; private set; }

    /// <summary>Second Term total obtained, when sat.</summary>
    public int? SecondTermTotal { get; private set; }

    /// <summary>Third Term total obtained, when sat.</summary>
    public int? ThirdTermTotal { get; private set; }

    /// <summary>Sum of the term totals sat.</summary>
    public int GrandTotal { get; private set; }

    /// <summary>Two places, per the school's annual method.</summary>
    public decimal CumulativeAverage { get; private set; }

    /// <summary>Band letter from the Third Term snapshot.</summary>
    public string CumulativeGrade { get; private set; }

    /// <summary>The band's word.</summary>
    public string CumulativeRemark { get; private set; }

    /// <summary>Competition rank in the final arm; null when not ranked (a single term).</summary>
    public int? AnnualPosition { get; private set; }

    /// <summary>Shares its position.</summary>
    public bool AnnualPositionTied { get; private set; }

    /// <summary>Pupils ranked in the arm.</summary>
    public int AnnualPupilCount { get; private set; }

    /// <summary><see cref="AnnualSubjectResult"/> rows as JSON (jsonb).</summary>
    public string SubjectsJson { get; private set; }

    /// <summary>What the rules propose.</summary>
    public PromotionOutcome ProposedOutcome { get; private set; }

    /// <summary>When the run wrote this row.</summary>
    public DateTimeOffset ComputedAtUtc { get; private set; }

    /// <summary>Creates a row from one computation run.</summary>
    public static AnnualResult Create(
        Guid sessionId,
        Guid armId,
        Guid pupilId,
        int termsCounted,
        IReadOnlyList<decimal?> termAverages,
        IReadOnlyList<int?> termTotals,
        int grandTotal,
        decimal cumulativeAverage,
        string cumulativeGrade,
        string cumulativeRemark,
        int? annualPosition,
        bool annualPositionTied,
        int annualPupilCount,
        string subjectsJson,
        PromotionOutcome proposedOutcome,
        DateTimeOffset computedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(termAverages);
        ArgumentNullException.ThrowIfNull(termTotals);
        return new AnnualResult(Guid.CreateVersion7())
        {
            SessionId = sessionId,
            ArmId = armId,
            PupilId = pupilId,
            TermsCounted = termsCounted,
            FirstTermAverage = termAverages[0],
            SecondTermAverage = termAverages[1],
            ThirdTermAverage = termAverages[2],
            FirstTermTotal = termTotals[0],
            SecondTermTotal = termTotals[1],
            ThirdTermTotal = termTotals[2],
            GrandTotal = grandTotal,
            CumulativeAverage = cumulativeAverage,
            CumulativeGrade = cumulativeGrade,
            CumulativeRemark = cumulativeRemark,
            AnnualPosition = annualPosition,
            AnnualPositionTied = annualPositionTied,
            AnnualPupilCount = annualPupilCount,
            SubjectsJson = subjectsJson,
            ProposedOutcome = proposedOutcome,
            ComputedAtUtc = computedAtUtc,
        };
    }
}
