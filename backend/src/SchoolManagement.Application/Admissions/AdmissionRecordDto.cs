using SchoolManagement.Domain.Admissions;

namespace SchoolManagement.Application.Admissions;

/// <summary>
/// Sections A, I and J of the admission form (spec 6.5.9) — nested inside <c>PupilDto.Admission</c>
/// on <c>POST /pupils</c>'s response, and returned directly by <c>PATCH /admissions/{id}</c>.
/// </summary>
/// <param name="SessionId">Opaque to the client. The session admitted into.</param>
/// <param name="DateApplicationReceived"><see langword="null"/> when not supplied.</param>
/// <param name="DateAdmitted">Not in the future. The registration number's year comes from this (spec 6.5.10).</param>
/// <param name="ClassAdmittedInto">Opaque to the client. A class level id.</param>
/// <param name="ClassAdmittedIntoName">The level's display name, for a client that has no other lookup handy.</param>
/// <param name="AdmissionType">New or returning.</param>
/// <param name="AdmissionTypeNote"><see langword="null"/> when not supplied.</param>
/// <param name="AssessmentRequired">Whether an entrance assessment is required.</param>
/// <param name="AssessmentResultRemarks"><see langword="null"/> when not yet recorded — NOT required to save (spec 6.5.9).</param>
/// <param name="AssignedClassTeacher">Opaque admin-account id, or <see langword="null"/>. Informational only at this stage.</param>
/// <param name="DeclarationName"><see langword="null"/> when not yet supplied.</param>
/// <param name="DeclarationSigned">Section I: whether the signed paper form exists.</param>
/// <param name="DeclarationDate"><see langword="null"/> unless <paramref name="DeclarationSigned"/> is <see langword="true"/>.</param>
/// <param name="ApprovedBy">Opaque admin-account id. Written only by admission approval (TASK-0051) — always <see langword="null"/> today.</param>
/// <param name="ApprovedAt">Written only by admission approval (TASK-0051) — always <see langword="null"/> today.</param>
/// <param name="HeadOfSchoolConfirmed">Section J's second signature block. Defaults <see langword="false"/>.</param>
/// <param name="HeadOfSchoolName"><see langword="null"/> when not supplied.</param>
public sealed record AdmissionRecordDto(
    string SessionId,
    DateOnly? DateApplicationReceived,
    DateOnly DateAdmitted,
    string ClassAdmittedInto,
    string? ClassAdmittedIntoName,
    AdmissionType AdmissionType,
    string? AdmissionTypeNote,
    bool AssessmentRequired,
    string? AssessmentResultRemarks,
    string? AssignedClassTeacher,
    string? DeclarationName,
    bool DeclarationSigned,
    DateOnly? DeclarationDate,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    bool HeadOfSchoolConfirmed,
    string? HeadOfSchoolName);
