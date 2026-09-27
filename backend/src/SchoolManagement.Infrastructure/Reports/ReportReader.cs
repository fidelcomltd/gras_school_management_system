using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Subjects;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Reports;

/// <summary>The reports' projections: no tracking, no entities leave this class.</summary>
internal sealed class ReportReader(ApplicationDbContext context) : IReportReader
{
    public async Task<IReadOnlyList<ReportArm>> ListArmsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var rows = await (
                from arm in context.Arms.AsNoTracking()
                join level in context.ClassLevels.AsNoTracking() on arm.ClassLevelId equals level.Id
                where arm.SessionId == sessionId
                orderby level.ProgressionOrder, arm.Label
                select new { arm.Id, arm.Label, LevelId = level.Id, LevelName = level.Name, level.ProgressionOrder, level.SectionId, arm.Capacity })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(row => new ReportArm(
            row.Id, ArmDisplayName.Compose(row.LevelName, row.Label), row.LevelId, row.LevelName, row.ProgressionOrder, row.SectionId, row.Capacity));
    }

    public Task<ReportTerm?> FindTermAsync(Guid termId, CancellationToken cancellationToken) =>
        (from term in context.Terms.AsNoTracking()
         join session in context.AcademicSessions.AsNoTracking() on term.SessionId equals session.Id
         where term.Id == termId
         select new ReportTerm(term.Id, term.Name, term.Ordinal, session.Id, session.Name))
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ReportResultSet>> ListResultSetsAsync(Guid termId, CancellationToken cancellationToken) =>
        await context.ResultSets.AsNoTracking()
            .Where(set => set.TermId == termId)
            .Select(set => new ReportResultSet(set.Id, set.ArmId, set.State, set.ComputedAtUtc != null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportTermResult>> ListTermResultsAsync(IReadOnlyCollection<Guid> resultSetIds, CancellationToken cancellationToken) =>
        await context.PupilTermResults.AsNoTracking()
            .Where(result => resultSetIds.Contains(result.ResultSetId))
            .Select(result => new ReportTermResult(
                result.ResultSetId, result.PupilId, result.TotalObtained, result.Average, result.OverallGrade,
                result.ArmPosition, result.ArmPositionTied, result.LevelPosition, result.LevelPositionTied))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportSubjectLine>> ListSubjectLinesAsync(IReadOnlyCollection<Guid> resultSetIds, CancellationToken cancellationToken) =>
        await context.SubjectResultLines.AsNoTracking()
            .Where(line => resultSetIds.Contains(line.ResultSetId))
            .Select(line => new ReportSubjectLine(line.ResultSetId, line.PupilId, line.SubjectId, line.CaTotal, line.ExamMark, line.SubjectTotal, line.Grade, line.IsPass))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, ReportPupil>> FindPupilsAsync(IReadOnlyCollection<Guid> pupilIds, CancellationToken cancellationToken)
    {
        var rows = await context.Pupils.AsNoTracking()
            .Where(pupil => pupilIds.Contains(pupil.Id))
            .Select(pupil => new { pupil.Id, pupil.Surname, pupil.FirstName, pupil.MiddleName, pupil.RegistrationNumber })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(
            row => row.Id,
            row => new ReportPupil(row.Id, row.Surname, string.Join(' ', new[] { row.FirstName, row.MiddleName }.Where(name => !string.IsNullOrWhiteSpace(name))), row.RegistrationNumber));
    }

    public async Task<(IReadOnlyList<ReportSubject> Mapped, IReadOnlyDictionary<Guid, string> Names)> ListSubjectsAsync(
        Guid termId, IReadOnlyCollection<Guid> levelIds, CancellationToken cancellationToken)
    {
        var mapped = await (
                from mapping in context.SubjectMappings.AsNoTracking()
                join subject in context.Subjects.AsNoTracking() on mapping.SubjectId equals subject.Id
                where mapping.TermId == termId && levelIds.Contains(mapping.ClassLevelId) && mapping.Status == SubjectMappingStatus.Active
                orderby mapping.DisplayOrder, subject.Name
                select new ReportSubject(mapping.ClassLevelId, subject.Id, subject.Name, mapping.DisplayOrder))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var names = await context.Subjects.AsNoTracking()
            .ToDictionaryAsync(subject => subject.Id, subject => subject.Name, cancellationToken).ConfigureAwait(false);
        return (mapped, names);
    }
}
