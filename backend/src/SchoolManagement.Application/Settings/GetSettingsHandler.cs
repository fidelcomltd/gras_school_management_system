using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="GetSettingsQuery"/>.</summary>
internal sealed class GetSettingsQueryHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IPupilRepository pupils,
    IGradingBandRepository gradingBandRepository,
    IAssessmentComponentRepository assessmentComponentRepository,
    IRatingScaleRepository ratingScaleRepository,
    IDevelopmentDomainRepository developmentDomainRepository,
    ISectionRepository sectionRepository,
    ITraitRepository traitRepository,
    ISchoolImageRepository schoolImages)
    : IRequestHandler<GetSettingsQuery, Result<SettingsDto>>
{
    /// <inheritdoc />
    public async Task<Result<SettingsDto>> HandleAsync(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetReadOnlySingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        // TASK-0051: the register is real now — a live count, never null (amendment 2's "no register
        // exists yet" reason no longer holds).
        var issuedCount = await pupils
            .CountByRegistrationNumberPrefixAsync(profile.Abbreviation, cancellationToken)
            .ConfigureAwait(false);

        var bands = await gradingBandRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var components = await assessmentComponentRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var ratingScales = await ratingScaleRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var domains = await developmentDomainRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var sections = await sectionRepository.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var sectionNamesById = sections.ToDictionary(section => section.Id, section => section.Name);

        var traits = await traitRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var traitBlocks = await traitRepository.ListBlocksReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var affectiveScaleId = traitBlocks.FirstOrDefault(block => block.Id == TraitDomain.Affective)?.RatingScaleId ?? Guid.Empty;
        var psychomotorScaleId = traitBlocks.FirstOrDefault(block => block.Id == TraitDomain.Psychomotor)?.RatingScaleId ?? Guid.Empty;

        // TASK-0005b stage B2: the identity group's own logo/signature summaries.
        var logo = await schoolImages.FindCurrentDtoAsync(profile.CurrentLogoGroupId, cancellationToken).ConfigureAwait(false);
        var signature = await schoolImages.FindCurrentDtoAsync(profile.CurrentSignatureGroupId, cancellationToken).ConfigureAwait(false);

        return Result.Success(new SettingsDto(
            SettingsMapper.ToIdentityDto(profile, logo, signature),
            SettingsMapper.ToAbbreviationDto(profile, issuedCount),
            SettingsMapper.ToRegNumberDto(profile),
            SettingsMapper.ToGradingDto(bands, profile.GradingVersionNumber),
            SettingsMapper.ToAssessmentDto(components, profile.AssessmentVersionNumber),
            SettingsMapper.ToRatingScalesDto(ratingScales, profile.RatingScalesVersionNumber),
            SettingsMapper.ToDevelopmentDomainsDto(domains, sectionNamesById, profile.DevelopmentDomainsVersionNumber),
            SettingsMapper.ToTraitsDto(traits, affectiveScaleId, psychomotorScaleId, profile.TraitsVersionNumber)));
    }
}
