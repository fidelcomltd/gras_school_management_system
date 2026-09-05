using SchoolManagement.Domain.Auth;

namespace SchoolManagement.Application.Abstractions.Auth;

/// <summary>Persistence port for <see cref="AdminSession"/>.</summary>
public interface IAdminSessionRepository
{
    /// <summary>Adds a new session. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(AdminSession session, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED session by id, for a command that will mutate it (refresh, sign-out, revoke).</summary>
    Task<AdminSession?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a read-only session by id, for a query. <c>AsNoTracking</c>.</summary>
    Task<AdminSession?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Every currently-active (not revoked, not expired) session for the account, TRACKED, oldest
    /// first — so the caller can revoke the oldest directly once the concurrent-session cap (spec
    /// 6.1.11) is reached.
    /// </summary>
    Task<IReadOnlyList<AdminSession>> FindActiveForAccountAsync(
        Guid adminAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every OTHER currently-active session for the account, tracked, for a password change to revoke
    /// (spec 6.1.11: "revokes every active session for the account except the one performing the change").
    /// </summary>
    Task<IReadOnlyList<AdminSession>> FindTrackedActiveForAccountExceptAsync(
        Guid adminAccountId,
        Guid exceptSessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
