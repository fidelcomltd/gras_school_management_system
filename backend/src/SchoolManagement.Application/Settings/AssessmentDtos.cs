namespace SchoolManagement.Application.Settings;

/// <summary>
/// One component, both inside <see cref="SettingsAssessmentGroupDto"/> and inside
/// <see cref="SettingsDto"/>'s envelope. An element of an ORDERED ARRAY (6.2.13's durability
/// requirement) — never a named field like <c>firstCa</c>/<c>secondCa</c>; a client that reads by
/// name breaks the moment a school adds a component.
/// </summary>
/// <param name="Id">Opaque id — the key marks are stored against (spec 6.2.6).</param>
/// <param name="Name">For example "1st CA", "2nd CA", "Exam".</param>
/// <param name="ShortLabel">Used as the column header where space is tight.</param>
/// <param name="MaxMark">1 to 100.</param>
/// <param name="IsExamination">Exactly one component in the structure has this <see langword="true"/>.</param>
/// <param name="DisplayOrder">Column order. The examination component is always last.</param>
public sealed record AssessmentComponentDto(
    string Id,
    string Name,
    string ShortLabel,
    int MaxMark,
    bool IsExamination,
    int DisplayOrder);

/// <summary>
/// The assessment-structure group, both inside <see cref="SettingsDto"/> and as
/// <c>PUT /api/v1/settings/assessment</c>'s own success body (spec 6.2.6).
/// </summary>
/// <param name="Components">Every component, ordered by <c>displayOrder</c> (examination last).</param>
/// <param name="VersionNumber">
/// The assessment group's current optimistic-concurrency pointer. Echo this back as
/// <c>expectedVersion</c> on the next save.
/// </param>
public sealed record SettingsAssessmentGroupDto(IReadOnlyList<AssessmentComponentDto> Components, int VersionNumber);

/// <summary>
/// One submitted component on the wire (spec 6.2.6/6.2.13). <see cref="Id"/> is opaque (CLAUDE.md
/// §8) — <see langword="null"/> for a new component; when it matches an existing component's id it is
/// an edit to that same row, never a delete-and-recreate (see <c>AssessmentComponent</c>'s remarks).
/// <c>displayOrder</c> is deliberately absent — it is system-maintained from array position (spec
/// 6.2.6: "System-maintained from the drag order"), with the examination forced last regardless of
/// where it sits in this array.
/// </summary>
/// <param name="Id">An existing component's opaque id, or <see langword="null"/> for a new one.</param>
/// <param name="Name">Up to 40 characters. Unique, case-insensitive.</param>
/// <param name="ShortLabel">Up to 12 characters. Unique, case-insensitive.</param>
/// <param name="MaxMark">1 to 100.</param>
/// <param name="IsExamination">Exactly one submitted component must set this <see langword="true"/>.</param>
public sealed record AssessmentComponentSaveRequest(string? Id, string Name, string ShortLabel, int MaxMark, bool IsExamination);
