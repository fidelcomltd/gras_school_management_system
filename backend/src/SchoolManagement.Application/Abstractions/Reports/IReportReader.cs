using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Reports;

/// <summary>An arm with its level, for grouping and naming.</summary>
/// <param name="ArmId">The arm.</param>
/// <param name="Name">Its display name, e.g. "Primary 3A".</param>
/// <param name="LevelId">Its class level.</param>
/// <param name="LevelName">The level's name.</param>
/// <param name="LevelOrder">The level's progression order.</param>
/// <param name="SectionId">The level's section.</param>
/// <param name="Capacity">The arm's capacity, if set.</param>
public sealed record ReportArm(Guid ArmId, string Name, Guid LevelId, string LevelName, int LevelOrder, Guid SectionId, int? Capacity);

/// <summary>A term with its session.</summary>
/// <param name="TermId">The term.</param>
/// <param name="Name">"First Term".</param>
/// <param name="Ordinal">1 to 3.</param>
/// <param name="SessionId">Its session.</param>
/// <param name="SessionName">"2026/2027".</param>
public sealed record ReportTerm(Guid TermId, string Name, int Ordinal, Guid SessionId, string SessionName);

/// <summary>A result set's identity and state.</summary>
/// <param name="ResultSetId">The set.</param>
/// <param name="ArmId">Its arm.</param>
/// <param name="State">Its lifecycle state.</param>
/// <param name="Computed">Whether it has been computed at least once.</param>
public sealed record ReportResultSet(Guid ResultSetId, Guid ArmId, ResultSetState State, bool Computed);

/// <summary>A pupil's name and number.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Surname">Surname.</param>
/// <param name="GivenNames">First and middle names.</param>
/// <param name="RegistrationNumber">Their number, if issued.</param>
public sealed record ReportPupil(Guid PupilId, string Surname, string GivenNames, string? RegistrationNumber)
{
    /// <summary>"OKAFOR Chidera Ada", as the result sheet prints it.</summary>
    public string DisplayName => $"{Surname.ToUpperInvariant()} {GivenNames}".Trim();
}

/// <summary>A pupil's computed term result.</summary>
/// <param name="ResultSetId">The set.</param>
/// <param name="PupilId">The pupil.</param>
/// <param name="TotalObtained">Sum of subject totals.</param>
/// <param name="Average">Term average.</param>
/// <param name="Grade">Overall grade.</param>
/// <param name="ArmPosition">Position in the arm, if ranked.</param>
/// <param name="ArmTied">Tied at that position.</param>
/// <param name="LevelPosition">Position across the level, if ranked.</param>
/// <param name="LevelTied">Tied at that position.</param>
public sealed record ReportTermResult(
    Guid ResultSetId, Guid PupilId, int TotalObtained, decimal Average, string Grade, int? ArmPosition, bool ArmTied, int? LevelPosition, bool LevelTied);

/// <summary>A pupil's computed line in one subject.</summary>
/// <param name="ResultSetId">The set.</param>
/// <param name="PupilId">The pupil.</param>
/// <param name="SubjectId">The subject.</param>
/// <param name="CaTotal">Continuous assessment total.</param>
/// <param name="ExamMark">Examination mark, null when absent.</param>
/// <param name="SubjectTotal">Out of 100.</param>
/// <param name="Grade">Band letter.</param>
/// <param name="IsPass">At or above the pass mark.</param>
public sealed record ReportSubjectLine(Guid ResultSetId, Guid PupilId, Guid SubjectId, int CaTotal, int? ExamMark, int SubjectTotal, string Grade, bool IsPass);

/// <summary>A subject mapped to a level for a term, in the level's display order.</summary>
/// <param name="LevelId">The level.</param>
/// <param name="SubjectId">The subject.</param>
/// <param name="Name">Its name.</param>
/// <param name="DisplayOrder">Its place on the sheet.</param>
public sealed record ReportSubject(Guid LevelId, Guid SubjectId, string Name, int DisplayOrder);

/// <summary>A grading band, in display order.</summary>
/// <param name="Letter">The grade letter.</param>
/// <param name="LowerBound">Lowest total in the band.</param>
/// <param name="UpperBound">Highest total in the band.</param>
/// <param name="Remark">The band's word.</param>
public sealed record ReportGradeBand(string Letter, int LowerBound, int UpperBound, string Remark);

