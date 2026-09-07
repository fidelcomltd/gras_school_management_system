using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ITermRepository"/>.</summary>
internal sealed class TermRepository(ApplicationDbContext context) : ITermRepository
{
    /// <inheritdoc />
    public Task AddAsync(Term term, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(term);
        cancellationToken.ThrowIfCancellationRequested();

        context.Terms.Add(term);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Term?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Terms.FirstOrDefaultAsync(term => term.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Term?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Terms.AsNoTracking().FirstOrDefaultAsync(term => term.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Term?> FindByOrdinalAsync(Guid sessionId, int ordinal, CancellationToken cancellationToken) =>
        context.Terms
            .AsNoTracking()
            .FirstOrDefaultAsync(term => term.SessionId == sessionId && term.Ordinal == ordinal, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Term>> ListBySessionReadOnlyAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await context.Terms
            .AsNoTracking()
            .Where(term => term.SessionId == sessionId)
            .OrderBy(term => term.Ordinal)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<Term?> FindActiveAsync(CancellationToken cancellationToken) =>
        context.Terms.AsNoTracking().FirstOrDefaultAsync(term => term.State == TermState.Active, cancellationToken);
}
