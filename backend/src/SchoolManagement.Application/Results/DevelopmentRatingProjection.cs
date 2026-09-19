using System.Globalization;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Builds a <see cref="DevelopmentRatingSheetDto"/> from its raw ingredients — THE one place both
/// <c>GetDevelopmentRatingsHandler</c> and <c>SaveDevelopmentRatingsHandler</c> (for its "refreshed"
/// response) assemble the grid, same convention as <c>TraitRatingProjection</c>.
/// </summary>
internal static class DevelopmentRatingProjection
{
    public static DevelopmentRatingSheetDto Build(
        Guid armId,
        Guid termId,
        ResultSet? resultSet,
        IReadOnlyList<ArmRosterPupil> roster,
        IReadOnlyList<DevelopmentDomain> activeDomainsOrdered,
        IReadOnlyList<RatingScale> scales,
        IReadOnlyCollection<DevelopmentRatingSnapshot> allRatings)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(activeDomainsOrdered);
        ArgumentNullException.ThrowIfNull(scales);
        ArgumentNullException.ThrowIfNull(allRatings);

        var scalesById = scales.ToDictionary(scale => scale.Id);

        var domainDtos = activeDomainsOrdered.Select(domain => BuildDomain(domain, scalesById)).ToArray();
        var activeIndicatorTotal = domainDtos.Sum(domain => domain.ActiveIndicatorCount);

        var activeIndicatorIds = activeDomainsOrdered
            .SelectMany(domain => domain.Indicators.Where(indicator => indicator.Status == DevelopmentIndicatorStatus.Active))
            .Select(indicator => indicator.Id)
            .ToArray();

        var ratingsByPupil = allRatings
            .GroupBy(rating => rating.PupilId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(rating => rating.IndicatorId, rating => rating));

        var rows = roster.Select(pupil => BuildRow(pupil, ratingsByPupil, activeIndicatorIds)).ToArray();

        var version = DevelopmentRatingVersion.Compute(allRatings.ToArray());

        var resultSetDto = resultSet is null
            ? null
            : new ResultSetSummaryDto(
                resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute);

        return new DevelopmentRatingSheetDto(
            armId.ToString("D", CultureInfo.InvariantCulture),
            termId.ToString("D", CultureInfo.InvariantCulture),
            version,
            resultSetDto,
            domainDtos,
            activeIndicatorTotal,
            rows);
    }

    private static DevelopmentRatingDomainDto BuildDomain(DevelopmentDomain domain, Dictionary<Guid, RatingScale> scalesById)
    {
        var scale = scalesById[domain.RatingScaleId];

        var scaleDto = new RatingScaleDto(
            scale.Id.ToString("D", CultureInfo.InvariantCulture),
            scale.Name,
            scale.Points
                .OrderBy(point => point.PointOrder)
                .Select(point => new RatingScalePointDto(
                    point.Id.ToString("D", CultureInfo.InvariantCulture), point.PointCode, point.PointLabel, point.PointOrder))
                .ToList());

        var activeIndicators = domain.Indicators
            .Where(indicator => indicator.Status == DevelopmentIndicatorStatus.Active)
            .OrderBy(indicator => indicator.DisplayOrder)
            .Select(indicator => new DevelopmentIndicatorDto(
                indicator.Id.ToString("D", CultureInfo.InvariantCulture), indicator.Name, indicator.DisplayOrder, indicator.Status))
            .ToList();

        return new DevelopmentRatingDomainDto(
            domain.Id.ToString("D", CultureInfo.InvariantCulture),
            domain.Name,
            domain.DisplayOrder,
            domain.RatingScaleId.ToString("D", CultureInfo.InvariantCulture),
            scaleDto,
            domain.AllowsIndicatorComment,
            activeIndicators.Count,
            activeIndicators);
    }

    private static DevelopmentRatingRowDto BuildRow(
        ArmRosterPupil pupil, Dictionary<Guid, Dictionary<Guid, DevelopmentRatingSnapshot>> ratingsByPupil, IReadOnlyList<Guid> activeIndicatorIds)
    {
        ratingsByPupil.TryGetValue(pupil.PupilId, out var pupilRatings);

        var ratings = activeIndicatorIds.ToDictionary(
            indicatorId => indicatorId.ToString("D", CultureInfo.InvariantCulture),
            indicatorId =>
            {
                if (pupilRatings is not null && pupilRatings.TryGetValue(indicatorId, out var rating))
                {
                    return new DevelopmentRatingCellDto(
                        rating.RatingScalePointId.ToString("D", CultureInfo.InvariantCulture), rating.Comment);
                }

                return new DevelopmentRatingCellDto(null, null);
            },
            StringComparer.Ordinal);

        return new DevelopmentRatingRowDto(
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
