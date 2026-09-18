using System.Globalization;
using System.Text.Json;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Builds a <see cref="ScoreSheetDto"/> from its raw ingredients — THE one place both
/// <c>GetScoreSheetHandler</c> and <c>SaveScoreSheetHandler</c> (for its "refreshed" response)
/// assemble the sheet, so the two can never drift into reporting it differently.
/// </summary>
internal static class ScoreSheetProjection
{
    public static ScoreSheetDto Build(
        Guid armId,
        Guid subjectId,
        Guid termId,
        ResultSet? resultSet,
        IReadOnlyList<ArmRosterPupil> roster,
        IReadOnlyList<AssessmentComponent> structure,
        IReadOnlyDictionary<Guid, ScoreSheetVersionRow> scoresByPupil)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(scoresByPupil);

        var nonExamComponents = structure
            .Where(component => !component.IsExamination)
            .OrderBy(component => component.DisplayOrder)
            .ToArray();
        var examComponent = structure.Single(component => component.IsExamination);

        var componentKeys = nonExamComponents
            .Select(component => component.Id.ToString("D", CultureInfo.InvariantCulture))
            .ToArray();

        var componentDtos = nonExamComponents
            .Select(component => new ScoreSheetComponentDto(
                component.Id.ToString("D", CultureInfo.InvariantCulture), component.ShortLabel, component.MaxMark))
            .ToArray();

        var examinationDto = new ScoreSheetComponentDto(
            examComponent.Id.ToString("D", CultureInfo.InvariantCulture), examComponent.ShortLabel, examComponent.MaxMark);

        var rows = roster.Select(pupil => BuildRow(pupil, scoresByPupil, componentKeys, nonExamComponents)).ToArray();

        var version = ScoreSheetVersion.Compute(scoresByPupil.Values.ToArray());

        var resultSetDto = resultSet is null
            ? null
            : new ResultSetSummaryDto(
                resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute);

        return new ScoreSheetDto(
            armId.ToString("D", CultureInfo.InvariantCulture),
            subjectId.ToString("D", CultureInfo.InvariantCulture),
            termId.ToString("D", CultureInfo.InvariantCulture),
            version,
            resultSetDto,
            componentDtos,
            examinationDto,
            rows);
    }

    private static ScoreSheetRowDto BuildRow(
        ArmRosterPupil pupil,
        IReadOnlyDictionary<Guid, ScoreSheetVersionRow> scoresByPupil,
        IReadOnlyList<string> componentKeys,
        AssessmentComponent[] nonExamComponents)
    {
        Dictionary<string, int?> componentMarks;
        int? examMark = null;
        var examAbsent = false;

        if (scoresByPupil.TryGetValue(pupil.PupilId, out var score))
        {
            componentMarks = JsonSerializer.Deserialize<Dictionary<string, int?>>(score.ComponentMarksJson)
                ?? new Dictionary<string, int?>(StringComparer.Ordinal);
            examMark = score.ExamMark;
            examAbsent = score.ExamAbsent;
        }
        else
        {
            componentMarks = new Dictionary<string, int?>(StringComparer.Ordinal);
        }

        // Every known non-examination component id is always a key — blank (null) for a pupil with
        // no row at all, or one whose stored row predates a component this handler cannot see yet.
        foreach (var key in componentKeys)
        {
            if (!componentMarks.ContainsKey(key))
            {
                componentMarks[key] = null;
            }
        }

        var caComplete = nonExamComponents.Length > 0 && nonExamComponents.All(component =>
            componentMarks.TryGetValue(component.Id.ToString("D", CultureInfo.InvariantCulture), out var mark) && mark is not null);
        var examComplete = examAbsent || examMark is not null;

        int? caTotal = caComplete
            ? nonExamComponents.Sum(component => componentMarks[component.Id.ToString("D", CultureInfo.InvariantCulture)])
            : null;

        int? subjectTotal = caComplete && examComplete
            ? (examAbsent ? caTotal : caTotal + examMark)
            : null;

        return new ScoreSheetRowDto(
            pupil.PupilId.ToString("D", CultureInfo.InvariantCulture),
            pupil.RegistrationNumber,
            ComposeDisplayName(pupil),
            componentMarks,
            examMark,
            examAbsent,
            caTotal,
            subjectTotal);
    }

    private static string ComposeDisplayName(ArmRosterPupil pupil) =>
        string.IsNullOrWhiteSpace(pupil.MiddleName)
            ? $"{pupil.Surname} {pupil.FirstName}"
            : $"{pupil.Surname} {pupil.FirstName} {pupil.MiddleName}";
}
