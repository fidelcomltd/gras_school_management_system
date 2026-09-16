namespace SchoolManagement.Application.Settings;

/// <summary>
/// One band, both inside <see cref="SettingsGradingGroupDto"/> and inside <see cref="SettingsDto"/>'s
/// envelope. An element of an ORDERED ARRAY (6.2.13's durability requirement) — never a named field —
/// sorted by <c>displayOrder</c> for printing; grade RESOLUTION sorts by <c>lowerBound</c> internally
/// (6.2.11), a concern the wire shape does not need to express.
/// </summary>
/// <param name="Id">Opaque id.</param>
/// <param name="LowerBound">0 to 100 inclusive.</param>
/// <param name="UpperBound">0 to 100 inclusive, greater than or equal to <paramref name="LowerBound"/>.</param>
/// <param name="GradeLetter">Up to 3 characters — widened by 6.2.13 to hold <c>A+</c> and <c>B-</c>.</param>
/// <param name="Remark">Printed in the Remark column of the result sheet.</param>
/// <param name="DisplayOrder">Printing order of the grading key.</param>
public sealed record GradingBandDto(
    string Id,
    int LowerBound,
    int UpperBound,
    string GradeLetter,
    string Remark,
    int DisplayOrder);

/// <summary>
/// The grading-scale group, both inside <see cref="SettingsDto"/> and as
/// <c>PUT /api/v1/settings/grading</c>'s / <c>POST /api/v1/settings/grading/reset</c>'s own success
/// body (spec 6.2.5).
/// </summary>
/// <param name="Bands">Every band, ordered by <c>displayOrder</c>.</param>
/// <param name="VersionNumber">
/// The grading group's current optimistic-concurrency pointer. Echo this back as
/// <c>expectedVersion</c> on the next save.
/// </param>
public sealed record SettingsGradingGroupDto(IReadOnlyList<GradingBandDto> Bands, int VersionNumber);
