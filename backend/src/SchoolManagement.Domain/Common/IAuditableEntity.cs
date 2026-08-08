namespace SchoolManagement.Domain.Common;

/// <summary>
/// Marks an entity whose create/modify audit fields are maintained automatically by
/// <c>AuditingInterceptor</c>. Implement it and the fields are populated on save; never
/// set them by hand, or the values will disagree between code paths.
/// </summary>
/// <remarks>
/// The setters are public purely so the EF Core interceptor can populate them. This is a
/// deliberate, contained purity compromise: the alternative (reflection over backing fields)
/// trades a visible seam for an invisible one. Application code must treat them as read-only.
/// </remarks>
public interface IAuditableEntity
{
    /// <summary>When the row was created. UTC, set once by the interceptor.</summary>
    DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Who created the row, or <c>null</c> for a system/anonymous action.</summary>
    string? CreatedBy { get; set; }

    /// <summary>When the row was last modified. <c>null</c> until first modification.</summary>
    DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <summary>Who last modified the row, or <c>null</c> for a system/anonymous action.</summary>
    string? ModifiedBy { get; set; }
}
