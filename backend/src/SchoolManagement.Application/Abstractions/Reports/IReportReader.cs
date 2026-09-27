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

    /// <summary>The subjects mapped to these levels for the term (active mappings), plus every subject's name by id.</summary>
    Task<(IReadOnlyList<ReportSubject> Mapped, IReadOnlyDictionary<Guid, string> Names)> ListSubjectsAsync(
        Guid termId, IReadOnlyCollection<Guid> levelIds, CancellationToken cancellationToken);
}
