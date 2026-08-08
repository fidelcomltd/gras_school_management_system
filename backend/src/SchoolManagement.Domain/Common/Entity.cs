namespace SchoolManagement.Domain.Common;

/// <summary>
/// Base class for entities with identity. Equality is by <see cref="Id"/> and concrete type,
/// never by reference or by field-by-field comparison.
/// </summary>
/// <typeparam name="TId">The identity type. Prefer <see cref="Guid"/> created with
/// <see cref="Guid.CreateVersion7()"/> so keys are time-ordered and index-friendly.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>Initialises the entity with its identity.</summary>
    protected Entity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    /// <summary>
    /// Required by EF Core for materialisation. Never call this from application code.
    /// </summary>
    // The null-forgiving assignment is safe because EF Core always populates Id from the
    // database row immediately after constructing the instance.
    protected Entity() => Id = default!;

    /// <summary>The entity's identity. Assigned once at construction and never changed.</summary>
    public TId Id { get; private set; }

    /// <inheritdoc />
    public bool Equals(Entity<TId>? other) =>
        other is not null && other.GetType() == GetType() && other.Id.Equals(Id);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity<TId> entity && Equals(entity);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
