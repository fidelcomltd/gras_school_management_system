namespace SchoolManagement.Domain.Audit;

/// <summary>
/// Spec 6.1.12's <c>outcome</c> column: "success or rejected. Rejected entries are written for
/// privilege failures and for escalation attempts."
/// </summary>
public enum AuditOutcome
{
    /// <summary>The governed write happened.</summary>
    Success,

    /// <summary>
    /// The attempt was refused — a privilege check or an escalation rule. No governed write
    /// occurred, which is exactly why this row must survive even when everything else in the same
    /// request rolls back (root <c>CLAUDE.md</c> §4.1 decision, 2026-09-09).
    /// </summary>
    Rejected,
}
