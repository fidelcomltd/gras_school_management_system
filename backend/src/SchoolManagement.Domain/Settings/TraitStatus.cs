namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Lifecycle state of a <see cref="Trait"/> (spec 6.2.7's status rule, carried into 6.2.13's per-block
/// scales). Archiving, not deleting, is how a trait that has ever been rated is removed from new entry
/// screens while staying on historical sheets through the publication snapshot.
/// </summary>
public enum TraitStatus
{
    /// <summary>Normal. Appears on the entry screen for its domain.</summary>
    Active = 0,

    /// <summary>Off the entry screen for new terms; stays on any sheet already snapshotted.</summary>
    Archived = 1,
}
