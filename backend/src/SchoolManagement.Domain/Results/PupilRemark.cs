using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One pupil's class-teacher or head-teacher remark, for one result set (spec 09 §6.7.3's
/// <c>pupil_remark</c>; appendix C.6; TASK-0086 stage A). Free text, never converted to a mark and
/// never computed — the same posture <see cref="TraitRating"/> takes.
/// </summary>
/// <remarks>
/// <para>
/// UNIQUE ON <c>(result_set_id, pupil_id, kind)</c> — one remark of each kind per pupil per result
/// set. A cleared remark (empty/whitespace text on save) is a row DELETE, same convention
/// <see cref="TraitRating"/> uses for an explicit-null cell, so there is never a row holding empty
/// text to confuse a completeness count.
/// </para>
/// <para>
/// <see cref="WrittenByName"/> and <see cref="WrittenAtUtc"/> are a snapshot, captured at the
/// moment the TEXT actually changes — appendix C.6: "captured at the time of writing so a staff
/// change later does not rewrite an issued sheet." <see cref="UpdateText"/> enforces this itself:
/// saving the same text again (whether by the same admin or another) leaves the snapshot alone.
/// </para>
/// </remarks>
public sealed class PupilRemark : Entity<Guid>, IAuditableEntity
{
    /// <summary>Appendix C.6, F, E.5: 300 characters, superseding spec §6.7.7's 240 (ruling L, 2026-09-19; drift).</summary>
    public const int TextMaxLength = 300;

    private PupilRemark(
        Guid id, Guid resultSetId, Guid pupilId, RemarkKind kind, string text,
        Guid writtenByAdminId, string writtenByName, DateTimeOffset writtenAtUtc)
        : base(id)
    {
        ResultSetId = resultSetId;
        PupilId = pupilId;
        Kind = kind;
        Text = text;
        WrittenByAdminId = writtenByAdminId;
        WrittenByName = writtenByName;
        WrittenAtUtc = writtenAtUtc;
    }

    // EF Core materialisation constructor.
    private PupilRemark()
        : base()
    {
    }

    /// <summary>The result set this remark belongs to.</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>Unique together with <see cref="Kind"/> within <see cref="ResultSetId"/>.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Class teacher's or head teacher's remark. Immutable once created — see <see cref="RemarkKind"/>.</summary>
    public RemarkKind Kind { get; private set; }

    /// <summary>The remark text, 1-<see cref="TextMaxLength"/> characters, already trimmed. Never empty — an empty remark is a deleted row.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>The admin account that most recently wrote a CHANGED text.</summary>
    public Guid WrittenByAdminId { get; private set; }

    /// <summary>The staff name snapshot at the moment the text last actually changed (appendix C.6).</summary>
    public string WrittenByName { get; private set; } = string.Empty;

    /// <summary>When the text last actually changed.</summary>
    public DateTimeOffset WrittenAtUtc { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>Creates a new remark row. The caller has already validated every rule — this entity trusts its input, same posture as <see cref="TraitRating.Create"/>.</summary>
    public static Result<PupilRemark> Create(
        Guid id, Guid resultSetId, Guid pupilId, RemarkKind kind, string text,
        Guid writtenByAdminId, string writtenByName, DateTimeOffset writtenAtUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<PupilRemark>(Error.Validation("pupil_remark.id_required", "Id must not be empty."));
        }

        if (resultSetId == Guid.Empty || pupilId == Guid.Empty || writtenByAdminId == Guid.Empty)
        {
            return Result.Failure<PupilRemark>(Error.Validation(
                "pupil_remark.reference_required", "ResultSetId, PupilId and WrittenByAdminId must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length > TextMaxLength)
        {
            return Result.Failure<PupilRemark>(Error.Validation(
                "pupil_remark.text_invalid", $"Text must be between 1 and {TextMaxLength} characters."));
        }

        return Result.Success(new PupilRemark(id, resultSetId, pupilId, kind, text, writtenByAdminId, writtenByName, writtenAtUtc));
    }

    /// <summary>
    /// Applies new text to an EXISTING row. The caller has already validated it. Rewrites
    /// <see cref="WrittenByAdminId"/>, <see cref="WrittenByName"/> and <see cref="WrittenAtUtc"/>
    /// ONLY when <paramref name="text"/> differs from the current <see cref="Text"/> (appendix
    /// C.6) — a no-op resave by a different admin, or the same admin resaving unchanged text,
    /// leaves the original attribution alone. Returns whether the text actually changed, so the
    /// caller knows whether to audit and re-hash the sheet version.
    /// </summary>
    public bool UpdateText(string text, Guid writtenByAdminId, string writtenByName, DateTimeOffset writtenAtUtc)
    {
        if (string.Equals(Text, text, StringComparison.Ordinal))
        {
            return false;
        }

        Text = text;
        WrittenByAdminId = writtenByAdminId;
        WrittenByName = writtenByName;
        WrittenAtUtc = writtenAtUtc;
        return true;
    }
}
