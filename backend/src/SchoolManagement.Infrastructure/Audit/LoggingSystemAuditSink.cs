using System.Text.Json;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Abstractions.Audit;

namespace SchoolManagement.Infrastructure.Audit;

/// <summary>
/// Default <see cref="ISystemAuditSink"/>: writes a structured log entry. See the interface's
/// remarks — this mirrors <c>LoggingAuthorizationAuditSink</c> (TASK-0002) exactly, and for the same
/// reason: the real <c>audit_event</c> table (spec 6.1.12) does not exist yet.
/// </summary>
/// <remarks>
/// TODO(TASK-0002): replace with real <c>audit_event</c> persistence, sharing the write's
/// transaction, once the audit log module exists — the same TODO <c>LoggingAuthorizationAuditSink</c>
/// carries, since one future card replaces both seams together.
/// </remarks>
internal sealed class LoggingSystemAuditSink(ILogger<LoggingSystemAuditSink> logger) : ISystemAuditSink
{
    /// <inheritdoc />
    public Task RecordAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        CancellationToken cancellationToken)
    {
        var metadataJson = metadata is null ? "{}" : JsonSerializer.Serialize(metadata);

        SystemAuditLog.Recorded(
            logger,
            action,
            entityType ?? "(none)",
            entityId ?? "(none)",
            metadataJson);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Source-generated log messages for <see cref="LoggingSystemAuditSink"/>. CA1848 is an error, so
/// this goes through <c>[LoggerMessage]</c> rather than direct <c>ILogger</c> calls; no parameter
/// carries personal data, only ids and codes.
/// </summary>
internal static partial class SystemAuditLog
{
    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Information,
        Message = "System audit event: action={Action} entityType={EntityType} entityId={EntityId} metadata={Metadata}")]
    public static partial void Recorded(
        ILogger logger,
        string action,
        string entityType,
        string entityId,
        string metadata);
}
