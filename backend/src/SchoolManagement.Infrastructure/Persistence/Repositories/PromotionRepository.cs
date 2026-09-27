using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Promotion;
using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IPromotionRepository"/>.</summary>
internal sealed class PromotionRepository(ApplicationDbContext context) : IPromotionRepository
{
    /// <inheritdoc />
    public Task<PromotionBatch?> FindCommittedForSessionAsync(Guid sourceSessionId, CancellationToken cancellationToken) =>
        context.PromotionBatches.AsNoTracking().Include(batch => batch.Decisions)
            .FirstOrDefaultAsync(batch => batch.SourceSessionId == sourceSessionId && batch.State == PromotionBatchState.Committed, cancellationToken);

    /// <inheritdoc />
    public Task<PromotionBatch?> FindTrackedWithDecisionsAsync(Guid batchId, CancellationToken cancellationToken) =>
        context.PromotionBatches.Include(batch => batch.Decisions).FirstOrDefaultAsync(batch => batch.Id == batchId, cancellationToken);

    /// <inheritdoc />
    public Task AddAsync(PromotionBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        context.PromotionBatches.Add(batch);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnnualResult>> ListAnnualResultsAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await context.AnnualResults.AsNoTracking().Where(result => result.SessionId == sessionId).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<bool> AnyMarkInSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        (from score in context.SubjectScores.AsNoTracking()
         join term in context.Terms.AsNoTracking() on score.TermId equals term.Id
         where term.SessionId == sessionId
         select score.Id).AnyAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyPinUseInSessionAsync(Guid sessionId, IReadOnlyCollection<Guid> pupilIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pupilIds);
        var ids = pupilIds.ToArray();
        return (from use in context.PinUses.AsNoTracking()
                join pin in context.Pins.AsNoTracking() on use.PinId equals pin.Id
                join batch in context.PinBatches.AsNoTracking() on pin.BatchId equals batch.Id
                where batch.SessionId == sessionId && ids.Contains(use.PupilId)
                select use.Id).AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveEnrolmentAsync(Guid enrolmentId, CancellationToken cancellationToken) =>
        context.Enrolments.Where(row => row.Id == enrolmentId).ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public Task<AcademicSession?> FindNextSessionAsync(DateOnly startDate, CancellationToken cancellationToken) =>
        context.AcademicSessions.AsNoTracking()
            .Where(session => session.StartDate > startDate)
            .OrderBy(session => session.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Pupil>> ListInactiveEnrolledInSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var ids = from enrolment in context.Enrolments.AsNoTracking()
                  join arm in context.Arms.AsNoTracking() on enrolment.ArmId equals arm.Id
                  where arm.SessionId == sessionId
                  select enrolment.PupilId;
        return await context.Pupils.AsNoTracking()
            .Where(pupil => pupil.Status != PupilStatus.Active && pupil.Status != PupilStatus.Pending && ids.Contains(pupil.Id))
            .OrderBy(pupil => pupil.Surname).ThenBy(pupil => pupil.FirstName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
