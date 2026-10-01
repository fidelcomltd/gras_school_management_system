using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Fees;
using SchoolManagement.Domain.Fees;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IFeeNoticeRepository"/>.</summary>
internal sealed class FeeNoticeRepository(ApplicationDbContext context) : IFeeNoticeRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FeeLabel>> ListLabelsTrackedAsync(Guid sectionId, CancellationToken cancellationToken) =>
        await context.FeeLabels.Where(label => label.SectionId == sectionId).OrderBy(label => label.DisplayOrder)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<FeeLabel>> ListLabelsReadOnlyAsync(Guid sectionId, CancellationToken cancellationToken) =>
        await context.FeeLabels.AsNoTracking().Where(label => label.SectionId == sectionId).OrderBy(label => label.DisplayOrder)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<FeeAmount>> ListAmountsTrackedAsync(Guid termId, IReadOnlyCollection<Guid> labelIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(labelIds);
        var ids = labelIds.ToArray();
        return await context.FeeAmounts.Where(amount => amount.TermId == termId && ids.Contains(amount.FeeLabelId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FeeAmount>> ListAmountsReadOnlyAsync(Guid termId, IReadOnlyCollection<Guid> labelIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(labelIds);
        var ids = labelIds.ToArray();
        return await context.FeeAmounts.AsNoTracking().Where(amount => amount.TermId == termId && ids.Contains(amount.FeeLabelId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FeeAmount>> ListAmountsReadOnlyAsync(Guid termId, Guid classLevelId, CancellationToken cancellationToken) =>
        await context.FeeAmounts.AsNoTracking().Where(amount => amount.TermId == termId && amount.ClassLevelId == classLevelId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddLabelAsync(FeeLabel label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);
        cancellationToken.ThrowIfCancellationRequested();
        context.FeeLabels.Add(label);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RemoveLabelAsync(FeeLabel label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);

        // Every term's amounts go too: loaded and removed explicitly rather than trusting the cascade to reach tracked rows.
        var amounts = await context.FeeAmounts.Where(amount => amount.FeeLabelId == label.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        context.FeeAmounts.RemoveRange(amounts);
        context.FeeLabels.Remove(label);
    }

    /// <inheritdoc />
    public Task AddAmountAsync(FeeAmount amount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amount);
        cancellationToken.ThrowIfCancellationRequested();
        context.FeeAmounts.Add(amount);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAmountAsync(FeeAmount amount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amount);
        cancellationToken.ThrowIfCancellationRequested();
        context.FeeAmounts.Remove(amount);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Term?> FindPreviousTermAsync(Term term, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(term);
        return context.Terms.AsNoTracking()
            .Where(candidate => candidate.StartDate < term.StartDate)
            .OrderByDescending(candidate => candidate.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutstandingFee>> ListOutstandingTrackedAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.OutstandingFees.Where(fee => fee.ResultSetId == resultSetId).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutstandingFee>> ListOutstandingReadOnlyAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.OutstandingFees.AsNoTracking().Where(fee => fee.ResultSetId == resultSetId).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddOutstandingAsync(OutstandingFee fee, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fee);
        cancellationToken.ThrowIfCancellationRequested();
        context.OutstandingFees.Add(fee);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveOutstandingAsync(OutstandingFee fee, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fee);
        cancellationToken.ThrowIfCancellationRequested();
        context.OutstandingFees.Remove(fee);
        return Task.CompletedTask;
    }
}
