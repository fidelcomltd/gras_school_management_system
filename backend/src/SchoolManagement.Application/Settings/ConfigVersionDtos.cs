using System.Text.Json;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// One row of <c>GET /api/v1/config-versions</c>'s cursor-paged list — everything except the
/// snapshot itself, which only the detail endpoint returns.
/// </summary>
/// <param name="Id">Opaque id. Pass to <c>GET /api/v1/config-versions/{id}</c> for the full detail.</param>
/// <param name="VersionNumber">The globally monotonic version number (spec 6.2.9).</param>
/// <param name="ChangedGroup">Which settings group this save changed.</param>
/// <param name="ActorAdminId">The acting administrator's id, or <see langword="null"/> for a system action.</param>
/// <param name="CreatedAtUtc">When this version was written.</param>
public sealed record ConfigVersionSummaryDto(
    string Id,
    long VersionNumber,
    string ChangedGroup,
    string? ActorAdminId,
    DateTimeOffset CreatedAtUtc);

/// <summary>The full body of <c>GET /api/v1/config-versions/{id}</c>.</summary>
/// <param name="Id">Opaque id.</param>
/// <param name="VersionNumber">The globally monotonic version number (spec 6.2.9).</param>
/// <param name="ChangedGroup">Which settings group this save changed.</param>
/// <param name="ActorAdminId">The acting administrator's id, or <see langword="null"/> for a system action.</param>
/// <param name="Reason">
/// The reason given for this save, or <see langword="null"/> when the changed group's rule does not
/// require one (6.2.10) — always <see langword="null"/> for an <c>Identity</c> row.
/// </param>
/// <param name="CreatedAtUtc">When this version was written.</param>
/// <param name="Snapshot">
/// The whole serialised configuration as of this save (6.2.9) — every group's values at that moment,
/// not only the one that changed.
/// </param>
public sealed record ConfigVersionDetailDto(
    string Id,
    long VersionNumber,
    string ChangedGroup,
    string? ActorAdminId,
    string? Reason,
    DateTimeOffset CreatedAtUtc,
    JsonElement Snapshot);
