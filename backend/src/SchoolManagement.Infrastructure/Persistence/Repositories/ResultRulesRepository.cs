using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IResultRulesRepository"/>.</summary>
internal sealed class ResultRulesRepository(ApplicationDbContext context) : IResultRulesRepository
{
    /// <inheritdoc />
    public Task<ResultRules> GetTrackedSingletonAsync(CancellationToken cancellationToken) =>
        context.ResultRules.FirstAsync(resultRules => resultRules.Id == ResultRules.SingletonId, cancellationToken);

    /// <inheritdoc />
    public Task<ResultRules> GetReadOnlySingletonAsync(CancellationToken cancellationToken) =>
        context.ResultRules
            .AsNoTracking()
            .FirstAsync(resultRules => resultRules.Id == ResultRules.SingletonId, cancellationToken);
}
