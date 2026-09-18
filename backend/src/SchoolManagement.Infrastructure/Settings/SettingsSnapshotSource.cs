using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// Real implementation of <see cref="ISettingsSnapshotSource"/> (TASK-0072 stage 3a) — reads every
/// settings group's current state through the SAME repositories every settings handler already used
/// individually for this purpose, just gathered behind one seam. Reads run sequentially rather than
/// in parallel: these are five small, independently-cheap reads against a database connection this
/// same request's ambient transaction already holds, and <c>DbContext</c> is not thread-safe for
/// concurrent queries on one connection.
/// </summary>
internal sealed class SettingsSnapshotSource(
    IGradingBandRepository gradingBandRepository,
    IAssessmentComponentRepository assessmentComponentRepository,
    IResultRulesRepository resultRulesRepository,
    IRatingScaleRepository ratingScaleRepository,
    IDevelopmentDomainRepository developmentDomainRepository)
    : ISettingsSnapshotSource
{
    /// <inheritdoc />
    public async Task<SettingsSnapshotState> LoadAsync(CancellationToken cancellationToken)
    {
        var bands = await gradingBandRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var components = await assessmentComponentRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var resultRules = await resultRulesRepository.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var ratingScales = await ratingScaleRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var developmentDomains = await developmentDomainRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);

        return new SettingsSnapshotState(bands, components, resultRules, ratingScales, developmentDomains);
    }
}
