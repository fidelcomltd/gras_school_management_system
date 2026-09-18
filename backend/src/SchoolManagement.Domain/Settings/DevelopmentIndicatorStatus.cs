namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Lifecycle state of a <see cref="DevelopmentIndicator"/> (spec 6.2.13: "status (active or
/// archived)"). Distinct from <see cref="DevelopmentDomainStatus"/> despite the identical two values —
/// grep-able and matches this codebase's per-entity convention (<c>RoleStatus</c>, <c>LevelStatus</c>),
/// rather than one shared enum coupling an indicator's lifecycle to its domain's.
/// </summary>
public enum DevelopmentIndicatorStatus
{
    /// <summary>Normal. Appears on the entry screen for its domain.</summary>
    Active = 0,

    /// <summary>
    /// Off the entry screen for new terms; stays on any sheet already snapshotted. Spec 6.2.13's
    /// refusal message: an indicator that has ever been rated is archived rather than removed.
    /// </summary>
    Archived = 1,
}
