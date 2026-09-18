namespace SchoolManagement.Domain.Sessions;

/// <summary>
/// Lifecycle of a <see cref="Term"/> (spec 6.3.4, 6.3.6). Only one term system-wide may be
/// <see cref="Active"/> — enforced by a partial unique index (spec 6.3.9), not application code.
/// </summary>
public enum TermState
{
    /// <summary>Created, not yet opened. No score entry. Moves to <see cref="Active"/> by <c>term.open</c>.</summary>
    Upcoming = 0,

    /// <summary>The only term in which marks can be entered. Moves to <see cref="Closed"/> by <c>term.close</c>.</summary>
    Active = 1,

    /// <summary>
    /// Read-only for marks; score-entry-shaped writes are refused (spec 6.3.6). Reversible only by a
    /// Super Admin via reopen.
    /// </summary>
    Closed = 2,
}
