using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One saved phrase a class teacher or head teacher can pick from when writing a remark (spec
/// §6.7.7 delta item 4; TASK-0086 stage B). Inserted text is COPIED into the remark at the moment
/// it is picked — this row is never referenced afterwards, which is why it is hard-deleted rather
/// than soft-deleted or archived.
/// </summary>
/// <remarks>
/// UNIQUE ON <c>(kind, text_key)</c> — a duplicate is judged trimmed and case-insensitive within
/// the same <see cref="RemarkKind"/> only; the same phrase is allowed once per kind, and the two
/// kinds' lists never collide with each other. <see cref="TextKey"/> is the stored, lower-invariant
/// comparison key — same convention <c>Arm.LabelKey</c> uses — rather than a live
/// <c>LOWER(text)</c> computation at query time.
/// </remarks>
public sealed class RemarkTemplate : Entity<Guid>, IAuditableEntity
{
    /// <summary>Delta item 4: "1-300 characters, else 422."</summary>
    public const int TextMinLength = 1;

    /// <summary>Delta item 4: "1-300 characters, else 422."</summary>
    public const int TextMaxLength = 300;

    private RemarkTemplate(Guid id, RemarkKind kind, string text)
        : base(id)
    {
        Kind = kind;
        Text = text;
        TextKey = text.ToLowerInvariant();
    }

    // EF Core materialisation constructor.
    private RemarkTemplate()
        : base()
    {
    }

    /// <summary>Which template list this phrase belongs to. Immutable — editing in place is out of scope (delete and re-add).</summary>
    public RemarkKind Kind { get; private set; }

    /// <summary>The phrase as typed, trimmed. 1-<see cref="TextMaxLength"/> characters.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Lower-invariant comparison key, stored so the duplicate check and its unique index never re-derive it at query time.</summary>
    public string TextKey { get; private set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new template. The caller has already checked for a duplicate (data-dependent, spec
    /// delta item 4) — this factory only enforces length, the same "entity trusts its input for
    /// anything data-dependent" posture <see cref="PupilRemark.Create"/> takes.
    /// </summary>
    public static Result<RemarkTemplate> Create(Guid id, RemarkKind kind, string text)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<RemarkTemplate>(Error.Validation("remark_template.id_required", "Id must not be empty."));
        }

        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length < TextMinLength || trimmed.Length > TextMaxLength)
        {
            return Result.Failure<RemarkTemplate>(Error.Validation(
                "remark_template.text_invalid", $"Text must be between {TextMinLength} and {TextMaxLength} characters."));
        }

        return Result.Success(new RemarkTemplate(id, kind, trimmed));
    }
}
