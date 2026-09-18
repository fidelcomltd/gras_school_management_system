using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IDevelopmentDomainRepository"/>. Queries
/// <c>development_domain</c> and <c>development_indicator</c> as two flat, independently-queried
/// tables and groups them in memory — see <see cref="DevelopmentDomain"/>'s remarks for why there is
/// no EF navigation to join instead.
/// </summary>
internal sealed class DevelopmentDomainRepository(ApplicationDbContext context) : IDevelopmentDomainRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DevelopmentDomain>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken)
    {
        var domains = await context.DevelopmentDomains
            .AsNoTracking()
            .OrderBy(domain => domain.SectionId)
            .ThenBy(domain => domain.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var indicators = await context.DevelopmentIndicators
            .AsNoTracking()
            .OrderBy(indicator => indicator.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var indicatorsByDomain = indicators.ToLookup(indicator => indicator.DomainId);

        return domains
            .Select(domain => DevelopmentDomain.Create(
                domain.Id,
                domain.SectionId,
                domain.Name,
                domain.DisplayOrder,
                domain.RatingScaleId,
                domain.AllowsIndicatorComment,
                domain.Status,
                indicatorsByDomain[domain.Id].ToList()))
            .ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Id-stable from the start — TASK-0072 stage 1 had to fix this after the fact for
    /// <c>RatingScaleRepository</c>; this repository applies the lesson directly. A domain or
    /// indicator whose id matches an existing row is UPDATED IN PLACE (id preserved, via
    /// <see cref="DevelopmentDomain.Update"/>/<see cref="DevelopmentIndicator.Update"/> on the TRACKED
    /// entity, so EF issues an <c>UPDATE</c>, never a <c>DELETE</c>+<c>INSERT</c> of the same id); an
    /// existing row whose id is absent from <paramref name="domains"/> (a domain) or its owning
    /// domain's submitted indicators (an indicator) is removed; anything with no matching id is a
    /// genuinely new row. The caller (stage 2b's command handler) has already resolved which
    /// submitted id is which, checked the usage gates before any removal, and rejected an unknown
    /// submitted id — this method trusts <paramref name="domains"/>' ids exactly as given.
    /// </remarks>
    public async Task ReplaceAllAsync(IReadOnlyList<DevelopmentDomain> domains, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domains);

        var existingDomainsById = await context.DevelopmentDomains
            .ToDictionaryAsync(domain => domain.Id, cancellationToken)
            .ConfigureAwait(false);

        var existingIndicatorsByDomain = (await context.DevelopmentIndicators
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToLookup(indicator => indicator.DomainId);

        var submittedDomainIds = domains.Select(domain => domain.Id).ToHashSet();

        foreach (var existingDomain in existingDomainsById.Values)
        {
            if (!submittedDomainIds.Contains(existingDomain.Id))
            {
                context.DevelopmentDomains.Remove(existingDomain);
            }
        }

        foreach (var domain in domains)
        {
            if (existingDomainsById.TryGetValue(domain.Id, out var trackedDomain))
            {
                trackedDomain.Update(
                    domain.SectionId,
                    domain.Name,
                    domain.DisplayOrder,
                    domain.RatingScaleId,
                    domain.AllowsIndicatorComment,
                    domain.Status);
            }
            else
            {
                context.DevelopmentDomains.Add(domain);
            }

            var trackedIndicatorsById = existingIndicatorsByDomain[domain.Id].ToDictionary(indicator => indicator.Id);
            var submittedIndicatorIds = domain.Indicators.Select(indicator => indicator.Id).ToHashSet();

            foreach (var trackedIndicator in trackedIndicatorsById.Values)
            {
                if (!submittedIndicatorIds.Contains(trackedIndicator.Id))
                {
                    context.DevelopmentIndicators.Remove(trackedIndicator);
                }
            }

            foreach (var indicator in domain.Indicators)
            {
                if (trackedIndicatorsById.TryGetValue(indicator.Id, out var trackedIndicator))
                {
                    trackedIndicator.Update(indicator.Name, indicator.DisplayOrder, indicator.Status);
                }
                else
                {
                    context.DevelopmentIndicators.Add(indicator);
                }
            }
        }

        // No SaveChangesAsync — the unit-of-work behaviour commits the transaction.
    }
}
