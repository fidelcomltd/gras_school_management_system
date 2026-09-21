using System.Globalization;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Builds a <see cref="RemarkSheetDto"/> from its raw ingredients — THE one place every
/// class-teacher and head-teacher remark handler assembles its sheet, same convention as
/// <c>TraitRatingProjection</c>. Kind-agnostic: the caller has already read the right
/// <see cref="RemarkKind"/> slice.
/// </summary>
internal static class RemarkProjection
{
    public static RemarkSheetDto Build(
        Guid armId,
        Guid termId,
        ResultSet? resultSet,
        IReadOnlyList<ArmRosterPupil> roster,
        IReadOnlyCollection<PupilRemarkSnapshot> allRemarks)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(allRemarks);

        var remarksByPupil = allRemarks.ToDictionary(remark => remark.PupilId);

        var rows = roster.Select(pupil => BuildRow(pupil, remarksByPupil)).ToArray();

        var version = RemarkVersion.Compute(allRemarks.ToArray());

        var resultSetDto = resultSet is null
            ? null
            : new ResultSetSummaryDto(
                resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute);

        return new RemarkSheetDto(
            armId.ToString("D", CultureInfo.InvariantCulture),
            termId.ToString("D", CultureInfo.InvariantCulture),
            version,
            resultSetDto,
            rows);
    }

    private static RemarkRowDto BuildRow(ArmRosterPupil pupil, Dictionary<Guid, PupilRemarkSnapshot> remarksByPupil)
    {
        remarksByPupil.TryGetValue(pupil.PupilId, out var remark);

        return new RemarkRowDto(
            pupil.PupilId.ToString("D", CultureInfo.InvariantCulture),
            pupil.RegistrationNumber,
            ComposeDisplayName(pupil),
            remark?.Text,
            remark?.WrittenByName,
            remark?.WrittenAtUtc);
    }

    private static string ComposeDisplayName(ArmRosterPupil pupil) =>
        string.IsNullOrWhiteSpace(pupil.MiddleName)
            ? $"{pupil.Surname} {pupil.FirstName}"
            : $"{pupil.Surname} {pupil.FirstName} {pupil.MiddleName}";
}
