using System.Globalization;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>Maps <see cref="Subject"/> to its wire shape.</summary>
internal static class SubjectMapper
{
    /// <summary>Projects one subject to its wire shape. The three term-scoped counts are the caller's job (spec 6.6.7; delta amendment 3).</summary>
    public static SubjectDto ToDto(
        Subject subject, int? mappedLevelCount, int? armExceptionCount, int? pupilsTakingCount)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return new SubjectDto(
            subject.Id.ToString("D", CultureInfo.InvariantCulture),
            subject.Name,
            subject.Code,
            subject.Description,
            subject.Status,
            mappedLevelCount,
            armExceptionCount,
            pupilsTakingCount);
    }
}
