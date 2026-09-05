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
}
