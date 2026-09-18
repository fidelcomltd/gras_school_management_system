namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Lifecycle state of a <see cref="DevelopmentDomain"/> (spec 6.2.13: "status (active or archived)").
/// Archiving, not deleting, is how a domain whose indicators have ever been rated is removed from new
/// entry screens while staying on historical sheets through the publication snapshot.
/// </summary>
public enum DevelopmentDomainStatus
{
    /// <summary>Normal. Appears on the entry screen for its section.</summary>
    Active = 0,

    /// <summary>Off the entry screen for new terms; stays on any sheet already snapshotted.</summary>
    Archived = 1,
}
