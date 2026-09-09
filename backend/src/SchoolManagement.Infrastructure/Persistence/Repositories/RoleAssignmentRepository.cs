using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IRoleAssignmentRepository"/>.</summary>
internal sealed class RoleAssignmentRepository(ApplicationDbContext context) : IRoleAssignmentRepository
{
    /// <inheritdoc />
    public Task AddAsync(RoleAssignment assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        cancellationToken.ThrowIfCancellationRequested();

        context.RoleAssignments.Add(assignment);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RoleAssignment?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.RoleAssignments.FirstOrDefaultAsync(assignment => assignment.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleAssignment>> ListForAccountReadOnlyAsync(
        Guid adminAccountId, CancellationToken cancellationToken) =>
        await context.RoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.AdminAccountId == adminAccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleAssignment>> ListActiveForAccountReadOnlyAsync(
        Guid adminAccountId, CancellationToken cancellationToken) =>
        await context.RoleAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.AdminAccountId == adminAccountId &&
                assignment.Status == RoleAssignmentStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleAssignment>> ListActiveForAccountTrackedAsync(
        Guid adminAccountId, CancellationToken cancellationToken) =>
        await context.RoleAssignments
            .Where(assignment =>
                assignment.AdminAccountId == adminAccountId &&
                assignment.Status == RoleAssignmentStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<bool> ExistsForRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
        context.RoleAssignments.AsNoTracking().AnyAsync(assignment => assignment.RoleId == roleId, cancellationToken);
}
