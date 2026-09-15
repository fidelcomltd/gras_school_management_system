namespace SchoolManagement.Domain.Admissions;

/// <summary>An <see cref="AdmissionRecord"/>'s section A "New / Returning" tick (spec 6.5.9).</summary>
public enum AdmissionType
{
    /// <summary>The pupil has never been enrolled at the school before.</summary>
    New,

    /// <summary>The pupil is re-entering after a prior withdrawal or transfer (spec 6.5.14).</summary>
    Returning,
}
