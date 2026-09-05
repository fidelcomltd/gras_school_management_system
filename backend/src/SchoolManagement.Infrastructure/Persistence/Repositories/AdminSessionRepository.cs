using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Domain.Auth;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAdminSessionRepository"/>.</summary>
internal sealed class AdminSessionRepository(ApplicationDbContext context) : IAdminSessionRepository
{
    /// <inheritdoc />
    public Task AddAsync(AdminSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        context.AdminSessions.Add(session);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AdminSession?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AdminSessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<AdminSession?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AdminSessions.AsNoTracking().FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminSession>> FindActiveForAccountAsync(
        Guid adminAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessions = await context.AdminSessions
            .Where(session => session.AdminAccountId == adminAccountId)
            .Where(session => session.RevokedAtUtc == null)
            .Where(session => session.IdleExpiresAtUtc > now && session.AbsoluteExpiresAtUtc > now)
            .OrderBy(session => session.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sessions;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminSession>> FindTrackedActiveForAccountExceptAsync(
        Guid adminAccountId,
        Guid exceptSessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessions = await context.AdminSessions
            .Where(session => session.AdminAccountId == adminAccountId)
            .Where(session => session.Id != exceptSessionId)
            .Where(session => session.RevokedAtUtc == null)
            .Where(session => session.IdleExpiresAtUtc > now && session.AbsoluteExpiresAtUtc > now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sessions;
    }
}
