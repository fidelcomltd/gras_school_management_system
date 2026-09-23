using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils.Import;

/// <summary>Whether a row can be imported.</summary>
public enum PupilImportRowOutcome
{
    /// <summary>Valid. It may still carry register matches that need a skip-or-create decision.</summary>
    Accepted,

    /// <summary>At least one error. Nothing imports until it is fixed.</summary>
    Rejected,
}

/// <summary>
/// The validation report (spec 6.5.13): every data row with its outcome, plus per-arm capacity warnings. Health answers
/// are never echoed back.
/// </summary>
/// <param name="FileSha256">The uploaded file's SHA-256, hex. Commit must send it back, so decisions cannot land on another file.</param>
/// <param name="TotalRows">Data rows in the file.</param>
/// <param name="AcceptedCount">Rows with no error.</param>
/// <param name="RejectedCount">Rows with at least one error.</param>
/// <param name="RegisterMatchCount">Accepted rows matching a pupil already on the register; each needs skip or create.</param>
/// <param name="Rows">Every data row, in file order.</param>
/// <param name="CapacityWarnings">Arms this file would take over capacity, counting every accepted row.</param>
public sealed record PupilImportReportDto(
    string FileSha256,
    int TotalRows,
    int AcceptedCount,
    int RejectedCount,
    int RegisterMatchCount,
    IReadOnlyList<PupilImportRowDto> Rows,
    IReadOnlyList<PupilImportCapacityWarningDto> CapacityWarnings);

/// <summary>One data row of the report.</summary>
/// <param name="SheetRow">The spreadsheet row number (the header is row 1).</param>
/// <param name="Surname">As typed, when present.</param>
/// <param name="FirstName">As typed, when present.</param>
/// <param name="DateOfBirth">When it could be read.</param>
/// <param name="ArmId">The resolved arm, when it could be.</param>
/// <param name="ArmName">The resolved arm's display name.</param>
/// <param name="Outcome">Accepted or rejected.</param>
/// <param name="Errors">Why the row is rejected: one entry per problem, each naming its column.</param>
/// <param name="RegisterMatches">Existing pupils with the same surname, first name and date of birth. A warning, not an error.</param>
public sealed record PupilImportRowDto(
    int SheetRow,
    string? Surname,
    string? FirstName,
    DateOnly? DateOfBirth,
    string? ArmId,
    string? ArmName,
    PupilImportRowOutcome Outcome,
    IReadOnlyList<PupilImportIssueDto> Errors,
    IReadOnlyList<PupilImportRegisterMatchDto> RegisterMatches);

/// <summary>One problem with a row.</summary>
/// <param name="Column">The template column it concerns.</param>
/// <param name="Message">What is wrong and how to fix it.</param>
public sealed record PupilImportIssueDto(string Column, string Message);

/// <summary>A pupil already on the register that a row appears to duplicate.</summary>
/// <param name="PupilId">The existing pupil.</param>
/// <param name="RegistrationNumber">Null while that pupil is pending.</param>
/// <param name="Status">Its status.</param>
/// <param name="Surname">Its surname.</param>
/// <param name="FirstName">Its first name.</param>
/// <param name="DateOfBirth">Its date of birth.</param>
public sealed record PupilImportRegisterMatchDto(
    string PupilId, string? RegistrationNumber, PupilStatus Status, string Surname, string FirstName, DateOnly DateOfBirth);

/// <summary>An arm the file would take over its capacity (spec 6.4.6: a warning, overridable with <c>arm.capacity.override</c>).</summary>
/// <param name="ArmId">The arm.</param>
/// <param name="ArmName">Its display name.</param>
/// <param name="Capacity">Its capacity.</param>
/// <param name="CurrentCount">Pupils enrolled now.</param>
/// <param name="ImportCount">Rows in this file for it.</param>
public sealed record PupilImportCapacityWarningDto(string ArmId, string ArmName, int Capacity, int CurrentCount, int ImportCount);

/// <summary>What a commit created.</summary>
/// <param name="ImportedCount">Pupils created.</param>
/// <param name="SkippedCount">Rows skipped by decision.</param>
/// <param name="Pupils">Each created pupil, in file order, which is registration-number order.</param>
public sealed record PupilImportResultDto(int ImportedCount, int SkippedCount, IReadOnlyList<PupilImportedDto> Pupils);

/// <summary>One created pupil.</summary>
/// <param name="SheetRow">Its row in the file.</param>
/// <param name="PupilId">The new pupil.</param>
/// <param name="RegistrationNumber">The number issued.</param>
public sealed record PupilImportedDto(int SheetRow, string PupilId, string RegistrationNumber);
