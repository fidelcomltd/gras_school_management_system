using System.Globalization;
using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// Maps <see cref="ClassLevel"/>/<see cref="Section"/> to their wire shapes, including computing
/// <c>isEntryLevel</c>/<c>isGraduatingLevel</c> at read time (spec 6.4.2: "Not stored").
/// </summary>
internal static class LevelMapper
{
    /// <summary>
    /// Projects one level, given the full universe it belongs to. An INACTIVE level is always
    /// <c>isEntryLevel: false, isGraduatingLevel: false</c> — the derivation only has meaning over the
    /// active chain (spec 6.4.2: "the active level that no other active level points at").
    /// </summary>
    /// <param name="level">The level to project.</param>
    /// <param name="allLevels">Every level (active and inactive), used to resolve <paramref name="level"/>'s section and its place in the active chain.</param>
    /// <param name="sectionNamesById">Every section's current name, keyed by id.</param>
    public static LevelDto ToDto(
        ClassLevel level,
        IReadOnlyList<ClassLevel> allLevels,
        IReadOnlyDictionary<Guid, string> sectionNamesById)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(allLevels);
        ArgumentNullException.ThrowIfNull(sectionNamesById);

        var isEntry = false;
        var isGraduating = false;

        if (level.Status == LevelStatus.Active)
        {
            var pointedAt = allLevels
                .Where(candidate => candidate.Status == LevelStatus.Active && candidate.NextLevelId is not null)
                .Select(candidate => candidate.NextLevelId!.Value)
                .ToHashSet();

            isEntry = !pointedAt.Contains(level.Id);
            isGraduating = level.NextLevelId is null;
        }

        var sectionName = sectionNamesById.TryGetValue(level.SectionId, out var name) ? name : string.Empty;

        return new LevelDto(
            level.Id.ToString("D", CultureInfo.InvariantCulture),
            level.Name,
            level.SectionId.ToString("D", CultureInfo.InvariantCulture),
            sectionName,
            level.ProgressionOrder,
            level.NextLevelId?.ToString("D", CultureInfo.InvariantCulture),
            isEntry,
            isGraduating,
            level.Status);
    }

    /// <summary>Builds the <see cref="ChainLevel"/> projection for every active level in <paramref name="allLevels"/>.</summary>
    public static IReadOnlyList<ChainLevel> ToChainLevels(IReadOnlyList<ClassLevel> allLevels)
    {
        ArgumentNullException.ThrowIfNull(allLevels);

        return allLevels
            .Where(level => level.Status == LevelStatus.Active)
            .Select(level => new ChainLevel(level.Id, level.Name, level.ProgressionOrder, level.NextLevelId))
            .ToArray();
    }
}
