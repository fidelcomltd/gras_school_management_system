namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Spec 6.2.13's amendment seed: nine bands, replacing 6.2.5's superseded six-band A-to-F table (see
/// <c>04-module-school-settings.md</c>'s "Known drift" entry — the earlier text was never deleted).
/// The SINGLE source both the install-time migration seed (<c>GradingBandConfiguration.HasData</c>)
/// and <c>POST /settings/grading/reset</c> (<c>ResetGradingCommandHandler</c>) build from — the same
/// relationship <c>SeededClassLevels</c> already has with its own consumers.
/// </summary>
/// <remarks>
/// No id travels with these rows: <see cref="GradingBand"/>'s remarks explain why the whole scale is
/// a blind replace, never an id-preserving merge — a reset simply builds fresh
/// <see cref="GradingBand"/> instances with new ids from this list's values, exactly as an ordinary
/// <c>PUT /settings/grading</c> save would from client input. The migration's own fixed ids (needed
/// only for <c>HasData</c>'s determinism) live in <c>GradingBandConfiguration</c>, not here.
/// </remarks>
public static class GradingScaleSeed
{
    /// <summary>
    /// The nine seeded bands, in print order (highest first) — the order a fresh
    /// <c>PUT /settings/grading</c>-shaped save would assign as <c>displayOrder</c> 1 through 9.
    /// </summary>
    public static readonly IReadOnlyList<GradingBandInput> SeededBands =
    [
        new(90, 100, "A+", "Very excellent"),
        new(85, 89, "A", "Excellent"),
        new(75, 84, "B", "Very good"),
        new(70, 74, "B-", "Good"),
        new(60, 69, "C+", "Average"),
        new(50, 59, "C", "Fair"),
        new(40, 49, "D", "More effort"),
        new(20, 39, "E", "Not Now"),
        new(0, 19, "F", "Fail"),
    ];
}
