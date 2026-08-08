namespace SchoolManagement.Domain.Common;

/// <summary>
/// Marks an entity that is never physically deleted.
/// </summary>
/// <remarks>
/// <para>
/// Calling <c>Remove</c> on one of these is intercepted and rewritten into an update that
/// sets <see cref="IsDeleted"/>. A global query filter then hides deleted rows from every
/// read, so a query cannot forget to exclude them.
/// </para>
/// <para>
/// Consequence you must know about: a unique index on a soft-deletable entity has to be
/// filtered (<c>WHERE is_deleted = false</c>), otherwise a deleted row keeps blocking the
/// value forever. The entity configuration for such an index must say so explicitly.
/// </para>
/// </remarks>
public interface ISoftDeletable
{
    /// <summary>Whether the row is logically deleted. Maintained by the interceptor.</summary>
    bool IsDeleted { get; set; }

    /// <summary>When the row was logically deleted, or <c>null</c> if it is live.</summary>
    DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>Who logically deleted the row, or <c>null</c> if it is live.</summary>
    string? DeletedBy { get; set; }
}
