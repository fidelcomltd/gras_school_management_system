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
    string? CreatedBy);
