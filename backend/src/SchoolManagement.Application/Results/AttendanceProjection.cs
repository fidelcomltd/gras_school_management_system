using System.Globalization;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Builds an <see cref="AttendanceSheetDto"/> from its raw ingredients — THE one place both
/// <c>GetAttendanceHandler</c> and <c>SaveAttendanceHandler</c> (for its "refreshed" response)
/// assemble the sheet, same convention as <c>TraitRatingProjection</c>.
/// </summary>
internal static class AttendanceProjection
{
    public static AttendanceSheetDto Build(
        Guid armId,
        Guid termId,
        int? timesSchoolOpened,
        ResultSet? resultSet,
        IReadOnlyList<ArmRosterPupil> roster,
        IReadOnlyCollection<AttendanceEntrySnapshot> allEntries)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(allEntries);

        var entriesByPupil = allEntries.ToDictionary(entry => entry.PupilId, entry => entry.TimesPresent);

        var rows = roster.Select(pupil => BuildRow(pupil, timesSchoolOpened, entriesByPupil)).ToArray();

        var version = AttendanceVersion.Compute(allEntries.ToArray());

        var resultSetDto = resultSet is null
            ? null
            : new ResultSetSummaryDto(
                resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute, resultSet.ReturnReason);

        return new AttendanceSheetDto(
            armId.ToString("D", CultureInfo.InvariantCulture),
            termId.ToString("D", CultureInfo.InvariantCulture),
            version,
            resultSetDto,
            timesSchoolOpened,
            rows);
    }

    private static AttendanceRowDto BuildRow(ArmRosterPupil pupil, int? timesSchoolOpened, Dictionary<Guid, int> entriesByPupil)
    {
        var timesPresent = entriesByPupil.TryGetValue(pupil.PupilId, out var present) ? present : (int?)null;
        var timesAbsent = timesPresent is null || timesSchoolOpened is null ? (int?)null : timesSchoolOpened.Value - timesPresent.Value;

        return new AttendanceRowDto(
            pupil.PupilId.ToString("D", CultureInfo.InvariantCulture),
            pupil.RegistrationNumber,
            ComposeDisplayName(pupil),
            timesPresent,
            timesAbsent);
    }

    private static string ComposeDisplayName(ArmRosterPupil pupil) =>
        string.IsNullOrWhiteSpace(pupil.MiddleName)
            ? $"{pupil.Surname} {pupil.FirstName}"
            : $"{pupil.Surname} {pupil.FirstName} {pupil.MiddleName}";
}
