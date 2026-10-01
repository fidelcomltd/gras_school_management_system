using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>
/// Reads sheet data: one pupil for one term (the arm they were enrolled in, its result set, and their rows), or every pupil
/// in one result set at once. Both go through the same set-bound read, so the portal and staff printing cannot disagree.
/// </summary>
internal sealed class ResultSheetReader(ApplicationDbContext context) : IResultSheetReader
{
    public async Task<ResultSheetData?> ReadAsync(Guid pupilId, Guid termId, CancellationToken cancellationToken)
    {
        var term = await context.Terms.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return null;
        }

        // The arm of record for the term: the latest enrolment overlapping it (a mid-term move has two).
        var armId = await (
                from enrolment in context.Enrolments.AsNoTracking()
                join arm in context.Arms.AsNoTracking() on enrolment.ArmId equals arm.Id
                where enrolment.PupilId == pupilId && arm.SessionId == term.SessionId
                    && enrolment.EffectiveFrom <= term.EndDate && (enrolment.EffectiveTo == null || enrolment.EffectiveTo >= term.StartDate)
                orderby enrolment.EffectiveFrom descending
                select (Guid?)arm.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (armId is null)
        {
            return null;
        }

        var resultSet = await context.ResultSets.AsNoTracking()
            .FirstOrDefaultAsync(set => set.ArmId == armId && set.TermId == termId, cancellationToken)
            .ConfigureAwait(false);
        return resultSet is null ? null : (await ReadSetAsync(resultSet, [pupilId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(pupilId);
    }

    public async Task<IReadOnlyDictionary<Guid, ResultSheetData>> ReadSetAsync(Guid resultSetId, Guid? pupilId, CancellationToken cancellationToken)
    {
        var resultSet = await context.ResultSets.AsNoTracking()
            .FirstOrDefaultAsync(set => set.Id == resultSetId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null)
        {
            return new Dictionary<Guid, ResultSheetData>();
        }

        List<Guid> pupilIds = pupilId is { } only
            ? [only]
            : await context.SubjectResultLines.AsNoTracking()
                .Where(line => line.ResultSetId == resultSetId)
                .Select(line => line.PupilId)
                .Distinct()
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        return pupilIds.Count == 0 ? new Dictionary<Guid, ResultSheetData>() : await ReadSetAsync(resultSet, pupilIds, cancellationToken).ConfigureAwait(false);
    }

    // One query per table for the whole list, whatever its length. A pupil id with no pupil row is left out.
    private async Task<IReadOnlyDictionary<Guid, ResultSheetData>> ReadSetAsync(ResultSet resultSet, IReadOnlyList<Guid> pupilIds, CancellationToken cancellationToken)
    {
        var setId = resultSet.Id;
        var pupils = await context.Pupils.AsNoTracking()
            .Where(pupil => pupilIds.Contains(pupil.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (pupils.Count == 0)
        {
            return new Dictionary<Guid, ResultSheetData>();
        }

        var scores = (await context.SubjectScores.AsNoTracking()
                .Where(score => score.ResultSetId == setId && pupilIds.Contains(score.PupilId))
                .Select(score => new { score.PupilId, Row = new SheetScoreRow(score.SubjectId, score.ComponentMarksJson, score.ExamMark, score.ExamAbsent, score.VoidedAt != null) })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(entry => entry.PupilId, entry => entry.Row);
        var lines = (await context.SubjectResultLines.AsNoTracking()
                .Where(line => line.ResultSetId == setId && pupilIds.Contains(line.PupilId))
                .Select(line => new { line.PupilId, Row = new SheetLineRow(line.SubjectId, line.SubjectTotal, line.Grade, line.Remark) })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(entry => entry.PupilId, entry => entry.Row);
        var termResults = await context.PupilTermResults.AsNoTracking()
            .Where(result => result.ResultSetId == setId && pupilIds.Contains(result.PupilId))
            .ToDictionaryAsync(result => result.PupilId, cancellationToken).ConfigureAwait(false);
        var traits = (await context.TraitRatings.AsNoTracking()
                .Where(rating => rating.ResultSetId == setId && pupilIds.Contains(rating.PupilId))
                .Select(rating => new { rating.PupilId, Row = new SheetRatingRow(rating.TraitId, rating.RatingScalePointId, null) })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(entry => entry.PupilId, entry => entry.Row);
        var development = (await context.DevelopmentRatings.AsNoTracking()
                .Where(rating => rating.ResultSetId == setId && pupilIds.Contains(rating.PupilId))
                .Select(rating => new { rating.PupilId, Row = new SheetRatingRow(rating.IndicatorId, rating.RatingScalePointId, rating.Comment) })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(entry => entry.PupilId, entry => entry.Row);
        var present = (await context.AttendanceEntries.AsNoTracking()
                .Where(entry => entry.ResultSetId == setId && pupilIds.Contains(entry.PupilId))
                .Select(entry => new { entry.PupilId, entry.TimesPresent })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(entry => entry.PupilId, entry => (int?)entry.TimesPresent);
        var outstanding = (await context.OutstandingFees.AsNoTracking()
                .Where(fee => fee.ResultSetId == setId && pupilIds.Contains(fee.PupilId))
                .Select(fee => new { fee.PupilId, fee.Amount })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(fee => fee.PupilId, fee => (int?)fee.Amount);
        var remarks = (await context.PupilRemarks.AsNoTracking()
                .Where(remark => remark.ResultSetId == setId && pupilIds.Contains(remark.PupilId))
                .Select(remark => new { remark.PupilId, remark.Kind, remark.Text })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(remark => remark.PupilId);
        var previousPublishedAt = resultSet.RevisionNumber > 1
            ? await context.ResultSetSnapshots.AsNoTracking()
                .Where(snapshot => snapshot.ResultSetId == setId && snapshot.RevisionNumber == resultSet.RevisionNumber - 1)
                .Select(snapshot => (DateTimeOffset?)snapshot.PublishedAtUtc)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : null;

        return pupils.ToDictionary(pupil => pupil.Id, Data);

        ResultSheetData Data(Pupil pupil)
        {
            var givenNames = string.Join(' ', new[] { pupil.FirstName, pupil.MiddleName }.Where(name => !string.IsNullOrWhiteSpace(name)));
            var termResult = termResults.GetValueOrDefault(pupil.Id);
            var own = remarks[pupil.Id].ToList();
            return new ResultSheetData(
                setId,
                resultSet.State,
                resultSet.ConfigSnapshotJson,
                resultSet.RevisionNumber,
                resultSet.PublishedAtUtc,
                previousPublishedAt,
                pupil.Surname,
                givenNames,
                pupil.DateOfBirth,
                pupil.RegistrationNumber ?? string.Empty,
                [.. scores[pupil.Id]],
                [.. lines[pupil.Id]],
                termResult?.Average,
                termResult?.OverallGrade,
                [.. traits[pupil.Id]],
                [.. development[pupil.Id]],
                present[pupil.Id].FirstOrDefault(),
                own.FirstOrDefault(remark => remark.Kind == RemarkKind.ClassTeacher)?.Text,
                own.FirstOrDefault(remark => remark.Kind == RemarkKind.HeadTeacher)?.Text,
                outstanding[pupil.Id].FirstOrDefault());
        }
    }
}
