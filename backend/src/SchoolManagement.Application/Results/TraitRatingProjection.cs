using System.Globalization;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Builds a <see cref="TraitRatingSheetDto"/> from its raw ingredients — THE one place both
/// <c>GetTraitRatingsHandler</c> and <c>SaveTraitRatingsHandler</c> (for its "refreshed" response)
/// assemble the grid, same convention as <c>ScoreSheetProjection</c> (TASK-0076).
/// </summary>
internal static class TraitRatingProjection
{
    private static readonly TraitDomain[] BlockOrder = [TraitDomain.Affective, TraitDomain.Psychomotor];

    public static TraitRatingSheetDto Build(
        Guid armId,
        Guid termId,
        ResultSet? resultSet,
        IReadOnlyList<ArmRosterPupil> roster,
        IReadOnlyList<Trait> allTraits,
        IReadOnlyList<TraitBlock> blocks,
        IReadOnlyList<RatingScale> scales,
        IReadOnlyCollection<TraitRatingSnapshot> allRatings)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(allTraits);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(scales);
        ArgumentNullException.ThrowIfNull(allRatings);

        var activeTraits = allTraits.Where(trait => trait.Status == TraitStatus.Active).ToArray();
        var scalesById = scales.ToDictionary(scale => scale.Id);
        var blocksByDomain = blocks.ToDictionary(block => block.Id);

        var blockDtos = BlockOrder
            .Where(domain => blocksByDomain.ContainsKey(domain))
            .Select(domain => BuildBlock(domain, blocksByDomain[domain], scalesById, activeTraits))
            .ToArray();

        var ratingsByPupil = allRatings
            .GroupBy(rating => rating.PupilId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(rating => rating.TraitId, rating => rating.RatingScalePointId));

        var rows = roster.Select(pupil => BuildRow(pupil, ratingsByPupil, activeTraits)).ToArray();

        var version = TraitRatingVersion.Compute(allRatings.ToArray());

        var resultSetDto = resultSet is null
            ? null
            : new ResultSetSummaryDto(
                resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute, resultSet.ReturnReason);

        return new TraitRatingSheetDto(
            armId.ToString("D", CultureInfo.InvariantCulture),
            termId.ToString("D", CultureInfo.InvariantCulture),
            version,
            resultSetDto,
            blockDtos,
            rows);
    }

    private static TraitRatingBlockDto BuildBlock(
        TraitDomain domain, TraitBlock block, Dictionary<Guid, RatingScale> scalesById, IReadOnlyList<Trait> activeTraits)
    {
        var scale = scalesById[block.RatingScaleId];

        var scaleDto = new RatingScaleDto(
            scale.Id.ToString("D", CultureInfo.InvariantCulture),
            scale.Name,
            scale.Points
                .OrderBy(point => point.PointOrder)
                .Select(point => new RatingScalePointDto(
                    point.Id.ToString("D", CultureInfo.InvariantCulture), point.PointCode, point.PointLabel, point.PointOrder))
                .ToList());

        var traitDtos = activeTraits
            .Where(trait => trait.Domain == domain)
            .OrderBy(trait => trait.DisplayOrder)
            .Select(trait => new TraitDto(
                trait.Id.ToString("D", CultureInfo.InvariantCulture), trait.Domain, trait.Name, trait.DisplayOrder, trait.Status))
            .ToList();

        return new TraitRatingBlockDto(domain, scaleDto, traitDtos);
    }

    private static TraitRatingRowDto BuildRow(
        ArmRosterPupil pupil, Dictionary<Guid, Dictionary<Guid, Guid>> ratingsByPupil, IReadOnlyList<Trait> activeTraits)
    {
        ratingsByPupil.TryGetValue(pupil.PupilId, out var pupilRatings);

        var ratings = activeTraits.ToDictionary(
            trait => trait.Id.ToString("D", CultureInfo.InvariantCulture),
            trait => pupilRatings is not null && pupilRatings.TryGetValue(trait.Id, out var pointId)
                ? pointId.ToString("D", CultureInfo.InvariantCulture)
                : null,
            StringComparer.Ordinal);

        return new TraitRatingRowDto(
            pupil.PupilId.ToString("D", CultureInfo.InvariantCulture),
            pupil.RegistrationNumber,
            ComposeDisplayName(pupil),
            ratings);
    }

    private static string ComposeDisplayName(ArmRosterPupil pupil) =>
        string.IsNullOrWhiteSpace(pupil.MiddleName)
            ? $"{pupil.Surname} {pupil.FirstName}"
            : $"{pupil.Surname} {pupil.FirstName} {pupil.MiddleName}";
}
