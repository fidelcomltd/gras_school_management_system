using SchoolManagement.Domain.Fees;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Abstractions.Fees;

/// <summary>Persistence for spec 6.2.13's fee notice: section lines, per-level amounts per term, and per-pupil outstanding figures.</summary>
public interface IFeeNoticeRepository
{
    /// <summary>A section's lines in print order, tracked.</summary>
    Task<IReadOnlyList<FeeLabel>> ListLabelsTrackedAsync(Guid sectionId, CancellationToken cancellationToken);

    /// <summary>A section's lines in print order, read-only.</summary>
    Task<IReadOnlyList<FeeLabel>> ListLabelsReadOnlyAsync(Guid sectionId, CancellationToken cancellationToken);

    /// <summary>Every amount printed on <paramref name="termId"/>'s sheets for these lines, tracked.</summary>
    Task<IReadOnlyList<FeeAmount>> ListAmountsTrackedAsync(Guid termId, IReadOnlyCollection<Guid> labelIds, CancellationToken cancellationToken);

    /// <summary>Every amount printed on <paramref name="termId"/>'s sheets for these lines, read-only.</summary>
    Task<IReadOnlyList<FeeAmount>> ListAmountsReadOnlyAsync(Guid termId, IReadOnlyCollection<Guid> labelIds, CancellationToken cancellationToken);

    /// <summary>The amounts printed on <paramref name="termId"/>'s sheets for one class level, read-only.</summary>
    Task<IReadOnlyList<FeeAmount>> ListAmountsReadOnlyAsync(Guid termId, Guid classLevelId, CancellationToken cancellationToken);

    /// <summary>Stages a new line. Does NOT commit.</summary>
    Task AddLabelAsync(FeeLabel label, CancellationToken cancellationToken);

    /// <summary>Stages a line's removal together with its amounts in every term. Does NOT commit.</summary>
    Task RemoveLabelAsync(FeeLabel label, CancellationToken cancellationToken);

    /// <summary>Stages a new amount. Does NOT commit.</summary>
    Task AddAmountAsync(FeeAmount amount, CancellationToken cancellationToken);

    /// <summary>Stages an amount's removal (the cell was cleared). Does NOT commit.</summary>
    Task RemoveAmountAsync(FeeAmount amount, CancellationToken cancellationToken);

    /// <summary>The term that starts most recently before <paramref name="term"/>, in any session: "copy from previous term".</summary>
    Task<Term?> FindPreviousTermAsync(Term term, CancellationToken cancellationToken);

    /// <summary>A result set's outstanding figures, tracked.</summary>
    Task<IReadOnlyList<OutstandingFee>> ListOutstandingTrackedAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>A result set's outstanding figures, read-only.</summary>
    Task<IReadOnlyList<OutstandingFee>> ListOutstandingReadOnlyAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>Stages a new figure. Does NOT commit.</summary>
    Task AddOutstandingAsync(OutstandingFee fee, CancellationToken cancellationToken);

    /// <summary>Stages a figure's removal (the cell was cleared). Does NOT commit.</summary>
    Task RemoveOutstandingAsync(OutstandingFee fee, CancellationToken cancellationToken);
}
