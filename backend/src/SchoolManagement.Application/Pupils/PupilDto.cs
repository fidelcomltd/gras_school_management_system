using SchoolManagement.Application.Admissions;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// The pupil read shape for this card: list, detail, the admissions queue and duplicate candidates
/// all use this one DTO — 6.5.15's extra detail-view sections (contacts, health, enrolment history)
/// and list-view completeness column do not exist yet (see the task card's own out-of-scope list),
/// so there is nothing today that would make a Summary/Detail split carry different fields.
/// </summary>
/// <param name="Id">Opaque to the client.</param>
/// <param name="RegistrationNumber"><see langword="null"/> while pending — always null within this card.</param>
/// <param name="Surname">As typed (spec 6.5.4: displayed uppercase only on the result sheet, out of scope here).</param>
/// <param name="FirstName">As typed.</param>
/// <param name="MiddleName"><see langword="null"/> when not supplied.</param>
/// <param name="Sex">Male or female.</param>
/// <param name="DateOfBirth">ISO-8601 date.</param>
/// <param name="AgeYears">Derived, not stored — whole years as of today (spec 6.5.4's term-end computation is result-sheet-specific and out of scope; see <c>backend/docs/ASSUMPTIONS.md</c> §2.27).</param>
/// <param name="Nationality">Free text; defaults <c>Nigerian</c>.</param>
/// <param name="StateOfOrigin">One of the 36 states or the FCT.</param>
/// <param name="Lga">One of <see cref="StateOfOrigin"/>'s Local Government Areas.</param>
/// <param name="HomeAddress">The child's own address.</param>
/// <param name="PreviousSchool"><see langword="null"/> when not supplied.</param>
/// <param name="PreviousClass"><see langword="null"/> when not supplied.</param>
/// <param name="Status">Pending for every record this card creates; the other members exist for the column's shape only.</param>
/// <param name="OtherInformation"><see langword="null"/> when not supplied.</param>
/// <param name="MatchedField">
/// Which field a search term matched (<c>"Surname"</c>, <c>"FirstName"</c>, <c>"MiddleName"</c> or
/// <c>"RegistrationNumber"</c>), or <see langword="null"/> when no search term was given. Contact and
/// authorised-pickup-person search (spec 6.5.15) are the NEXT card's — never a value here.
/// </param>
/// <param name="CreatedAtUtc">System.</param>
/// <param name="CreatedBy">The admin account id that created the record, or <see langword="null"/>.</param>
/// <param name="Admission">
/// Sections A, I and J of the admission form (spec 6.5.9, TASK-0062). Populated only by
/// <c>CreatePupilHandler</c>'s own response today — <see langword="null"/> on every other read path
/// in this card (list, single-get, duplicates), which do not load the admission record.
/// </param>
/// <param name="LevelAppliedFor">
/// The admission record's <c>class_admitted_into</c> display name. Populated ONLY by the admissions
/// queue (TASK-0062; spec 6.5.15's queue column) — <see langword="null"/> everywhere else.
/// </param>
/// <param name="DateApplicationReceived">
/// The admission record's own field, lifted onto the row for the admissions queue (TASK-0062; spec
/// 6.5.15). Populated ONLY by the admissions queue — <see langword="null"/> everywhere else.
/// </param>
/// <param name="Missing">
/// The admissions queue's own "what is missing" column (TASK-0062; spec 6.5.11 step 9, 6.5.15).
/// Restricted to what sections A and I's stored fields can check today: steps 2 to 8 (contacts,
/// health, barred persons, pickup persons, documents) have no entity yet, so a gap there can never
/// appear here — a known, recorded limitation, not a claim of completeness. Populated ONLY by the
/// admissions queue — <see langword="null"/> everywhere else.
/// </param>
public sealed record PupilDto(
    string Id,
    string? RegistrationNumber,
    string Surname,
    string FirstName,
    string? MiddleName,
    PupilSex Sex,
    DateOnly DateOfBirth,
    int AgeYears,
    string Nationality,
    string StateOfOrigin,
    string Lga,
    string HomeAddress,
    string? PreviousSchool,
    string? PreviousClass,
    PupilStatus Status,
    string? OtherInformation,
    string? MatchedField,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    AdmissionRecordDto? Admission = null,
    string? LevelAppliedFor = null,
    DateOnly? DateApplicationReceived = null,
    IReadOnlyList<string>? Missing = null);
