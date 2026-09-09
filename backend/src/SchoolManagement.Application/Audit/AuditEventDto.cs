using SchoolManagement.Domain.Audit;

namespace SchoolManagement.Application.Audit;

/// <summary>
/// One row of the audit trail (spec 6.1.12), rendered for the read surface. Carries every column
/// the spec's table defines and no more — <c>source_ip</c> is already truncated as stored (root
/// <c>CLAUDE.md</c> §8, PII), never widened here.
/// </summary>
/// <param name="Id">Opaque to the frontend (root CLAUDE.md §8) — the underlying BIGSERIAL is an Application/Infrastructure concern only.</param>
/// <param name="OccurredAtUtc">Stored UTC; the UI converts to WAT for display (spec 6.1.12).</param>
/// <param name="ActorAdminId">Null for a system-initiated action.</param>
/// <param name="ActorLabel">Staff name and email captured at write time (spec 6.1.12).</param>
/// <param name="Action">The privilege string of the operation, or a fixed system code.</param>
/// <param name="EntityType">For example <c>grading_band</c>, <c>result_set</c>, <c>pupil</c>.</param>
/// <param name="EntityId">Null for a bulk action, which carries a batch id in <paramref name="AfterJson"/> instead.</param>
/// <param name="Outcome">Either <c>Success</c> or <c>Rejected</c>; crosses the wire as a string (root CLAUDE.md §8) via the global enum converter.</param>
/// <param name="BeforeJson">
/// The prior state of changed fields only, or null on create. Raw JSON text, not a parsed
/// <see cref="System.Text.Json.JsonElement"/> — TASK-0049 keeps it a plain string deliberately, so a
/// second/third use of <c>JsonElement</c> alongside <c>ConfigVersionDetailDto.Snapshot</c> never
/// forces the OpenAPI generator to hoist a shared, description-less component schema for it.
/// </param>
/// <param name="AfterJson">The new state of changed fields only, or null on delete. Same raw-text shape as <see cref="BeforeJson"/>.</param>
/// <param name="Reason">Present only for the actions spec 6.1.12 mandates a reason for.</param>
/// <param name="SourceIp">Already truncated to /24 (IPv4) or /48 (IPv6) at write time.</param>
/// <param name="UserAgent">Already truncated to 300 characters at write time.</param>
public sealed record AuditEventDto(
    string Id,
    DateTimeOffset OccurredAtUtc,
    string? ActorAdminId,
    string ActorLabel,
    string Action,
    string EntityType,
    string? EntityId,
    AuditOutcome Outcome,
    string? BeforeJson,
    string? AfterJson,
    string? Reason,
    string? SourceIp,
    string? UserAgent);
