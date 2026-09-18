using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// Every settings group's CURRENT, persisted state, bundled into one value so
/// <see cref="SettingsSnapshotBuilder.Build"/> takes one parameter instead of one per group
/// (TASK-0072 stage 3a). Loaded whole by <see cref="Abstractions.Settings.ISettingsSnapshotSource"/>;
/// a handler that just changed one group overrides ONLY that field with its own in-memory,
/// pre-save value via a <c>with</c> expression — for example
/// <c>state with { RatingScales = scales }</c> — because the group it changed has not been
/// persisted yet when the snapshot is built (spec 6.2.9's "the whole serialised configuration").
/// </summary>
/// <remarks>
/// THIS RECORD IS THE FIX for the ripple TASK-0072 stages 1-2 both paid for: before it, every
/// settings handler constructor took one repository per OTHER group purely to read its current state
/// and hand it to <see cref="SettingsSnapshotBuilder.Build"/>, so a new settings group meant editing
/// every existing handler and its tests (~17 files, TASK-0072 stage 2b's own closing note). Adding a
/// group now means adding one field here, loading it in
/// <see cref="Abstractions.Settings.ISettingsSnapshotSource"/>, and using it in
/// <see cref="SettingsSnapshotBuilder.Build"/> — every EXISTING handler keeps compiling unchanged,
/// because a <c>with</c> expression that does not name the new field simply keeps whatever the source
/// loaded for it.
/// </remarks>
/// <param name="GradingBands">The grading scale (TASK-0069), ordered or not — <see cref="SettingsSnapshotBuilder.Build"/> orders it.</param>
/// <param name="AssessmentComponents">The assessment structure (TASK-0069).</param>
/// <param name="ResultRules">The result rules singleton row (TASK-0077).</param>
/// <param name="RatingScales">The rating scales, with their points attached (TASK-0072 stage 1).</param>
/// <param name="DevelopmentDomains">The nursery development domains, with their indicators attached (TASK-0072 stage 2).</param>
public sealed record SettingsSnapshotState(
    IReadOnlyList<GradingBand> GradingBands,
    IReadOnlyList<AssessmentComponent> AssessmentComponents,
    ResultRules ResultRules,
    IReadOnlyList<RatingScale> RatingScales,
    IReadOnlyList<DevelopmentDomain> DevelopmentDomains);
