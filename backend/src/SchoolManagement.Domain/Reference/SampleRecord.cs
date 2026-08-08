using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Reference;

/// <summary>
/// REFERENCE SCAFFOLD — NOT A BUSINESS ENTITY. DELETE WITH THE FIRST REAL AGGREGATE.
/// </summary>
/// <remarks>
/// <para>
/// This type exists only to prove the persistence wiring actually works end to end:
/// EF Core mapping, the auditing interceptor, the soft-delete query filter, the <c>xmin</c>
/// concurrency token, snake_case naming, pagination, migrations, and the integration-test
/// harness. It carries no business meaning and no product decision.
/// </para>
/// <para>
/// The domain vocabulary for this project has not been decided (see the orchestrator's
/// open questions), so inventing <c>Student</c> or <c>Enrolment</c> here would be guessing
/// at a product. When the first real aggregate arrives, delete this type, its configuration,
/// its repository, its endpoints, and its tests, and add a migration dropping the table.
/// Tracked in <c>docs/ASSUMPTIONS.md</c>.
/// </para>
/// </remarks>
public sealed class SampleRecord : Entity<Guid>, IAuditableEntity, ISoftDeletable
{
    /// <summary>The longest permitted <see cref="Label"/>. Mirrored by the request validator and the column length.</summary>
    public const int LabelMaxLength = 120;

    /// <summary>The longest permitted <see cref="Note"/>.</summary>
    public const int NoteMaxLength = 1000;

    private SampleRecord(Guid id, string label, string? note)
        : base(id)
    {
        Label = label;
        Note = note;
    }

    // EF Core materialisation constructor. Label is populated from the row immediately after.
    private SampleRecord()
        : base() => Label = null!;

    /// <summary>A short human-readable label. Required.</summary>
    public string Label { get; private set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <inheritdoc />
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Creates a record, enforcing its invariants.
    /// </summary>
    /// <remarks>
    /// The request validator rejects malformed input at the boundary; this factory is the
    /// invariant of last resort, so the entity cannot exist in an invalid state even when
    /// constructed from a code path that skipped the pipeline. Both layers are intentional:
    /// the validator produces a good 422 for a client, this produces a guarantee for us.
    /// </remarks>
    /// <param name="id">The new identity. Prefer <see cref="Guid.CreateVersion7()"/>.</param>
    /// <param name="label">A short label. Required, trimmed, at most <see cref="LabelMaxLength"/> characters.</param>
    /// <param name="note">An optional note, at most <see cref="NoteMaxLength"/> characters.</param>
    public static Result<SampleRecord> Create(Guid id, string label, string? note)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<SampleRecord>(
                Error.Validation("sample_record.id_required", "Id must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return Result.Failure<SampleRecord>(
                Error.Validation("sample_record.label_required", "Label must not be empty."));
        }

        var trimmedLabel = label.Trim();
        if (trimmedLabel.Length > LabelMaxLength)
        {
            return Result.Failure<SampleRecord>(Error.Validation(
                "sample_record.label_too_long",
                $"Label must be at most {LabelMaxLength} characters."));
        }

        if (note is { Length: > NoteMaxLength })
        {
            return Result.Failure<SampleRecord>(Error.Validation(
                "sample_record.note_too_long",
                $"Note must be at most {NoteMaxLength} characters."));
        }

        return Result.Success(new SampleRecord(id, trimmedLabel, note));
    }
}
