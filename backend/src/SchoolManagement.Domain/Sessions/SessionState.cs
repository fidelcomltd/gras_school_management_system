namespace SchoolManagement.Domain.Sessions;

/// <summary>
/// Lifecycle of an <see cref="AcademicSession"/> (spec 6.3.3). Only one session system-wide may be
/// <see cref="Active"/> — enforced by a partial unique index (spec 6.3.9), not application code.
/// </summary>
public enum SessionState
{
    /// <summary>Created, not yet the current session. Its terms have not been opened.</summary>
    Upcoming = 0,

    /// <summary>The current session. Set when its First Term opens (spec 6.3.5).</summary>
    Active = 1,

    /// <summary>
    /// No longer current. Set as a side effect of the NEXT session's First Term opening (spec 6.3.5)
    /// — not when this session's own last term closes, which can leave a session <see cref="Active"/>
    /// with zero active terms for a while (the school plans next year while Third Term still runs).
    /// </summary>
    Closed = 2,
}