/// <summary>A development indicator with its domain (nursery, spec 6.2.9).</summary>
/// <param name="IndicatorId">The indicator.</param>
/// <param name="Name">Its wording.</param>
/// <param name="DomainId">Its domain.</param>
/// <param name="DomainName">The domain's name.</param>
/// <param name="RatingScaleId">The domain's rating scale.</param>
public sealed record ReportIndicator(Guid IndicatorId, string Name, Guid DomainId, string DomainName, Guid RatingScaleId);

/// <summary>A rating-scale point.</summary>
/// <param name="PointId">The point.</param>
/// <param name="RatingScaleId">Its scale.</param>
/// <param name="Label">Its wording.</param>
/// <param name="Order">Its place on the scale.</param>
public sealed record ReportRatingPoint(Guid PointId, Guid RatingScaleId, string Label, int Order);

/// <summary>A pupil's development rating.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="IndicatorId">The indicator.</param>
/// <param name="PointId">The point given.</param>
public sealed record ReportDevelopmentRating(Guid PupilId, Guid IndicatorId, Guid PointId);

/// <summary>A configured fee line and its amount for one level in one term (spec 6.2.13).</summary>
/// <param name="LevelId">The level.</param>
/// <param name="Label">The line's wording.</param>
/// <param name="DisplayOrder">Its place on the notice.</param>
/// <param name="Amount">Naira, or null when no amount is set for this level.</param>
public sealed record ReportFeeLine(Guid LevelId, string Label, int DisplayOrder, int? Amount);

/// <summary>A session, for choosing and naming.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="Name">"2026/2027".</param>
/// <param name="StartDate">For ordering.</param>
public sealed record ReportSession(Guid SessionId, string Name, DateOnly StartDate);

/// <summary>A pupil's annual cumulative row (spec 6.7.10).</summary>
/// <param name="ArmId">The arm the annual run placed them in.</param>
/// <param name="PupilId">The pupil.</param>
/// <param name="TermAverages">First, second and third term averages, null where not taken.</param>
/// <param name="CumulativeAverage">The cumulative average.</param>
/// <param name="Grade">Its grade.</param>
/// <param name="Position">Annual position, if ranked.</param>
/// <param name="Tied">Tied at that position.</param>
/// <param name="ProposedOutcome">The computed proposal.</param>
/// <param name="SubjectsJson">Per-subject annual means (<c>AnnualSubjectResult</c>s).</param>
public sealed record ReportAnnualResult(
    Guid ArmId,
    Guid PupilId,
    IReadOnlyList<decimal?> TermAverages,
    decimal CumulativeAverage,
    string Grade,
    int? Position,
    bool Tied,
    PromotionOutcome ProposedOutcome,
    string SubjectsJson);

/// <summary>A committed (not reversed) promotion decision for a pupil (spec 6.3.7).</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Outcome">The final outcome.</param>
/// <param name="TargetArmId">The arm in the next session, if any.</param>
/// <param name="TargetSessionId">The next session.</param>
/// <param name="Reason">Recorded when the outcome overrides the proposal.</param>
public sealed record ReportPromotionDecision(Guid PupilId, PromotionDecisionOutcome Outcome, Guid? TargetArmId, Guid TargetSessionId, string? Reason);

/// <summary>One computed term result in a pupil's history.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="TermOrdinal">1 to 3.</param>
/// <param name="TermName">"First Term".</param>
/// <param name="ArmId">The arm the result belongs to.</param>
/// <param name="Average">Term average.</param>
/// <param name="Grade">Overall grade.</param>
/// <param name="ArmPosition">Position in the arm.</param>
/// <param name="ArmTied">Tied there.</param>
/// <param name="LevelPosition">Position in the level.</param>
/// <param name="LevelTied">Tied there.</param>
/// <param name="State">The result set's state.</param>
public sealed record ReportPupilTerm(
    Guid SessionId, int TermOrdinal, string TermName, Guid ArmId, decimal Average, string Grade,
    int? ArmPosition, bool ArmTied, int? LevelPosition, bool LevelTied, ResultSetState State);

/// <summary>Read-only projections the reports assemble from (spec 15 section 10). Every list is small: one session's worth.</summary>
public interface IReportReader
{
    /// <summary>The session's arms, by level progression then label.</summary>
    Task<IReadOnlyList<ReportArm>> ListArmsAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>The term and its session, or null.</summary>
    Task<ReportTerm?> FindTermAsync(Guid termId, CancellationToken cancellationToken);

    /// <summary>Every result set for the term.</summary>
    Task<IReadOnlyList<ReportResultSet>> ListResultSetsAsync(Guid termId, CancellationToken cancellationToken);

