using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Application.Abstractions.Classes;

/// <summary>Persistence port for <see cref="ClassLevel"/>.</summary>
/// <remarks>
/// No filtered/paged query here, deliberately — spec 6.4.2: "The session list is short and always
/// will be" holds just as much for levels (nine seeded rows, a school adds a handful more at most).
/// Every read loads the FULL set and filters/pages/sorts in the Application handler — see
/// <c>ListLevelsHandler</c> — rather than reaching for <c>RoleRepository</c>'s raw-SQL keyset
/// technique, which exists there because that list genuinely can grow large.
/// </remarks>
public interface IClassLevelRepository
{
    /// <summary>Adds a new level. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(ClassLevel level, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED level by id, for a command that will mutate it.</summary>
    Task<ClassLevel?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="level"/> permanently. No <c>SaveChangesAsync</c>.</summary>
    Task RemoveAsync(ClassLevel level, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="normalizedNameKey"/> (already lower-invariant) is already used by
    /// another level — spec 6.4.2: "Unique, case-insensitive," with NO status carve-out (same
    /// reasoning as <c>Role.NameKey</c>'s own unique index: an inactive level's name still blocks
    /// reuse, so a rename can never collide with a deactivated sibling's stored name).
    /// </summary>
    Task<bool> NameExistsAsync(string normalizedNameKey, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>
    /// Every level, active and inactive, TRACKED — for a command that may need to mutate more than
    /// one row at once (an insert-after shift, a whole-chain reorder).
    /// </summary>
    Task<IReadOnlyList<ClassLevel>> ListAllTrackedAsync(CancellationToken cancellationToken);

    /// <summary>Every level, active and inactive, <c>AsNoTracking</c> — for a query.</summary>
    Task<IReadOnlyList<ClassLevel>> ListAllReadOnlyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether any level (active OR inactive — inactive levels keep their stored <c>nextLevelId</c>
    /// for historical reference, spec 6.4.2) currently points its <c>nextLevelId</c> at
    /// <paramref name="id"/>. One of the delete precondition's checkable references TODAY; see
    /// <c>DeleteLevelHandler</c> for the DEFERRED remainder (arms, enrolments, subject mappings,
    /// results — none of those tables exist yet).
    /// </summary>
    Task<ClassLevel?> FindReferencingNextLevelAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Flips <c>progression_order</c> to its NEGATIVE for every level in <paramref name="ids"/>, via a
    /// single raw SQL statement executed IMMEDIATELY (not through change tracking) inside the ambient
    /// transaction <c>UnitOfWorkBehavior</c> already opened.
    /// </summary>
    /// <remarks>
    /// Required before a batch reassignment of <c>progression_order</c> (a whole-chain
    /// <c>POST /levels/reorder</c>, or the "shift everyone after the insertion point up by one" half of
    /// <c>POST /levels</c>'s insert-after path) — spec 6.4.2's own partial UNIQUE index on
    /// <c>progression_order</c> is NOT DEFERRABLE (PostgreSQL has no deferrable PARTIAL constraint,
    /// only deferrable table constraints, and this one must stay partial to exempt inactive levels),
    /// so it is checked immediately, per row, as each row is written. Two rows simply swapping values
    /// (or N rows all shifting by one) will collide mid-batch if written directly to their FINAL
    /// values, regardless of write order — proven empirically against the real database during this
    /// card, not assumed. Negating first moves every affected row to a value nothing else in the
    /// table can hold (no stored value is ever negative), so the SECOND pass — the caller's own
    /// ordinary entity mutations to the real final values, persisted later by the normal tracked
    /// <c>SaveChangesAsync</c> — can never collide with a level not yet in its own final position.
    /// </remarks>
    Task NegateProgressionOrdersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
