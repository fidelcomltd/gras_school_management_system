using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>Everything the annual sheet needs for one pupil and session.</summary>
/// <param name="Row">The stored annual result.</param>
/// <param name="ThirdTermSnapshotJson">The final arm's Third Term publication snapshot; null when that set is not published now.</param>
/// <param name="PupilSurname">Printed upper-case.</param>
/// <param name="PupilGivenNames">First and middle names.</param>
/// <param name="RegistrationNumber">Current number.</param>
/// <param name="SubjectNames">Subject id to name, for every subject in the row (some may not be on the Third Term snapshot).</param>
public sealed record AnnualSheetData(
    AnnualResult Row,
    string? ThirdTermSnapshotJson,
    string PupilSurname,
    string PupilGivenNames,
    string RegistrationNumber,
    IReadOnlyDictionary<Guid, string> SubjectNames);

/// <summary>Reads annual sheets.</summary>
public interface IAnnualSheetReader
{
    /// <summary>The pupil's annual result in the session, or null when none has been computed.</summary>
    Task<AnnualSheetData?> ReadAsync(Guid pupilId, Guid sessionId, CancellationToken cancellationToken);
}
