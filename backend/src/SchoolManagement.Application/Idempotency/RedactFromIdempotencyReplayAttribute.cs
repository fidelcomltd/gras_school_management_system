namespace SchoolManagement.Application.Idempotency;

/// <summary>
/// Marks a response DTO property that must never appear in a STORED idempotency replay copy —
/// orchestrator amendment A2 (approved delta "TASK-0019 / TASK-0027"): the create-account response's
/// <c>temporaryPassword</c> is shown once and "never displays it again" (spec 6.1.9, 6.1.14), so a
/// replay that returned it verbatim would be a second display. The mechanism built here is generic;
/// its first caller is TASK-0027, which marks that property with this attribute.
/// </summary>
/// <remarks>
/// Applied to the record's generated property via <c>[property: RedactFromIdempotencyReplay]</c> on
/// the primary-constructor parameter. The live response the caller actually receives is NEVER
/// touched — only the copy persisted for replay has the marked property's JSON value set to
/// <see langword="null"/>. See <c>SchoolManagement.Api.Idempotency.RequireIdempotencyKeyExtensions</c>
/// for where the reflection and redaction happen.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RedactFromIdempotencyReplayAttribute : Attribute;
