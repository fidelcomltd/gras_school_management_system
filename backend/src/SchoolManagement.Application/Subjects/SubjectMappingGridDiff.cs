using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>One desired (subject, level) mapping in a grid-shaped write, before it becomes a row.</summary>
public sealed record DesiredSubjectMappingEntry(Guid SubjectId, Guid ClassLevelId, int DisplayOrder);

/// <summary>
/// The result of diffing a desired grid against the currently active mappings for one term — the
/// SAME computation <c>PUT /subject-mappings</c>, <c>POST /subject-mappings/copy</c> and
/// <c>POST /subject-mappings/prefill</c> all share (spec 6.6.5's preview: "This will add 12 mappings
/// and end 2 mappings").
/// </summary>
/// <param name="Additions">Desired pairs with no currently active mapping.</param>
/// <param name="Endings">Currently active mappings absent from the desired set. Always empty when the caller passed <c>additiveOnly: true</c>.</param>
/// <param name="Reorders">
/// Pairs present in both, whose <see cref="SubjectMapping.DisplayOrder"/> differs — applied silently,
/// never counted in <see cref="Additions"/>/<see cref="Endings"/>, matching spec 6.6.5's own count
/// ("add N mappings and end M mappings" never mentions reordering).
/// </param>
public sealed record SubjectMappingGridDiffResult(
    IReadOnlyList<DesiredSubjectMappingEntry> Additions,
    IReadOnlyList<SubjectMapping> Endings,
    IReadOnlyList<(SubjectMapping Mapping, int NewDisplayOrder)> Reorders);

/// <summary>Computes <see cref="SubjectMappingGridDiffResult"/>. THE single place this diff is computed.</summary>
internal static class SubjectMappingGridDiff
{
    private readonly record struct PairKey(Guid SubjectId, Guid ClassLevelId);

    /// <summary>Diffs a desired grid against the currently active mappings for one term.</summary>
    /// <param name="currentActive">Every currently ACTIVE mapping for the term being written.</param>
    /// <param name="desired">The full desired set of (subject, level) pairs for that term.</param>
    /// <param name="additiveOnly">
    /// <see langword="true"/> for <c>copy</c> and <c>prefill</c> (TASK-0070 delta amendment 5: prefill
    /// "can never end a mapping") — every currently active mapping absent from <paramref name="desired"/>
    /// is left alone rather than ended.
    /// </param>
    public static SubjectMappingGridDiffResult Compute(
        IReadOnlyList<SubjectMapping> currentActive, IReadOnlyList<DesiredSubjectMappingEntry> desired, bool additiveOnly)
    {
        ArgumentNullException.ThrowIfNull(currentActive);
        ArgumentNullException.ThrowIfNull(desired);

        var currentByKey = currentActive.ToDictionary(mapping => new PairKey(mapping.SubjectId, mapping.ClassLevelId));
        var desiredByKey = desired.ToDictionary(entry => new PairKey(entry.SubjectId, entry.ClassLevelId));

        var additions = desired
            .Where(entry => !currentByKey.ContainsKey(new PairKey(entry.SubjectId, entry.ClassLevelId)))
            .ToArray();

        var endings = additiveOnly
            ? []
            : currentActive
                .Where(mapping => !desiredByKey.ContainsKey(new PairKey(mapping.SubjectId, mapping.ClassLevelId)))
                .ToArray();

        var reorders = currentActive
            .Where(mapping => desiredByKey.TryGetValue(new PairKey(mapping.SubjectId, mapping.ClassLevelId), out var entry) &&
                               entry.DisplayOrder != mapping.DisplayOrder)
            .Select(mapping => (mapping, desiredByKey[new PairKey(mapping.SubjectId, mapping.ClassLevelId)].DisplayOrder))
            .ToArray();

        return new SubjectMappingGridDiffResult(additions, endings, reorders);
    }
}
