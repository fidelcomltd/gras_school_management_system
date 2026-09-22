using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>What a verification token resolves to, read-only (spec 6.9.6).</summary>
/// <param name="TokenRevision">The revision the token was printed from.</param>
/// <param name="IssuedAt">That revision's publication.</param>
/// <param name="SnapshotJson">That revision's snapshot: school name, arm, term and session as printed.</param>
/// <param name="CurrentState">The result set's state now.</param>
/// <param name="CurrentRevision">The result set's revision now.</param>
/// <param name="CurrentPublishedAt">The current revision's publication, for the revised-on notice.</param>
/// <param name="PupilSurname">For initials only; never shown in full.</param>
/// <param name="PupilGivenNames">For initials only.</param>
/// <param name="RegistrationNumber">Shown.</param>
/// <param name="TotalObtained">From the computed row, live; shown only while the token's revision is current.</param>
/// <param name="TotalObtainable">Same.</param>
/// <param name="Average">Same.</param>
/// <param name="OverallGrade">Same.</param>
public sealed record ResultVerificationRecord(
    int TokenRevision,
    DateTimeOffset IssuedAt,
    string SnapshotJson,
    ResultSetState CurrentState,
    int CurrentRevision,
    DateTimeOffset? CurrentPublishedAt,
    string PupilSurname,
    string PupilGivenNames,
    string RegistrationNumber,
    int? TotalObtained,
    int? TotalObtainable,
    decimal? Average,
    string? OverallGrade);

/// <summary>Reads verification tokens.</summary>
public interface IResultVerificationReader
{
    /// <summary>The token printed on a pupil's sheet for a revision, or null when none was issued.</summary>
    Task<string?> FindTokenAsync(Guid resultSetId, Guid pupilId, int revisionNumber, CancellationToken cancellationToken);

    /// <summary>What <paramref name="token"/> (normalised) resolves to, or null when unknown.</summary>
    Task<ResultVerificationRecord?> FindAsync(string token, CancellationToken cancellationToken);
}
