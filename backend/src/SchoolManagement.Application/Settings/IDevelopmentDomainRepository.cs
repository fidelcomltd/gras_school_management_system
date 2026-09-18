using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Persistence port for development domains (spec 6.2.13). Implemented in Infrastructure.</summary>
/// <remarks>
/// <c>PUT /settings/development-domains</c> (stage 2b) replaces the WHOLE set, id-stable — same
/// convention as <see cref="IRatingScaleRepository"/> since TASK-0072 stage 1's review fix: a domain's
/// or indicator's id survives an unrelated edit to the same save, because a future rating (Phase 3)
/// references an indicator BY id from day one here, unlike rating scales which only grew that
/// requirement after their first save.
/// </remarks>
public interface IDevelopmentDomainRepository
{
    /// <summary>
    /// Loads every domain, read-only, with its indicators attached and ordered by
    /// <see cref="DevelopmentIndicator.DisplayOrder"/>. Domains are ordered by
    /// <see cref="DevelopmentDomain.SectionId"/> then <see cref="DevelopmentDomain.DisplayOrder"/>.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<IReadOnlyList<DevelopmentDomain>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces every existing domain and indicator with <paramref name="domains"/>, diffed by id: a
    /// matched id is updated in place (id preserved), an existing id absent from the submitted set is
    /// removed, and anything with no matching id is inserted new. Does NOT commit — the unit-of-work
    /// behaviour does that when the command returns a successful result.
    /// </summary>
    /// <param name="domains">The whole new set, already validated by the caller.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task ReplaceAllAsync(IReadOnlyList<DevelopmentDomain> domains, CancellationToken cancellationToken);
}
