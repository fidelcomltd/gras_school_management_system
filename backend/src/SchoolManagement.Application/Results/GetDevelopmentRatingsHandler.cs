using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>
/// The arm's section, its ACTIVE development domains (ordered, what the grid shows) and EVERY domain
/// of that section regardless of status (what <c>SaveDevelopmentRatingsHandler</c> needs to tell an
/// "unknown" indicator id from an "archived" one) — <see cref="GetDevelopmentRatingsHandler.ResolveSectionAsync"/>'s
/// result, TASK-0083 stage 2.
/// </summary>
internal sealed record DevelopmentRatingSectionContext(
    Section Section,
    IReadOnlyList<DevelopmentDomain> ActiveDomainsOrdered,
    IReadOnlyList<DevelopmentDomain> AllDomainsOfSection);

/// <summary>Handles <see cref="GetDevelopmentRatingsQuery"/>.</summary>
internal sealed class GetDevelopmentRatingsHandler(
    IArmRepository arms,
    ITermRepository terms,
    IClassLevelRepository classLevels,
    ISectionRepository sections,
    IEnrolmentRepository enrolments,
    IDevelopmentDomainRepository developmentDomains,
    IRatingScaleRepository ratingScales,
    IResultSetRepository resultSets,
    IDevelopmentRatingRepository developmentRatings)
    : IRequestHandler<GetDevelopmentRatingsQuery, Result<DevelopmentRatingSheetDto>>
{
    /// <summary>The arm's section has no active development domain (TASK-0083 ruling R1).</summary>
    public const string SectionNotRatedErrorCode = "development_ratings.section_not_rated";

    /// <inheritdoc />
    public async Task<Result<DevelopmentRatingSheetDto>> HandleAsync(GetDevelopmentRatingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<DevelopmentRatingSheetDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<DevelopmentRatingSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var sectionCheck = await ResolveSectionAsync(arm.ClassLevelId, classLevels, sections, developmentDomains, cancellationToken)
            .ConfigureAwait(false);
        if (sectionCheck.IsFailure)
        {
            return Result.Failure<DevelopmentRatingSheetDto>(sectionCheck.Error);
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var scales = await ratingScales.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var resultSet = await resultSets.FindReadOnlyByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<DevelopmentRatingSnapshot> ratings = resultSet is null
            ? []
            : await developmentRatings.ListReadOnlyAsync(resultSet.Id, cancellationToken).ConfigureAwait(false);

        var dto = DevelopmentRatingProjection.Build(
            armId, termId, resultSet, roster, sectionCheck.Value.ActiveDomainsOrdered, scales, ratings);

        return Result.Success(dto);
    }

    /// <summary>
    /// Resolves the arm's section (via its class level) and checks R1: a section rates development
    /// domains only when it has at least one ACTIVE one. Shared by
    /// <see cref="GetDevelopmentRatingsHandler"/> and <c>SaveDevelopmentRatingsHandler</c> so the rule
    /// is checked identically on both routes — same convention
    /// <c>GetTraitRatingsHandler.ResolveSectionAsync</c> established for the primary sheet. Also
    /// returns EVERY domain of the section regardless of status, which the SAVE handler needs to tell
    /// an unknown indicator id from an archived one; the GET handler uses only the active subset.
    /// </summary>
    internal static async Task<Result<DevelopmentRatingSectionContext>> ResolveSectionAsync(
        Guid classLevelId,
        IClassLevelRepository classLevels,
        ISectionRepository sections,
        IDevelopmentDomainRepository developmentDomains,
        CancellationToken cancellationToken)
    {
        var allLevels = await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var level = allLevels.SingleOrDefault(candidate => candidate.Id == classLevelId);
        if (level is null)
        {
            return Result.Failure<DevelopmentRatingSectionContext>(Error.NotFound("level.not_found", "No class level was found with that id."));
        }

        var allSections = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var section = allSections.SingleOrDefault(candidate => candidate.Id == level.SectionId);
        if (section is null)
        {
            return Result.Failure<DevelopmentRatingSectionContext>(Error.NotFound("section.not_found", "No section was found with that id."));
        }

        var allDomains = await developmentDomains.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var domainsOfSection = allDomains
            .Where(domain => domain.SectionId == section.Id)
            .OrderBy(domain => domain.DisplayOrder)
            .ToArray();

        var activeDomains = domainsOfSection.Where(domain => domain.Status == DevelopmentDomainStatus.Active).ToArray();

        if (activeDomains.Length == 0)
        {
            return Result.Failure<DevelopmentRatingSectionContext>(Error.Validation(
                SectionNotRatedErrorCode,
                $"{section.Name} arms do not rate development domains."));
        }

        return Result.Success(new DevelopmentRatingSectionContext(section, activeDomains, domainsOfSection));
    }
}
