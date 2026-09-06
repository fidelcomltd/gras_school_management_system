using SchoolManagement.Application.Auth.AdminAccounts;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Abstractions.Auth;

/// <summary>Persistence port for <see cref="AdminAccount"/>.</summary>
public interface IAdminAccountRepository
{
    /// <summary>Whether any admin account exists at all — the bootstrap seam's refuse-twice guard (spec 6.1.6).</summary>
    Task<bool> AnyExistsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new account. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.
    /// </summary>
    Task AddAsync(AdminAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a TRACKED account by email (case-insensitive — email is stored lower-invariant), for a
    /// command that will mutate it (sign-in's lockout counters, a password change).
    /// </summary>
    Task<AdminAccount?> FindTrackedByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED account by id, for a command that will mutate it.</summary>
    Task<AdminAccount?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a read-only projection-friendly account by id, for a query. <c>AsNoTracking</c>.</summary>
    Task<AdminAccount?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Persists ONLY <paramref name="account"/>'s lockout bookkeeping (failed-attempt counter, its
    /// timestamp, and the lockout deadline), on a connection and transaction INDEPENDENT of the
    /// ambient unit of work.
    /// </summary>
    /// <remarks>
    /// WHY THIS BREAKS THE "handlers never call SaveChanges, the unit of work commits on success"
    /// rule, deliberately: <c>SignInCommandHandler</c> must return a FAILED <see cref="Result"/> for
    /// a wrong password, and <c>UnitOfWork.ExecuteAtomicallyAsync</c> rolls back the ambient
    /// transaction whenever the operation's result is a failure (by design — see its remarks). Without
    /// an independent write here, every failed-attempt counter increment would be silently discarded
    /// by that same rollback, and the lockout rule (spec 6.1.11) would never actually trigger. This is
    /// the one narrow, documented exception; nothing else in this card's handlers calls it.
    /// </remarks>
    Task PersistLockoutStateAsync(AdminAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="normalizedEmail"/> is already used by an ACTIVE or SUSPENDED account
    /// (spec 6.1.3: "Unique across active and suspended accounts, case-insensitive" — a deactivated
    /// account's email is reusable, spec 6.1.13). <paramref name="excludingId"/> excludes the account
    /// being edited from its own uniqueness check.
    /// </summary>
    Task<bool> EmailExistsActiveOrSuspendedAsync(
        string normalizedEmail,
        Guid? excludingId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cursor-paginated, filtered list projection (spec 6.1.8, 9.5) — <c>AsNoTracking</c>, projected
    /// straight to <see cref="AdminAccountSummaryDto"/>. Default sort is status ascending (active
    /// first) then staff name ascending; deactivated accounts are excluded unless
    /// <paramref name="status"/> names them explicitly.
    /// </summary>
    /// <param name="status"><see langword="null"/> to use the default (active + suspended only).</param>
    /// <param name="search">Case-insensitive substring match against staff name or email, or <see langword="null"/>.</param>
    /// <param name="cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="pageSize">Already clamped to <see cref="CursorPageRequest.MaxPageSize"/> by the caller.</param>
    /// <param name="cancellationToken">Propagated to the underlying query.</param>
    Task<CursorPage<AdminAccountSummaryDto>> ListAsync(
        AdminAccountStatus? status,
        string? search,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Row-locks (<c>SELECT ... FOR UPDATE</c>) every currently ACTIVE Super Admin account and
    /// returns their ids, to be called inside the ambient transaction BEFORE any write that could
    /// leave zero (spec 6.1.6/6.1.13: "runs inside the transaction with a row lock on the account
    /// table, not as a pre-flight read"). Two concurrent transactions each locking this same set
    /// serialise against each other, which is what makes "two Super Admins suspend each other in the
    /// same minute" (spec 6.1.13) safe even though each targets a DIFFERENT row.
    /// </summary>
    Task<IReadOnlyList<Guid>> LockActiveSuperAdminIdsAsync(CancellationToken cancellationToken);
}
