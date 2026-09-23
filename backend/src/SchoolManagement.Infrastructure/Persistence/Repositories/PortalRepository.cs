using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Portal;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Portal;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

internal sealed class PortalRepository(ApplicationDbContext context) : IPortalRepository
{
    public async Task<Pin?> FindPinForUpdateAsync(string lookupKey, CancellationToken cancellationToken)
    {
        var ids = await context.Database
            .SqlQuery<Guid>($"SELECT id FROM pin WHERE lookup_key = {lookupKey} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids.Count == 0 ? null : await context.Pins.FirstOrDefaultAsync(pin => pin.Id == ids[0], cancellationToken).ConfigureAwait(false);
    }

    public async Task<(SessionState State, string Name)?> FindPinSessionAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var row = await (
                from batch in context.PinBatches.AsNoTracking()
                join session in context.AcademicSessions.AsNoTracking() on batch.SessionId equals session.Id
                where batch.Id == batchId
                select new { session.State, session.Name })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : (row.State, row.Name);
    }

    public async Task<PortalPupil?> FindPupilAsync(string normalizedRegistrationNumber, CancellationToken cancellationToken)
    {
        // Numbers are generated uppercase; only separators need removing before the comparison.
        var byCurrent = context.Pupils.AsNoTracking()
            .Where(pupil => pupil.RegistrationNumber != null
                && pupil.RegistrationNumber.Replace("-", string.Empty).Replace("/", string.Empty).Replace(" ", string.Empty).Replace(".", string.Empty) == normalizedRegistrationNumber)
            .Select(pupil => pupil.Id);
        var byAlias = context.PupilRegNumberHistory.AsNoTracking()
            .Where(alias => alias.OldRegistrationNumber.Replace("-", string.Empty).Replace("/", string.Empty).Replace(" ", string.Empty).Replace(".", string.Empty) == normalizedRegistrationNumber)
            .Select(alias => alias.PupilId);

        var pupilId = await byCurrent.Concat(byAlias).Distinct().Take(2).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (pupilId.Count != 1)
        {
            return null;
        }

        return await FindPupilByIdAsync(pupilId[0], cancellationToken).ConfigureAwait(false);
    }

    public async Task<PortalPupil?> FindPupilByIdAsync(Guid pupilId, CancellationToken cancellationToken)
    {
        var pupil = await context.Pupils.AsNoTracking()
            .Where(candidate => candidate.Id == pupilId && candidate.Status != PupilStatus.Pending)
            .Select(candidate => new { candidate.Id, candidate.Status, candidate.RegistrationNumber, candidate.Surname, candidate.FirstName, candidate.MiddleName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (pupil is null)
        {
            return null;
        }

        var name = string.Join(' ', new[] { pupil.FirstName, pupil.MiddleName, pupil.Surname }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return new PortalPupil(pupil.Id, pupil.Status, pupil.RegistrationNumber ?? string.Empty, name);
    }

    public async Task<IReadOnlyList<DateTimeOffset>> ListFailureTimesByAddressAsync(string sourceAddress, DateTimeOffset since, CancellationToken cancellationToken) =>
        await context.PortalAttempts.AsNoTracking()
            .Where(attempt => attempt.SourceAddress == sourceAddress && attempt.Outcome == PortalAttemptOutcome.NotFound && attempt.AttemptedAtUtc >= since)
            .Select(attempt => attempt.AttemptedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<DateTimeOffset>> ListFailureTimesByRegistrationNumberAsync(string normalizedRegistrationNumber, DateTimeOffset since, CancellationToken cancellationToken) =>
        await context.PortalAttempts.AsNoTracking()
            .Where(attempt => attempt.RegistrationNumber == normalizedRegistrationNumber && attempt.Outcome == PortalAttemptOutcome.NotFound && attempt.AttemptedAtUtc >= since)
            .Select(attempt => attempt.AttemptedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> HasOpenedAsync(Guid pinId, Guid pupilId, CancellationToken cancellationToken) =>
        context.PinUses.AnyAsync(use => use.PinId == pinId && use.PupilId == pupilId, cancellationToken);

    public async Task<IReadOnlySet<Guid>> ListPupilsOpenedSinceAsync(Guid pinId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var ids = await context.PinUses.AsNoTracking()
            .Where(use => use.PinId == pinId && use.OpenedAtUtc >= since)
            .Select(use => use.PupilId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> ListAnnualSessionIdsAsync(Guid pupilId, CancellationToken cancellationToken) =>
        (await context.AnnualResults.AsNoTracking()
            .Where(result => result.PupilId == pupilId)
            .Select(result => result.SessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
        .ToHashSet();

    public async Task<IReadOnlyList<PortalTermRow>> ListTermsAsync(Guid pupilId, CancellationToken cancellationToken)
    {
        var rows = await (
                from enrolment in context.Enrolments.AsNoTracking()
                where enrolment.PupilId == pupilId
                join arm in context.Arms.AsNoTracking() on enrolment.ArmId equals arm.Id
                join session in context.AcademicSessions.AsNoTracking() on arm.SessionId equals session.Id
                join term in context.Terms.AsNoTracking() on session.Id equals term.SessionId
                where enrolment.EffectiveFrom <= term.EndDate && (enrolment.EffectiveTo == null || enrolment.EffectiveTo >= term.StartDate)
                select new
                {
                    SessionId = session.Id,
                    SessionName = session.Name,
                    SessionStart = session.StartDate,
                    TermId = term.Id,
                    TermName = term.Name,
                    term.Ordinal,
                    ArmId = arm.Id,
                    enrolment.EffectiveFrom,
                    ResultSetState = context.ResultSets.Where(set => set.ArmId == arm.Id && set.TermId == term.Id).Select(set => (Domain.Results.ResultSetState?)set.State).FirstOrDefault(),
                    WeeklyPublished = context.WeeklyReports.Any(report =>
                        report.PupilId == pupilId && report.TermId == term.Id && report.State == Domain.Weekly.WeeklyReportState.Published
                        && context.WeeklyReportDays.Any(day => day.WeeklyReportId == report.Id
                            && (day.Behaviour != null || day.Performance != null || day.Dressing != null || day.HomeWork != null
                                || day.Eating != null || day.SymptomsOfIllness != null || day.TeacherComment != null || day.ParentComment != null))),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // A pupil who moved arms mid-term has two overlapping enrolments; the later one is the arm of record.
        return rows
            .GroupBy(row => row.TermId)
            .Select(group => group.OrderByDescending(row => row.EffectiveFrom).First())
            .OrderByDescending(row => row.SessionStart)
            .ThenBy(row => row.Ordinal)
            .Select(row => new PortalTermRow(row.SessionId, row.SessionName, row.TermId, row.TermName, row.Ordinal, row.ResultSetState, row.WeeklyPublished))
            .ToList();
    }

    public async Task<(PinUse Use, Pin Pin)?> FindUseAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var use = await context.PinUses.FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken).ConfigureAwait(false);
        if (use is null)
        {
            return null;
        }

        var pin = await context.Pins.FirstAsync(candidate => candidate.Id == use.PinId, cancellationToken).ConfigureAwait(false);
        return (use, pin);
    }

    public Task AddAttemptAsync(PortalAttempt attempt, CancellationToken cancellationToken)
    {
        context.PortalAttempts.Add(attempt);
        return Task.CompletedTask;
    }

    public Task AddUseAsync(PinUse use, CancellationToken cancellationToken)
    {
        context.PinUses.Add(use);
        return Task.CompletedTask;
    }
}
