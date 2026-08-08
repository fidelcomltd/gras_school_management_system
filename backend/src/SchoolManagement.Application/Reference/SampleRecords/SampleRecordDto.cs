namespace SchoolManagement.Application.Reference.SampleRecords;

/// <summary>
/// REFERENCE SLICE — read model for a sample record.
/// </summary>
/// <remarks>
/// A DTO, not the entity. Entities never cross the HTTP boundary: exposing one leaks the database
/// shape into the contract, drags navigation properties into the serialiser, and turns every schema
/// change into a breaking API change. Repositories project straight into this type in the SQL query
/// rather than loading entities and mapping them in memory.
/// </remarks>
/// <param name="Id">
/// Opaque identifier. A STRING on the wire even though it is a GUID in the database, per the
/// repo-wide rule that IDs are opaque to clients — that way the storage key type can change without
/// breaking them.
/// </param>
/// <param name="Label">The record's short label.</param>
/// <param name="Note">The optional note, or <c>null</c> if none was supplied.</param>
/// <param name="CreatedAtUtc">When the record was created. UTC, ISO-8601 with offset.</param>
/// <param name="ModifiedAtUtc">When the record was last modified, or <c>null</c> if never.</param>
public sealed record SampleRecordDto(
    string Id,
    string Label,
    string? Note,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc);
