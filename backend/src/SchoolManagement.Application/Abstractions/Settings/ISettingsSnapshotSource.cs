using SchoolManagement.Application.Settings;

namespace SchoolManagement.Application.Abstractions.Settings;

/// <summary>
/// Loads every settings group's CURRENT, persisted state in one call, for
/// <see cref="SettingsSnapshotBuilder.Build"/> (TASK-0072 stage 3a). Implemented in Infrastructure,
/// injecting one repository per group so no settings command handler has to any more — see
/// <see cref="SettingsSnapshotState"/>'s remarks for the ripple this replaces.
/// </summary>
public interface ISettingsSnapshotSource
{
    /// <summary>
    /// Reads every group's current state fresh (read-only, no tracking). A handler that just changed
    /// one group overrides that one field on the result with its own in-memory, pre-save value before
    /// calling <see cref="SettingsSnapshotBuilder.Build"/> — the group it changed is not yet persisted
    /// when this runs, so a naive re-read here would still return the OLD value.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<SettingsSnapshotState> LoadAsync(CancellationToken cancellationToken);
}