    /// <summary>The computed term results in these sets.</summary>
    Task<IReadOnlyList<ReportTermResult>> ListTermResultsAsync(IReadOnlyCollection<Guid> resultSetIds, CancellationToken cancellationToken);

    /// <summary>The computed subject lines in these sets.</summary>
    Task<IReadOnlyList<ReportSubjectLine>> ListSubjectLinesAsync(IReadOnlyCollection<Guid> resultSetIds, CancellationToken cancellationToken);

    /// <summary>These pupils' names and numbers, by id.</summary>
    Task<IReadOnlyDictionary<Guid, ReportPupil>> FindPupilsAsync(IReadOnlyCollection<Guid> pupilIds, CancellationToken cancellationToken);

    /// <summary>The subjects mapped to these levels for the term (active mappings), in display order.</summary>
    Task<IReadOnlyList<ReportSubject>> ListMappedSubjectsAsync(Guid termId, IReadOnlyCollection<Guid> levelIds, CancellationToken cancellationToken);

    /// <summary>The grading bands, in display order.</summary>
    Task<IReadOnlyList<ReportGradeBand>> ListGradeBandsAsync(CancellationToken cancellationToken);

    /// <summary>The ACTIVE indicators of the section's ACTIVE domains (what the sheet shows), in domain then indicator order.</summary>
    Task<IReadOnlyList<ReportIndicator>> ListIndicatorsAsync(Guid sectionId, CancellationToken cancellationToken);

    /// <summary>The points of these rating scales.</summary>
    Task<IReadOnlyList<ReportRatingPoint>> ListRatingPointsAsync(IReadOnlyCollection<Guid> ratingScaleIds, CancellationToken cancellationToken);

    /// <summary>The development ratings in this result set.</summary>
    Task<IReadOnlyList<ReportDevelopmentRating>> ListDevelopmentRatingsAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>Every amount-type fee line of these levels' sections, with each level's amount for the term (null when unset).</summary>
    Task<IReadOnlyList<ReportFeeLine>> ListFeeLinesAsync(Guid termId, IReadOnlyCollection<Guid> levelIds, CancellationToken cancellationToken);

    /// <summary>The pupils (non-pending) enrolled in the arm at any point in the term: the class roster a count is out of.</summary>
    Task<IReadOnlyList<Guid>> ListRosterAsync(Guid armId, Guid termId, CancellationToken cancellationToken);

    /// <summary>The typed outstanding figures in these result sets (0 included: it prints), by set: pupils carrying one and their total.</summary>
    Task<IReadOnlyDictionary<Guid, (int Pupils, long Total)>> SumOutstandingAsync(IReadOnlyCollection<Guid> resultSetIds, CancellationToken cancellationToken);

    /// <summary>A session by id, or null.</summary>
    Task<ReportSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>These sessions, by id.</summary>
    Task<IReadOnlyDictionary<Guid, ReportSession>> FindSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken);

    /// <summary>The session's annual cumulative rows.</summary>
    Task<IReadOnlyList<ReportAnnualResult>> ListAnnualResultsAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Every annual row of one pupil, any session.</summary>
    Task<IReadOnlyList<(Guid SessionId, ReportAnnualResult Annual)>> ListPupilAnnualAsync(Guid pupilId, CancellationToken cancellationToken);

    /// <summary>The decisions of the session's committed, not reversed, promotion batch (empty when none).</summary>
    Task<IReadOnlyList<ReportPromotionDecision>> ListPromotionDecisionsAsync(Guid sourceSessionId, CancellationToken cancellationToken);

    /// <summary>Promotion's rule inputs as configured now: the core subjects and the pass mark.</summary>
    Task<(IReadOnlyList<Guid> CoreSubjectIds, int PassMark)> GetCoreRulesAsync(CancellationToken cancellationToken);

    /// <summary>Every computed term result of one pupil, any session.</summary>
    Task<IReadOnlyList<ReportPupilTerm>> ListPupilTermsAsync(Guid pupilId, CancellationToken cancellationToken);

    /// <summary>These arms with their levels, whatever their session.</summary>
    Task<IReadOnlyDictionary<Guid, ReportArm>> FindArmsAsync(IReadOnlyCollection<Guid> armIds, CancellationToken cancellationToken);

    /// <summary>These subjects' names, by id.</summary>
    Task<IReadOnlyDictionary<Guid, string>> FindSubjectNamesAsync(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken);
}
