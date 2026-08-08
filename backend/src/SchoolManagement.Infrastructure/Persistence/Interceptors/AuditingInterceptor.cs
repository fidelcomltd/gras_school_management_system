using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps audit fields and converts hard deletes into soft deletes, on every save.
/// </summary>
/// <remarks>
/// <para>
/// WHY BOTH CONCERNS LIVE IN ONE INTERCEPTOR: they are order-dependent. Soft deletion rewrites an
/// entry from <see cref="EntityState.Deleted"/> to <see cref="EntityState.Modified"/>, and auditing
/// stamps <c>ModifiedAtUtc</c> on modified entries. Split across two interceptors, correctness would
/// depend on registration order — which is invisible at the point of failure and one careless
/// reorder away from silently losing the audit trail on deletes. Keeping them adjacent makes the
/// ordering explicit and local.
/// </para>
/// <para>
/// Implemented as an interceptor rather than an override of <c>SaveChangesAsync</c> so it also
/// applies to saves initiated from anywhere else (tests, tooling, a future background worker), and
/// so <see cref="ApplicationDbContext"/> stays free of cross-cutting logic.
/// </para>
/// <para>
/// Both the sync and async entry points are implemented. EF Core does not route one through the
/// other, so overriding only the async path would leave any synchronous save silently unaudited.
/// </para>
/// </remarks>
internal sealed class AuditingInterceptor(TimeProvider timeProvider, ICurrentUser currentUser)
    : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ApplyAuditAndSoftDelete(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ApplyAuditAndSoftDelete(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditAndSoftDelete(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // A single timestamp for the whole save, so every row written by one transaction carries the
        // same instant. Reading the clock per entity would make rows saved together differ by
        // microseconds and make an audit trail harder to reason about.
        var timestamp = timeProvider.GetUtcNow();
        var actor = currentUser.UserId;

        // Materialised before iterating: changing an entry's State modifies the ChangeTracker's
        // collection, which would invalidate a live enumerator.
        var entries = context.ChangeTracker.Entries().ToArray();

        foreach (var entry in entries)
        {
            // MUST come first — see the class remarks. Rewriting Deleted to Modified is what allows
            // the audit stamp below to record who deleted the row and when.
            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable deletable })
            {
                entry.State = EntityState.Modified;
                deletable.IsDeleted = true;
                deletable.DeletedAtUtc = timestamp;
                deletable.DeletedBy = actor;
            }

            if (entry.Entity is not IAuditableEntity auditable)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAtUtc = timestamp;
                    auditable.CreatedBy = actor;
                    StampConcurrencyToken(entry);
                    break;

                case EntityState.Modified:
                    auditable.ModifiedAtUtc = timestamp;
                    auditable.ModifiedBy = actor;
                    StampConcurrencyToken(entry);
                    break;

                case EntityState.Detached:
                case EntityState.Unchanged:
                case EntityState.Deleted:
                default:
                    // Nothing to stamp. Deleted can still be reached here for an entity that is not
                    // soft-deletable, which is a genuine hard delete and leaves no row to audit.
                    break;
            }
        }
    }

    /// <summary>
    /// Assigns a fresh optimistic-concurrency token.
    /// </summary>
    /// <remarks>
    /// Setting CurrentValue leaves the ORIGINAL value untouched, and EF Core builds the UPDATE's WHERE
    /// clause from the original. So a write whose row was changed by someone else in the meantime matches
    /// zero rows and raises <c>DbUpdateConcurrencyException</c> — which the global exception handler maps
    /// to 409. This is the whole mechanism: without the reassignment the token would never change and
    /// concurrent writes would silently overwrite each other.
    /// <para>
    /// Version 7 GUIDs are used for consistency with primary keys; only inequality matters here.
    /// </para>
    /// </remarks>
    private static void StampConcurrencyToken(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        // Absent for an entity type that opted out of the convention; nothing to do.
        if (entry.Metadata.FindProperty(ApplicationDbContext.ConcurrencyTokenProperty) is null)
        {
            return;
        }

        entry.Property(ApplicationDbContext.ConcurrencyTokenProperty).CurrentValue = Guid.CreateVersion7();
    }
}
