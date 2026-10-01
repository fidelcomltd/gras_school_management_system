using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Fees;
using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Domain.Subjects;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Reports;

/// <summary>The reports' projections: no tracking, no entities leave this class.</summary>
internal sealed class ReportReader(ApplicationDbContext context) : IReportReader
{
    public async Task<IReadOnlyList<ReportArm>> ListArmsAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await ArmsAsync(arm => arm.SessionId == sessionId, cancellationToken).ConfigureAwait(false);

    // The one arm-with-level projection, by level progression then label.
    private async Task<List<ReportArm>> ArmsAsync(System.Linq.Expressions.Expression<Func<Arm, bool>> which, CancellationToken cancellationToken)
    {
        var rows = await (
                from arm in context.Arms.AsNoTracking().Where(which)
                join level in context.ClassLevels.AsNoTracking() on arm.ClassLevelId equals level.Id
                orderby level.ProgressionOrder, arm.Label
                select new { arm.Id, arm.Label, LevelId = level.Id, LevelName = level.Name, level.ProgressionOrder, level.SectionId, arm.Capacity, level.NextLevelId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(row => new ReportArm(
            row.Id, ArmDisplayName.Compose(row.LevelName, row.Label), row.LevelId, row.LevelName, row.ProgressionOrder, row.SectionId, row.Capacity, row.NextLevelId));
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

    public async Task<IReadOnlyList<ReportSubject>> ListMappedSubjectsAsync(
        Guid termId, IReadOnlyCollection<Guid> levelIds, CancellationToken cancellationToken) =>
        await (
                from mapping in context.SubjectMappings.AsNoTracking()
                join subject in context.Subjects.AsNoTracking() on mapping.SubjectId equals subject.Id
                where mapping.TermId == termId && levelIds.Contains(mapping.ClassLevelId) && mapping.Status == SubjectMappingStatus.Active
                orderby mapping.DisplayOrder, subject.Name
                select new ReportSubject(mapping.ClassLevelId, subject.Id, subject.Name, mapping.DisplayOrder))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportGradeBand>> ListGradeBandsAsync(CancellationToken cancellationToken) =>
        await context.GradingBands.AsNoTracking()
            .OrderBy(band => band.DisplayOrder)
            .Select(band => new ReportGradeBand(band.GradeLetter, band.LowerBound, band.UpperBound, band.Remark))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportIndicator>> ListIndicatorsAsync(Guid sectionId, CancellationToken cancellationToken) =>
        await (
                from indicator in context.DevelopmentIndicators.AsNoTracking()
                join domain in context.DevelopmentDomains.AsNoTracking() on indicator.DomainId equals domain.Id
                where domain.SectionId == sectionId
                    && domain.Status == DevelopmentDomainStatus.Active && indicator.Status == DevelopmentIndicatorStatus.Active
                orderby domain.DisplayOrder, indicator.DisplayOrder
                select new ReportIndicator(indicator.Id, indicator.Name, domain.Id, domain.Name, domain.RatingScaleId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportRatingPoint>> ListRatingPointsAsync(IReadOnlyCollection<Guid> ratingScaleIds, CancellationToken cancellationToken) =>
        await context.RatingScalePoints.AsNoTracking()
            .Where(point => ratingScaleIds.Contains(point.RatingScaleId))
            .OrderBy(point => point.PointOrder)
            .Select(point => new ReportRatingPoint(point.Id, point.RatingScaleId, point.PointLabel, point.PointOrder))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportDevelopmentRating>> ListDevelopmentRatingsAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.DevelopmentRatings.AsNoTracking()
            .Where(rating => rating.ResultSetId == resultSetId)
            .Select(rating => new ReportDevelopmentRating(rating.PupilId, rating.IndicatorId, rating.RatingScalePointId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportFeeLine>> ListFeeLinesAsync(Guid termId, IReadOnlyCollection<Guid> levelIds, CancellationToken cancellationToken)
    {
        var lines = await (
                from level in context.ClassLevels.AsNoTracking()
                join label in context.FeeLabels.AsNoTracking() on level.SectionId equals label.SectionId
                where levelIds.Contains(level.Id) && label.Kind == FeeLabelKind.Amount
                select new { LevelId = level.Id, label.Id, label.Label, label.DisplayOrder })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var amounts = await context.FeeAmounts.AsNoTracking()
            .Where(amount => amount.TermId == termId && levelIds.Contains(amount.ClassLevelId))
            .ToDictionaryAsync(amount => (amount.FeeLabelId, amount.ClassLevelId), amount => amount.Amount, cancellationToken)
            .ConfigureAwait(false);
        return lines
            .OrderBy(line => line.DisplayOrder)
            .Select(line => new ReportFeeLine(line.LevelId, line.Label, line.DisplayOrder, amounts.TryGetValue((line.Id, line.LevelId), out var amount) ? amount : null))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> ListRosterAsync(Guid armId, Guid termId, CancellationToken cancellationToken) =>
        await (
                from enrolment in context.Enrolments.AsNoTracking()
                join pupil in context.Pupils.AsNoTracking() on enrolment.PupilId equals pupil.Id
                join term in context.Terms.AsNoTracking() on termId equals term.Id
                where enrolment.ArmId == armId
                    && enrolment.EffectiveFrom <= term.EndDate && (enrolment.EffectiveTo == null || enrolment.EffectiveTo >= term.StartDate)
                select pupil.Id)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, (int Pupils, long Total)>> SumOutstandingAsync(
        IReadOnlyCollection<Guid> resultSetIds, CancellationToken cancellationToken)
    {
        var rows = await context.OutstandingFees.AsNoTracking()
            .Where(fee => resultSetIds.Contains(fee.ResultSetId))
            .GroupBy(fee => fee.ResultSetId)
            .Select(group => new { group.Key, Pupils = group.Count(), Total = group.Sum(fee => (long)fee.Amount) })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(row => row.Key, row => (row.Pupils, row.Total));
    }

    public Task<ReportSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        context.AcademicSessions.AsNoTracking()
            .Where(session => session.Id == sessionId)
            .Select(session => new ReportSession(session.Id, session.Name, session.StartDate))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ReportSession>> FindSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken) =>
        await context.AcademicSessions.AsNoTracking()
            .Where(session => sessionIds.Contains(session.Id))
            .ToDictionaryAsync(session => session.Id, session => new ReportSession(session.Id, session.Name, session.StartDate), cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportAnnualResult>> ListAnnualResultsAsync(Guid sessionId, CancellationToken cancellationToken) =>
        (await context.AnnualResults.AsNoTracking().Where(result => result.SessionId == sessionId).ToListAsync(cancellationToken).ConfigureAwait(false))
            .ConvertAll(Annual);

    public async Task<IReadOnlyList<(Guid SessionId, ReportAnnualResult Annual)>> ListPupilAnnualAsync(Guid pupilId, CancellationToken cancellationToken) =>
        (await context.AnnualResults.AsNoTracking().Where(result => result.PupilId == pupilId).ToListAsync(cancellationToken).ConfigureAwait(false))
            .ConvertAll(result => (result.SessionId, Annual(result)));

    public async Task<IReadOnlyList<ReportPromotionDecision>> ListPromotionDecisionsAsync(Guid sourceSessionId, CancellationToken cancellationToken) =>
        await (
                from batch in context.PromotionBatches.AsNoTracking()
                join decision in context.Set<PromotionDecision>().AsNoTracking() on batch.Id equals decision.BatchId
                where batch.SourceSessionId == sourceSessionId && batch.State == PromotionBatchState.Committed
                select new ReportPromotionDecision(
                    decision.PupilId, decision.FromArmId, decision.ProposedOutcome, decision.Outcome, decision.TargetArmId, batch.TargetSessionId, decision.Reason))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, ReportPromotionDecision>> ListPupilDecisionsAsync(Guid pupilId, CancellationToken cancellationToken) =>
        await (
                from batch in context.PromotionBatches.AsNoTracking()
                join decision in context.Set<PromotionDecision>().AsNoTracking() on batch.Id equals decision.BatchId
                where decision.PupilId == pupilId && batch.State == PromotionBatchState.Committed
                select new
                {
                    batch.SourceSessionId,
                    Decision = new ReportPromotionDecision(
                        decision.PupilId, decision.FromArmId, decision.ProposedOutcome, decision.Outcome, decision.TargetArmId, batch.TargetSessionId, decision.Reason),
                })
            .ToDictionaryAsync(row => row.SourceSessionId, row => row.Decision, cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<Guid> CoreSubjectIds, int PassMark)> GetCoreRulesAsync(CancellationToken cancellationToken)
    {
        var rules = await context.ResultRules.AsNoTracking().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return rules is null ? ([], ResultRules.DefaultPassMark) : (rules.CoreSubjectIds, rules.PassMark);
    }

    public async Task<IReadOnlyList<ReportPupilTerm>> ListPupilTermsAsync(Guid pupilId, CancellationToken cancellationToken) =>
        await (
                from result in context.PupilTermResults.AsNoTracking()
                join set in context.ResultSets.AsNoTracking() on result.ResultSetId equals set.Id
                join term in context.Terms.AsNoTracking() on set.TermId equals term.Id
                where result.PupilId == pupilId
                select new ReportPupilTerm(
                    term.SessionId, term.Ordinal, term.Name, set.ArmId, result.Average, result.OverallGrade,
                    result.ArmPosition, result.ArmPositionTied, result.LevelPosition, result.LevelPositionTied, set.State))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, ReportArm>> FindArmsAsync(IReadOnlyCollection<Guid> armIds, CancellationToken cancellationToken) =>
        (await ArmsAsync(arm => armIds.Contains(arm.Id), cancellationToken).ConfigureAwait(false)).ToDictionary(arm => arm.ArmId);

    private static ReportAnnualResult Annual(AnnualResult result) => new(
        result.ArmId,
        result.PupilId,
        [result.FirstTermAverage, result.SecondTermAverage, result.ThirdTermAverage],
        result.CumulativeAverage,
        result.CumulativeGrade,
        result.AnnualPosition,
        result.AnnualPositionTied,
        result.ProposedOutcome,
        result.SubjectsJson);

    public async Task<IReadOnlyList<ReportRegisterPupil>> ListRegisterAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var rows = await (
                from enrolment in context.Enrolments.AsNoTracking()
                join arm in context.Arms.AsNoTracking() on enrolment.ArmId equals arm.Id
                join pupil in context.Pupils.AsNoTracking() on enrolment.PupilId equals pupil.Id
                join session in context.AcademicSessions.AsNoTracking() on arm.SessionId equals session.Id
                where arm.SessionId == sessionId
                select new
                {
                    pupil.Id,
                    pupil.Surname,
                    pupil.FirstName,
                    pupil.MiddleName,
                    pupil.RegistrationNumber,
                    pupil.Sex,
                    pupil.DateOfBirth,
                    pupil.Status,
                    enrolment.ArmId,
                    enrolment.EffectiveFrom,
                    enrolment.EffectiveTo,
                    SessionEnd = session.EndDate,
                })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.GroupBy(row => row.Id)
            .Select(group => group.OrderByDescending(row => row.EffectiveFrom).First())
            .Select(row => new ReportRegisterPupil(
                row.Id, row.Surname, string.Join(' ', new[] { row.FirstName, row.MiddleName }.Where(name => !string.IsNullOrWhiteSpace(name))),
                row.RegistrationNumber, row.Sex, row.DateOfBirth,
                row.EffectiveTo is null || row.EffectiveTo >= row.SessionEnd ? PupilStatus.Active : row.Status,
                row.ArmId))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, (string Name, int Order)>> FindLevelsAsync(IReadOnlyCollection<Guid> levelIds, CancellationToken cancellationToken) =>
        await context.ClassLevels.AsNoTracking()
            .Where(level => levelIds.Contains(level.Id))
            .ToDictionaryAsync(level => level.Id, level => (level.Name, level.ProgressionOrder), cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<ReportPinBatch> Batches, IReadOnlyList<ReportPin> Pins, IReadOnlyList<ReportPinUse> Uses)> ListPinUsageAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var batches = await context.PinBatches.AsNoTracking()
            .Where(batch => batch.SessionId == sessionId)
            .OrderBy(batch => batch.GeneratedAtUtc)
            .Select(batch => new ReportPinBatch(batch.Id, batch.Name))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var batchIds = batches.Select(batch => batch.BatchId).ToList();
        var pins = await context.Pins.AsNoTracking()
            .Where(pin => batchIds.Contains(pin.BatchId))
            .Select(pin => new ReportPin(pin.Id, pin.BatchId, pin.State, pin.UseCount))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var uses = await (
                from use in context.PinUses.AsNoTracking()
                join pin in context.Pins.AsNoTracking() on use.PinId equals pin.Id
                where batchIds.Contains(pin.BatchId)
                select new ReportPinUse(use.PinId, use.PupilId, use.OpenedAtUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return (batches, pins, uses);
    }

    public async Task<IReadOnlyList<ReportConfigVersion>> ListConfigVersionsAsync(CancellationToken cancellationToken) =>
        await context.ConfigVersions.AsNoTracking()
            .OrderBy(version => version.VersionNumber)
            .Select(version => new ReportConfigVersion(
                version.VersionNumber, version.ChangedGroup, version.SnapshotJson, version.ActorAdminId, version.Reason, version.CreatedAtUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, string>> FindAdminNamesAsync(IReadOnlyCollection<Guid> adminIds, CancellationToken cancellationToken) =>
        await context.AdminAccounts.AsNoTracking()
            .Where(admin => adminIds.Contains(admin.Id))
            .ToDictionaryAsync(admin => admin.Id, admin => admin.StaffName, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, string>> FindSubjectNamesAsync(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken) =>
        await context.Subjects.AsNoTracking()
            .Where(subject => subjectIds.Contains(subject.Id))
            .ToDictionaryAsync(subject => subject.Id, subject => subject.Name, cancellationToken).ConfigureAwait(false);
}
