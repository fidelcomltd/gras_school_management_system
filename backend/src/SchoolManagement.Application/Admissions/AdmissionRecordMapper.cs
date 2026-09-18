using System.Globalization;
using SchoolManagement.Domain.Admissions;

namespace SchoolManagement.Application.Admissions;

/// <summary>Maps <see cref="AdmissionRecord"/> to <see cref="AdmissionRecordDto"/>. Never exposes the entity itself.</summary>
public static class AdmissionRecordMapper
{
    /// <summary>Maps <paramref name="record"/> to its DTO.</summary>
    /// <param name="record">The entity to map.</param>
    /// <param name="classAdmittedIntoName">The class level's display name, or <see langword="null"/> when not resolved.</param>
    public static AdmissionRecordDto ToDto(AdmissionRecord record, string? classAdmittedIntoName)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new AdmissionRecordDto(
            record.SessionId.ToString("D", CultureInfo.InvariantCulture),
            record.DateApplicationReceived,
            record.DateAdmitted,
            record.ClassAdmittedInto.ToString("D", CultureInfo.InvariantCulture),
            classAdmittedIntoName,
            record.AdmissionType,
            record.AdmissionTypeNote,
            record.AssessmentRequired,
            record.AssessmentResultRemarks,
            record.AssignedClassTeacher?.ToString("D", CultureInfo.InvariantCulture),
            record.DeclarationName,
            record.DeclarationSigned,
            record.DeclarationDate,
            record.ApprovedBy?.ToString("D", CultureInfo.InvariantCulture),
            record.ApprovedAt,
            record.HeadOfSchoolConfirmed,
            record.HeadOfSchoolName);
    }
}
