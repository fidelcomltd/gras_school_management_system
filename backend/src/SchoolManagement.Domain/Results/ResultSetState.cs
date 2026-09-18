namespace SchoolManagement.Domain.Results;

/// <summary>
/// A <see cref="ResultSet"/>'s lifecycle state (spec 09 §6.7.11). A result set that does not exist
/// yet is shown in the interface as "Not started" — the absence of a row, not a member of this enum.
/// </summary>
public enum ResultSetState
{
    /// <summary>Marks, traits, attendance and remarks are editable. The state a fresh set starts in.</summary>
    Draft,

    /// <summary>Submitted; locked against the class teacher, waiting on the head teacher.</summary>
    AwaitingApproval,

    /// <summary>Approved by the head teacher; not yet published.</summary>
    Approved,

    /// <summary>Visible to parents on the portal.</summary>
    Published,

    /// <summary>Sent back to the class teacher with a reason; marks editable again.</summary>
    ReturnedForCorrection,

    /// <summary>A previously published set pulled from the portal by a Super Admin.</summary>
    Withdrawn,
}
