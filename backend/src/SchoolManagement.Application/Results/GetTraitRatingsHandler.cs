using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="GetTraitRatingsQuery"/>.</summary>
internal sealed class GetTraitRatingsHandler(
    IArmRepository arms,
    ITermRepository terms,
    IClassLevelRepository classLevels,
    ISectionRepository sections,
    IEnrolmentRepository enrolments,
    ITraitRepository traits,
    IRatingScaleRepository ratingScales,
    IResultSetRepository resultSets,
    ITraitRatingRepository traitRatings)
    : IRequestHandler<GetTraitRatingsQuery, Result<TraitRatingSheetDto>>
{
    /// <summary>The arm's section does not rate traits (TASK-0083 ruling R1).</summary>
    public const string SectionNotRatedErrorCode = "trait_ratings.section_not_rated";

    /// <inheritdoc />
    public async Task<Result<TraitRatingSheetDto>> HandleAsync(GetTraitRatingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<TraitRatingSheetDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<TraitRatingSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var sectionCheck = await ResolveSectionAsync(arm.ClassLevelId, classLevels, sections, cancellationToken).ConfigureAwait(false);
        if (sectionCheck.IsFailure)
        {
            return Result.Failure<TraitRatingSheetDto>(sectionCheck.Error);
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var allTraits = await traits.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var blocks = await traits.ListBlocksReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var scales = await ratingScales.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var resultSet = await resultSets.FindReadOnlyByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<TraitRatingSnapshot> ratings = resultSet is null
            ? []
            : await traitRatings.ListReadOnlyAsync(resultSet.Id, cancellationToken).ConfigureAwait(false);

        var dto = TraitRatingProjection.Build(armId, termId, resultSet, roster, allTraits, blocks, scales, ratings);

        return Result.Success(dto);
    }

    /// <summary>
    /// Resolves the arm's section (via its class level) and checks R1: a section rates traits only
    /// when <c>Section.RatesTraits</c> is true. Shared by <see cref="GetTraitRatingsHandler"/> and
    /// <c>SaveTraitRatingsHandler</c> so the rule is checked identically on both routes.
    /// </summary>
    internal static async Task<Result> ResolveSectionAsync(
        Guid classLevelId, IClassLevelRepository classLevels, ISectionRepository sections, CancellationToken cancellationToken)
    {
        var allLevels = await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var level = allLevels.SingleOrDefault(candidate => candidate.Id == classLevelId);
        if (level is null)
        {
            return Result.Failure(Error.NotFound("level.not_found", "No class level was found with that id."));
        }

        var allSections = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var section = allSections.SingleOrDefault(candidate => candidate.Id == level.SectionId);
        if (section is null)
        {
            return Result.Failure(Error.NotFound("section.not_found", "No section was found with that id."));
        }

        if (!section.RatesTraits)
        {
            return Result.Failure(Error.Validation(
                SectionNotRatedErrorCode,
                $"{section.Name} arms do not rate traits."));
        }

        return Result.Success();
    }
}
