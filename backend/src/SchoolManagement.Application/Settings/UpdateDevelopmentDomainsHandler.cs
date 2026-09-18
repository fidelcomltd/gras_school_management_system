using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UpdateDevelopmentDomainsCommand"/>.</summary>
/// <remarks>
/// <para>
/// OPTIMISTIC CONCURRENCY, same pattern as <c>UpdateRatingScalesCommandHandler</c>:
/// <see cref="UpdateDevelopmentDomainsCommand.ExpectedVersion"/> is compared against
/// <see cref="SchoolProfile.DevelopmentDomainsVersionNumber"/> BEFORE anything else runs.
/// </para>
/// <para>
/// IDS ARE STABLE ACROSS A SAVE, from the start — <see cref="DevelopmentDomain"/>/
/// <see cref="DevelopmentIndicator"/> never repeat stage 1's blind delete-all/insert-all mistake (see
/// their own remarks). A submitted domain or indicator carrying an <c>id</c> is updated in place, id
/// preserved; one with no <c>id</c> is new. A submitted id that matches no current row is rejected
/// <c>422 settings.developmentdomains.unknown_domain_id</c> / <c>unknown_indicator_id</c>. A submitted
/// <c>sectionId</c>/<c>ratingScaleId</c> that matches no existing row is rejected
/// <c>422 settings.developmentdomains.unknown_section_id</c> / <c>unknown_rating_scale_id</c>.
/// </para>
/// <para>
/// REMOVAL VS ARCHIVE (spec 6.2.13): an EXISTING domain or indicator whose id is absent from the
/// whole submission is being REMOVED — walked through <see cref="IDevelopmentIndicatorUsageGate"/>
/// before anything is written, refused <c>409 settings.developmentdomains.indicator_rated</c> on the
/// first indicator that has ever been rated. An id that IS present, even with a changed
/// <c>status</c>, is never removed and so never gated — archiving a submitted id is always allowed.
/// An omitted whole domain walks every one of ITS indicators through the gate the same way, so the
/// first ever-rated indicator anywhere in it trips the same refusal.
/// </para>
/// <para>
/// THE SNAPSHOT CARRIES EVERY GROUP, NOT ONLY THE ONE THAT CHANGED (spec 6.2.9) — TASK-0072 stage 3a:
/// this handler asks <see cref="ISettingsSnapshotSource"/> for every OTHER group's current state in
/// one call and overrides only the development-domains group with the just-saved, pre-commit
/// <c>domains</c> before handing the result to <see cref="SettingsSnapshotBuilder.Build"/>.
/// </para>
/// </remarks>
internal sealed class UpdateDevelopmentDomainsCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IDevelopmentDomainRepository developmentDomainRepository,
    ISectionRepository sectionRepository,
    IRatingScaleRepository ratingScaleRepository,
    IConfigVersionRepository configVersionRepository,
    IAcademicSessionRepository academicSessionRepository,
    IPublishedResultsGate publishedResultsGate,
    IDevelopmentIndicatorUsageGate developmentIndicatorUsageGate,
    ISettingsSnapshotSource settingsSnapshotSource,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateDevelopmentDomainsCommand, Result<SettingsDevelopmentDomainGroupDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.DevelopmentDomainsVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.developmentdomains.stale_version";

    /// <summary>Stable error code for removing a domain a submission's id does not name.</summary>
    public const string UnknownDomainIdErrorCode = "settings.developmentdomains.unknown_domain_id";

    /// <summary>Stable error code for a submitted indicator id that matches no current indicator of its owning domain.</summary>
    public const string UnknownIndicatorIdErrorCode = "settings.developmentdomains.unknown_indicator_id";

    /// <summary>Stable error code for a submitted <c>sectionId</c> that matches no existing section.</summary>
    public const string UnknownSectionIdErrorCode = "settings.developmentdomains.unknown_section_id";

    /// <summary>Stable error code for a submitted <c>ratingScaleId</c> that matches no existing scale.</summary>
    public const string UnknownRatingScaleIdErrorCode = "settings.developmentdomains.unknown_rating_scale_id";

    /// <summary>Stable error code for removing an indicator that has ever been rated (spec 6.2.13's exact message).</summary>
    public const string IndicatorRatedErrorCode = "settings.developmentdomains.indicator_rated";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<SettingsDevelopmentDomainGroupDto>> HandleAsync(
        UpdateDevelopmentDomainsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var profileIdText = profile.Id.ToString("D", CultureInfo.InvariantCulture);

        if (profile.DevelopmentDomainsVersionNumber != request.ExpectedVersion)
        {
            await auditSink.RecordRejectionAsync(
                "settings.developmentdomains.save_rejected_stale_version",
                SchoolProfileEntityType,
                profileIdText,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expectedVersion"] = request.ExpectedVersion,
                    ["currentVersion"] = profile.DevelopmentDomainsVersionNumber,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsDevelopmentDomainGroupDto>(Error.Conflict(
                StaleVersionErrorCode,
                "The development domains were changed by another administrator while you were " +
                "editing. Reload and make your change again."));
        }

        var reasonCheck = await PublishedResultsReasonGate
            .RequireReasonIfPublishedAsync(request.Reason, academicSessionRepository, publishedResultsGate, cancellationToken)
            .ConfigureAwait(false);
        if (reasonCheck.IsFailure)
        {
            return Result.Failure<SettingsDevelopmentDomainGroupDto>(reasonCheck.Error);
        }

        var structuralValidation = DevelopmentDomainRules.ValidateWholeSet(request.Domains);
        if (structuralValidation.IsFailure)
        {
            return Result.Failure<SettingsDevelopmentDomainGroupDto>(structuralValidation.Error);
        }

        var existingSections = await sectionRepository.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var sectionNamesById = existingSections.ToDictionary(section => section.Id, section => section.Name);

        var existingRatingScales = await ratingScaleRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var existingRatingScaleIds = existingRatingScales.Select(scale => scale.Id).ToHashSet();

        var existingDomains = await developmentDomainRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var existingDomainsById = existingDomains.ToDictionary(domain => domain.Id);
        var submittedDomainIds = request.Domains
            .Where(domain => domain.Id is not null)
            .Select(domain => domain.Id!.Value)
            .ToHashSet();

        // Unknown ids first — a submitted id that matches nothing live, 422, names the offending position.
        for (var domainIndex = 0; domainIndex < request.Domains.Count; domainIndex++)
        {
            var domainInput = request.Domains[domainIndex];

            if (domainInput.Id is Guid submittedDomainId && !existingDomainsById.ContainsKey(submittedDomainId))
            {
                return Result.Failure<SettingsDevelopmentDomainGroupDto>(new DevelopmentDomainValidationError(
                    UnknownDomainIdErrorCode,
                    $"Domain {domainInput.Name} has an id that does not match any existing domain. Reload and try again.",
                    domainIndex,
                    null));
            }

            if (!sectionNamesById.ContainsKey(domainInput.SectionId))
            {
                return Result.Failure<SettingsDevelopmentDomainGroupDto>(new DevelopmentDomainValidationError(
                    UnknownSectionIdErrorCode,
                    $"Domain {domainInput.Name} names a section that does not exist. Reload and try again.",
                    domainIndex,
                    null));
            }

            if (!existingRatingScaleIds.Contains(domainInput.RatingScaleId))
            {
                return Result.Failure<SettingsDevelopmentDomainGroupDto>(new DevelopmentDomainValidationError(
                    UnknownRatingScaleIdErrorCode,
                    $"Domain {domainInput.Name} names a rating scale that does not exist. Reload and try again.",
                    domainIndex,
                    null));
            }

            var existingIndicatorIds = domainInput.Id is Guid ownerId && existingDomainsById.TryGetValue(ownerId, out var ownerDomain)
                ? ownerDomain.Indicators.Select(indicator => indicator.Id).ToHashSet()
                : [];

            for (var indicatorIndex = 0; indicatorIndex < domainInput.Indicators.Count; indicatorIndex++)
            {
                var indicatorInput = domainInput.Indicators[indicatorIndex];
                if (indicatorInput.Id is Guid submittedIndicatorId && !existingIndicatorIds.Contains(submittedIndicatorId))
                {
                    return Result.Failure<SettingsDevelopmentDomainGroupDto>(new DevelopmentDomainValidationError(
                        UnknownIndicatorIdErrorCode,
                        $"Indicator {indicatorInput.Name} has an id that does not match any existing indicator of domain {domainInput.Name}. Reload and try again.",
                        domainIndex,
                        indicatorIndex));
                }
            }
        }

        // Removal vs archive: an existing id absent from the submission is being REMOVED. Archiving a
        // submitted id (present, status changed) is never gated — see this type's remarks.
        var submittedDomainsById = request.Domains
            .Where(domain => domain.Id is not null)
            .ToDictionary(domain => domain.Id!.Value);

        foreach (var existingDomain in existingDomains)
        {
            if (submittedDomainsById.TryGetValue(existingDomain.Id, out var retainedDomainInput))
            {
                var submittedIndicatorIds = retainedDomainInput.Indicators
                    .Where(indicator => indicator.Id is not null)
                    .Select(indicator => indicator.Id!.Value)
                    .ToHashSet();

                foreach (var existingIndicator in existingDomain.Indicators)
                {
                    if (submittedIndicatorIds.Contains(existingIndicator.Id))
                    {
                        continue;
                    }

                    var ratedResult = await RefuseIfRatedAsync(existingIndicator, cancellationToken).ConfigureAwait(false);
                    if (ratedResult is not null)
                    {
                        return ratedResult;
                    }
                }
            }
            else
            {
                // The whole domain is omitted — walk ALL of its indicators; the first rated one trips
                // the refusal, exactly as an indicator omitted individually would.
                foreach (var existingIndicator in existingDomain.Indicators)
                {
                    var ratedResult = await RefuseIfRatedAsync(existingIndicator, cancellationToken).ConfigureAwait(false);
                    if (ratedResult is not null)
                    {
                        return ratedResult;
                    }
                }
            }
        }

        var domains = request.Domains
            .Select(domainInput =>
            {
                var domainId = domainInput.Id ?? Guid.CreateVersion7();
                var indicators = domainInput.Indicators
                    .Select(indicatorInput => DevelopmentIndicator.Create(
                        indicatorInput.Id ?? Guid.CreateVersion7(),
                        domainId,
                        indicatorInput.Name,
                        indicatorInput.DisplayOrder,
                        indicatorInput.Status))
                    .ToList();

                return DevelopmentDomain.Create(
                    domainId,
                    domainInput.SectionId,
                    domainInput.Name,
                    domainInput.DisplayOrder,
                    domainInput.RatingScaleId,
                    domainInput.AllowsIndicatorComment,
                    domainInput.Status,
                    indicators);
            })
            .ToList();

        await developmentDomainRepository.ReplaceAllAsync(domains, cancellationToken).ConfigureAwait(false);

        profile.IncrementDevelopmentDomainsVersion();

        var snapshotState = await settingsSnapshotSource.LoadAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, snapshotState with { DevelopmentDomains = domains }),
            ConfigVersionGroup.DevelopmentDomains,
            currentUser.UserId,
            reason: reasonCheck.Value,
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.developmentdomains.updated",
            SchoolProfileEntityType,
            profileIdText,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["domainCount"] = domains.Count },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SettingsMapper.ToDevelopmentDomainsDto(domains, sectionNamesById, profile.DevelopmentDomainsVersionNumber));
    }

    /// <summary>
    /// Returns a failed <see cref="Result"/> carrying spec 6.2.13's exact refusal message when
    /// <paramref name="indicator"/> has ever been rated, recording the rejection first; otherwise
    /// <see langword="null"/>, meaning the caller may proceed.
    /// </summary>
    private async Task<Result<SettingsDevelopmentDomainGroupDto>?> RefuseIfRatedAsync(
        DevelopmentIndicator indicator,
        CancellationToken cancellationToken)
    {
        var everRated = await developmentIndicatorUsageGate
            .HasEverBeenRatedAsync(indicator.Id, cancellationToken)
            .ConfigureAwait(false);

        if (!everRated)
        {
            return null;
        }

        await auditSink.RecordRejectionAsync(
            "settings.developmentdomains.save_rejected_indicator_rated",
            "development_indicator",
            indicator.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["indicatorName"] = indicator.Name },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Failure<SettingsDevelopmentDomainGroupDto>(Error.Conflict(
            IndicatorRatedErrorCode,
            $"Ratings have already been entered for {indicator.Name} this term. Archive the " +
            "indicator instead, which keeps it on this term's sheets and removes it from next term."));
    }
}
