using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>Reads one pupil's sheet data for one term: the arm they were enrolled in, its result set, and their rows.</summary>
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
        var pupil = await context.Pupils.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == pupilId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null || pupil is null)
        {
            return null;
        }

        var setId = resultSet.Id;
        var scores = await context.SubjectScores.AsNoTracking()
            .Where(score => score.ResultSetId == setId && score.PupilId == pupilId)
            .Select(score => new SheetScoreRow(score.SubjectId, score.ComponentMarksJson, score.ExamMark, score.ExamAbsent, score.VoidedAt != null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var lines = await context.SubjectResultLines.AsNoTracking()
            .Where(line => line.ResultSetId == setId && line.PupilId == pupilId)
            .Select(line => new SheetLineRow(line.SubjectId, line.SubjectTotal, line.Grade, line.Remark))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var termResult = await context.PupilTermResults.AsNoTracking()
            .FirstOrDefaultAsync(result => result.ResultSetId == setId && result.PupilId == pupilId, cancellationToken).ConfigureAwait(false);
        var traits = await context.TraitRatings.AsNoTracking()
            .Where(rating => rating.ResultSetId == setId && rating.PupilId == pupilId)
            .Select(rating => new SheetRatingRow(rating.TraitId, rating.RatingScalePointId, null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var development = await context.DevelopmentRatings.AsNoTracking()
            .Where(rating => rating.ResultSetId == setId && rating.PupilId == pupilId)
            .Select(rating => new SheetRatingRow(rating.IndicatorId, rating.RatingScalePointId, rating.Comment))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var present = await context.AttendanceEntries.AsNoTracking()
            .Where(entry => entry.ResultSetId == setId && entry.PupilId == pupilId)
            .Select(entry => (int?)entry.TimesPresent)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var remarks = await context.PupilRemarks.AsNoTracking()
            .Where(remark => remark.ResultSetId == setId && remark.PupilId == pupilId)
            .Select(remark => new { remark.Kind, remark.Text })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var previousPublishedAt = resultSet.RevisionNumber > 1
            ? await context.ResultSetSnapshots.AsNoTracking()
                .Where(snapshot => snapshot.ResultSetId == setId && snapshot.RevisionNumber == resultSet.RevisionNumber - 1)
                .Select(snapshot => (DateTimeOffset?)snapshot.PublishedAtUtc)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : null;

        var givenNames = string.Join(' ', new[] { pupil.FirstName, pupil.MiddleName }.Where(name => !string.IsNullOrWhiteSpace(name)));
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
            scores,
            lines,
            termResult?.Average,
            termResult?.OverallGrade,
            traits,
            development,
            present,
            remarks.FirstOrDefault(remark => remark.Kind == RemarkKind.ClassTeacher)?.Text,
            remarks.FirstOrDefault(remark => remark.Kind == RemarkKind.HeadTeacher)?.Text);
    }
}
