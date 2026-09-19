using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>One trait, both inside <see cref="SettingsDto"/>'s envelope and as an element of <see cref="SettingsTraitsGroupDto"/>'s <c>Traits</c> array (spec 6.2.7 / 6.2.13).</summary>
/// <param name="Id">Opaque id.</param>
/// <param name="Domain">Affective or psychomotor.</param>
/// <param name="Name">For example <c>Punctuality</c>.</param>
/// <param name="DisplayOrder">Printed row order within its block.</param>
/// <param name="Status">Active or archived. An archived trait leaves new entry screens but stays on historical sheets.</param>
public sealed record TraitDto(string Id, TraitDomain Domain, string Name, int DisplayOrder, TraitStatus Status);

/// <summary>
/// The traits group, both inside <see cref="SettingsDto"/> and as <c>PUT /api/v1/settings/traits</c>'s
/// own success body (spec 6.2.7 / 6.2.13). Both blocks' scale ids travel alongside the trait list
/// because they are, together, the whole group a single <c>versionNumber</c> guards.
/// </summary>
/// <param name="AffectiveRatingScaleId">The scale the affective block's traits are rated against.</param>
/// <param name="PsychomotorRatingScaleId">The scale the psychomotor block's traits are rated against.</param>
/// <param name="Traits">Every trait, ordered by domain then <c>displayOrder</c>.</param>
/// <param name="VersionNumber">
/// The traits group's current optimistic-concurrency pointer. Echo this back as
/// <c>expectedVersion</c> on the next save.
/// </param>
public sealed record SettingsTraitsGroupDto(
    string AffectiveRatingScaleId,
    string PsychomotorRatingScaleId,
    IReadOnlyList<TraitDto> Traits,
    int VersionNumber);
