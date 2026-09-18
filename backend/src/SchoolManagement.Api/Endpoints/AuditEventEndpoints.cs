using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Audit;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The audit trail's read surface (TASK-0049; spec 6.1.12). No mutating endpoint exists here, and
/// that is deliberate: "Entries cannot be edited or deleted through any interface, and no endpoint
/// exists that would allow it." Persistence and the append-only guarantee are TASK-0048.
/// </summary>
/// <remarks>
/// Neither route calls <c>.RequireCsrfToken()</c> or declares <c>Idempotency-Key</c>, matching every
/// other GET route in this codebase (<c>ListAdminAccounts</c>, <c>ListPupils</c>, ...) even though
/// <c>/export</c> itself writes one row: both mechanisms are reserved for POST/PATCH/DELETE routes
/// here, and inventing a second convention for the one GET that happens to have a side effect would
/// be exactly that — a second convention. See this card's report for the assumption this records.
/// </remarks>
public sealed class AuditEventEndpoints : IEndpointModule
{
    private const string Tag = "Audit";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/audit-events")
            .WithTags(Tag);

        MapList(group);
        MapExport(group);
    }

    private static void MapList(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null,
                [FromQuery] DateTimeOffset? fromUtc = null,
                [FromQuery] DateTimeOffset? toUtc = null,
                [FromQuery] Guid? actorAdminId = null,
                [FromQuery] string? action = null,
                [FromQuery] string? entityType = null,
                [FromQuery] string? entityId = null,
                [FromQuery] AuditOutcome? outcome = null) =>
            {
                var result = await sender.SendAsync(
                    new ListAuditEventsQuery(cursor, pageSize, fromUtc, toUtc, actorAdminId, action, entityType, entityId, outcome),
                    cancellationToken);

                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Audit.View)
            .WithName("ListAuditEvents")
            .WithSummary("List audit events")
            .WithDescription(
                "Spec 6.1.12: filterable by date range (`fromUtc`/`toUtc`, both inclusive), " +
                "`actorAdminId`, `action`, `entityType`, `entityId` and `outcome` — every filter " +
                "optional and combinable. Sorted newest first by default (`occurred_at` descending, `id` " +
                "descending as the tie-break within the same instant). Cursor-paginated per spec " +
                $"9.5 — never offset. `pageSize` defaults to {CursorPageRequest.DefaultPageSize} " +
                $"and is capped at {CursorPageRequest.MaxPageSize}.")
            .Produces<CursorPage<AuditEventDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapExport(RouteGroupBuilder group) =>
        group.MapGet("/export", async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] DateTimeOffset? fromUtc = null,
                [FromQuery] DateTimeOffset? toUtc = null,
                [FromQuery] Guid? actorAdminId = null,
                [FromQuery] string? action = null,
                [FromQuery] string? entityType = null,
                [FromQuery] string? entityId = null,
                [FromQuery] AuditOutcome? outcome = null) =>
            {
                var result = await sender.SendAsync(
                    new ExportAuditEventsCommand(fromUtc, toUtc, actorAdminId, action, entityType, entityId, outcome),
                    cancellationToken);

                return result.Match(events => TypedResults.Stream(
                    stream => AuditEventCsvWriter.WriteAsync(stream, events, cancellationToken),
                    "text/csv",
                    fileDownloadName: "audit-log.csv"));
            })
            .RequirePrivilege(Privileges.Audit.Export)
            .WithName("ExportAuditEvents")
            .WithSummary("Export a filtered audit log to CSV")
            .WithDescription(
                "Spec 6.1.12: \"Export to CSV requires audit.export and is itself an audit event.\" " +
                "Same filters as the list endpoint, no paging — the whole matching set is " +
                "streamed, never buffered in memory. Before any row is written to the response, this " +
                "call writes its OWN `audit_event` row (action `audit.export`) recording the filters " +
                "used, so a later read of the log can answer \"who exported what.\" Columns are " +
                "exactly spec 6.1.12's thirteen; `sourceIp` stays truncated as stored.")
            .Produces<string>(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}

/// <summary>
/// Hand-rolled RFC 4180 CSV encoding for <see cref="AuditEventDto"/> — no dependency added for
/// thirteen well-known columns. Writes directly to the response body stream as each row is pulled
/// off the source sequence, so the filtered set is never materialised as a list.
/// </summary>
internal static class AuditEventCsvWriter
{
    private static readonly string[] Header =
    [
        "id", "occurredAtUtc", "actorAdminId", "actorLabel", "action", "entityType", "entityId",
        "outcome", "beforeJson", "afterJson", "reason", "sourceIp", "userAgent",
    ];

    public static async Task WriteAsync(Stream stream, IAsyncEnumerable<AuditEventDto> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        // leaveOpen: TypedResults.Stream owns closing the underlying response stream.
        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(string.Join(',', Header)).ConfigureAwait(false);

        await foreach (var auditEvent in events.WithCancellation(cancellationToken))
        {
            var fields = new[]
            {
                auditEvent.Id,
                auditEvent.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
                auditEvent.ActorAdminId ?? string.Empty,
                auditEvent.ActorLabel,
                auditEvent.Action,
                auditEvent.EntityType,
                auditEvent.EntityId ?? string.Empty,
                auditEvent.Outcome.ToString(),
                auditEvent.BeforeJson ?? string.Empty,
                auditEvent.AfterJson ?? string.Empty,
                auditEvent.Reason ?? string.Empty,
                auditEvent.SourceIp ?? string.Empty,
                auditEvent.UserAgent ?? string.Empty,
            };

            await writer.WriteLineAsync(string.Join(',', fields.Select(field => Escape(Neutralize(field))))).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// TASK-0053: neutralise CSV formula injection. A field beginning with <c>=</c>, <c>+</c>,
    /// <c>-</c>, <c>@</c>, TAB or CR is executed as a formula by Excel/LibreOffice/Sheets on open —
    /// RFC 4180 quoting alone does not stop this, since a quoted <c>"=1+1"</c> is still a formula.
    /// The conventional fix is a leading apostrophe, understood by all three as "treat as literal
    /// text". <c>user_agent</c> is attacker-controlled (failed sign-ins are audited), so this is
    /// applied uniformly to every field rather than enumerating "the dangerous" columns.
    /// Must run BEFORE <see cref="Escape"/>: neutralising after quoting would insert the apostrophe
    /// outside the quotes and corrupt the field.
    /// Assumption: none of the thirteen columns currently emits a signed number, so blanket-
    /// prefixing a leading '-' is safe today. Revisit if a future column carries one.
    /// </summary>
    private static string Neutralize(string field)
    {
        if (field.Length == 0)
        {
            return field;
        }

        return field[0] switch
        {
            '=' or '+' or '-' or '@' or '\t' or '\r' => "'" + field,
            _ => field,
        };
    }

    /// <summary>RFC 4180: a field containing a comma, quote or line break is quoted, with embedded quotes doubled.</summary>
    private static string Escape(string field)
    {
        if (field.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
