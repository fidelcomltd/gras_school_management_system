using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// School settings (TASK-0005a; spec 6.2.3, 6.2.9, 6.2.12). Approved contract delta:
/// <c>.agent/decisions/2026-Q3-contract-deltas.md</c> § "TASK-0005 — School settings delta" — build
/// exactly what it specifies. Only the <c>identity</c> group and the <c>config_version</c> read
/// endpoints exist here; TASK-0005b (uploads) and TASK-0005c (registration number, abbreviation)
/// extend the same <see cref="SettingsDto"/> and reuse this same ledger.
/// </summary>
/// <remarks>
/// <c>PATCH /settings/identity</c> is the ONLY mutating route in this file, and it calls both
/// <c>.RequireCsrfToken()</c> (CLAUDE.md §5) and <c>.RequireIdempotencyKey(required: false)</c>
/// (approved delta: accepted, not required — a retry converges the final field values, but would
/// otherwise double the <see cref="SchoolManagement.Domain.Settings.ConfigVersion"/> row and the
/// audit event).
/// </remarks>
public sealed class SettingsEndpoints : IEndpointModule
{
    private const string Tag = "Settings";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var settingsGroup = endpoints
            .MapGroup("/settings")
            .WithTags(Tag);

        MapGetSettings(settingsGroup);
        MapUpdateIdentity(settingsGroup);

        var configVersionsGroup = endpoints
            .MapGroup("/config-versions")
            .WithTags(Tag);

        MapListConfigVersions(configVersionsGroup);
        MapGetConfigVersion(configVersionsGroup);
    }

    private static void MapGetSettings(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetSettingsQuery(), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.View)
            .WithName("GetSettings")
            .WithSummary("Read the school settings")
            .WithDescription(
                "Everything in one payload for the settings area (spec 6.2.12). Returns only the " +
                "`identity` group as of TASK-0005a; later cards extend this same envelope additively " +
                "with sibling groups.")
            .Produces<SettingsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateIdentity(RouteGroupBuilder group) =>
        group.MapPatch("/identity", async (
                UpdateSchoolIdentityCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.IdentityUpdate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("UpdateSchoolIdentity")
            .WithSummary("Update the school's identity")
            .WithDescription(
                "School name, short name, address, phone, email, motto, head teacher name (spec " +
                "6.2.3). `timezone` and `abbreviation` are not editable here — timezone is fixed, and " +
                "the abbreviation has its own endpoint and its own optimistic-concurrency pointer. " +
                "`expectedVersion` must match the identity group's current `versionNumber` (from " +
                "`GET /settings`) or the save is rejected `409` before anything is written, and BOTH " +
                "the winning and the losing attempt are recorded on the audit trail (spec 6.2.11).")
            .Produces<SettingsIdentityGroupDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapListConfigVersions(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null) =>
            {
                var result = await sender.SendAsync(
                    new ListConfigVersionsQuery(cursor, pageSize),
                    cancellationToken);

                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Audit.View)
            .WithName("ListConfigVersions")
            .WithSummary("List configuration-version history, newest first")
            .WithDescription(
                "Cursor-paginated per spec 9.5 — never offset. `cursor` is the opaque `nextCursor` " +
                "from a previous page; omit it for the first page. `pageSize` defaults to " +
                $"{CursorPageRequest.DefaultPageSize} and is capped at {CursorPageRequest.MaxPageSize}.")
            .Produces<CursorPage<ConfigVersionSummaryDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGetConfigVersion(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetConfigVersionQuery(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Audit.View)
            .WithName("GetConfigVersion")
            .WithSummary("Read one configuration version in full, including its snapshot")
            .WithDescription(
                "Includes the full `snapshot` — the whole serialised configuration as of this save " +
                "(spec 6.2.9), not only the group that changed.")
            .Produces<ConfigVersionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
