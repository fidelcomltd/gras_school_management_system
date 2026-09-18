namespace SchoolManagement.Application.Settings;

/// <summary>One point, both inside <see cref="RatingScaleDto"/> and inside <see cref="SettingsRatingScaleGroupDto"/>'s envelope. An element of an ORDERED ARRAY, sorted by <c>pointOrder</c>.</summary>
/// <param name="Id">Opaque id.</param>
/// <param name="PointCode">Up to 1 character — the mark printed in the rating column, for example <c>E</c> or <c>5</c>.</param>
/// <param name="PointLabel">The legend text for this point.</param>
/// <param name="PointOrder">Ascending from worst to best.</param>
public sealed record RatingScalePointDto(string Id, string PointCode, string PointLabel, int PointOrder);

/// <summary>One scale, both inside <see cref="SettingsDto"/>'s envelope and as an element of <see cref="SettingsRatingScaleGroupDto"/>'s <c>Scales</c> array (spec 6.2.13).</summary>
/// <param name="Id">Opaque id.</param>
/// <param name="Name">For example <c>Nursery development</c>.</param>
/// <param name="Points">Every point on this scale, ordered by <c>pointOrder</c>.</param>
public sealed record RatingScaleDto(string Id, string Name, IReadOnlyList<RatingScalePointDto> Points);

/// <summary>
/// The rating-scales group, both inside <see cref="SettingsDto"/> and as
/// <c>PUT /api/v1/settings/rating-scales</c>'s own success body (spec 6.2.13).
/// </summary>
/// <param name="Scales">Every scale, ordered by name.</param>
/// <param name="VersionNumber">
/// The rating-scales group's current optimistic-concurrency pointer. Echo this back as
/// <c>expectedVersion</c> on the next save.
/// </param>
public sealed record SettingsRatingScaleGroupDto(IReadOnlyList<RatingScaleDto> Scales, int VersionNumber);
