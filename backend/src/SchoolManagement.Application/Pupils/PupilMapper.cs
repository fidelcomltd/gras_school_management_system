using System.Globalization;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils;

/// <summary>Maps <see cref="Pupil"/> to <see cref="PupilDto"/>. Never exposes the entity itself.</summary>
public static class PupilMapper
{
    /// <summary>Maps <paramref name="pupil"/> to its DTO.</summary>
    /// <param name="pupil">The entity to map.</param>
    /// <param name="asOfDate">"Today", for <see cref="PupilDto.AgeYears"/>.</param>
    /// <param name="matchedField">
    /// The field a search term matched against <paramref name="pupil"/>, or <see langword="null"/>
    /// when no search term was in play — see <see cref="PupilSearchMatcher"/>.
    /// </param>
    /// <param name="admission">See <see cref="PupilDto.Admission"/>'s own remarks — only <c>CreatePupilHandler</c> supplies this today.</param>
    /// <param name="levelAppliedFor">See <see cref="PupilDto.LevelAppliedFor"/>'s own remarks — only the admissions queue supplies this today.</param>
    /// <param name="dateApplicationReceived">See <see cref="PupilDto.DateApplicationReceived"/>'s own remarks — only the admissions queue supplies this today.</param>
    /// <param name="missing">See <see cref="PupilDto.Missing"/>'s own remarks — only the admissions queue supplies this today.</param>
    public static PupilDto ToDto(
        Pupil pupil,
        DateOnly asOfDate,
        string? matchedField = null,
        AdmissionRecordDto? admission = null,
        string? levelAppliedFor = null,
        DateOnly? dateApplicationReceived = null,
        IReadOnlyList<string>? missing = null)
    {
        ArgumentNullException.ThrowIfNull(pupil);

        return new PupilDto(
            pupil.Id.ToString("D", CultureInfo.InvariantCulture),
            pupil.RegistrationNumber,
            pupil.Surname,
            pupil.FirstName,
            pupil.MiddleName,
            pupil.Sex,
            pupil.DateOfBirth,
            Pupil.CalculateAgeYears(pupil.DateOfBirth, asOfDate),
            pupil.Nationality,
            pupil.StateOfOrigin,
            pupil.Lga,
            pupil.HomeAddress,
            pupil.PreviousSchool,
            pupil.PreviousClass,
            pupil.Status,
            pupil.OtherInformation,
            matchedField,
            pupil.CreatedAtUtc,
            pupil.CreatedBy,
            admission,
            levelAppliedFor,
            dateApplicationReceived,
            missing);
    }
}
