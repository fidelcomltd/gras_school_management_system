using System.Text.RegularExpressions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Classes;

/// <summary>
/// A room under a <see cref="ClassLevel"/> for one <c>AcademicSession</c> (spec 6.4.3, 6.4.7): a
/// PER-SESSION record — Primary 2A in 2026/2027 and Primary 2A in 2027/2028 are different rows,
/// because everything an arm carries (roster, form teacher, capacity) changes annually. Lives in
/// <c>Domain/Classes/</c> alongside <see cref="ClassLevel"/> per TASK-0038's own note that TASK-0039
/// would join this namespace.
/// </summary>
/// <remarks>
/// <c>display_name</c> is NOT a property here — spec 6.4.3: "Never stored as text." See
/// <see cref="ArmDisplayName"/>, composed at read time by the Application layer, which has the owning
/// level's current name; this entity only knows its own <see cref="Label"/>.
/// </remarks>
public sealed partial class Arm : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.4.3: <c>label</c> is <c>String 16</c>.</summary>
    public const int LabelMaxLength = 16;

    /// <summary>Spec 6.4.3: capacity lower bound.</summary>
    public const int MinCapacity = 1;

    /// <summary>Spec 6.4.3: capacity upper bound.</summary>
    public const int MaxCapacity = 100;

    /// <summary>Spec 6.4.3: "Capacity... Defaults 30."</summary>
    public const int DefaultCapacity = 30;

    private Arm(Guid id, Guid classLevelId, Guid sessionId, string label, int capacity, Guid? formTeacherAdminId)
        : base(id)
    {
        ClassLevelId = classLevelId;
        SessionId = sessionId;
        Label = label;
        LabelKey = label.ToLowerInvariant();
        Capacity = capacity;
        FormTeacherAdminId = formTeacherAdminId;
        Status = ArmStatus.Active;
    }

    // EF Core materialisation constructor.
    private Arm()
        : base()
    {
        Label = null!;
        LabelKey = null!;
    }

    /// <summary>The owning level (spec 6.4.3). Must reference an ACTIVE level at creation time; the caller checks this.</summary>
    public Guid ClassLevelId { get; private set; }

    /// <summary>The owning session (spec 6.4.3, 6.4.7). Must be upcoming or active at creation time; the caller checks this.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>
    /// Letters, digits and single internal spaces, trimmed, 1..16 characters. A single alphanumeric
    /// character is normalised to uppercase on save; a multi-character label keeps the case as typed
    /// (spec 6.4.3, 6.4.8).
    /// </summary>
    public string Label { get; private set; }

    /// <summary>
    /// Lower-invariant projection of <see cref="Label"/>, backing the case-insensitive unique index on
    /// <c>(class_level_id, session_id, lower(label))</c> — same technique as <c>ClassLevel.NameKey</c>.
    /// </summary>
    public string LabelKey { get; private set; }

    /// <summary>1..100, defaults 30. A SOFT limit (spec 6.4.6) — never enforced as a hard block here.</summary>
    public int Capacity { get; private set; }

    /// <summary>
    /// Must reference an active admin account when set; not required at creation (spec 6.4.3). The
    /// arm keeps a stale reference if that account is later deactivated (spec 6.4.8) — this entity
    /// never clears it on its own.
    /// </summary>
    public Guid? FormTeacherAdminId { get; private set; }

    /// <summary>Active, inactive or closed (spec 6.4.3). Defaults active.</summary>
    public ArmStatus Status { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new, active arm. The caller must already have checked that <paramref name="classLevelId"/>
    /// references an active level, that <paramref name="sessionId"/> references an upcoming or active
    /// session, label uniqueness within that level and session, and (when given) that
    /// <paramref name="formTeacherAdminId"/> references an active admin account — none of those lookups
    /// is available to this entity.
    /// </summary>
    public static Result<Arm> Create(
        Guid id,
        Guid classLevelId,
        Guid sessionId,
        string label,
        int? capacity,
        Guid? formTeacherAdminId)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Arm>(Error.Validation("arm.id_required", "Id must not be empty."));
        }

        if (classLevelId == Guid.Empty)
        {
            return Result.Failure<Arm>(Error.Validation("arm.class_level_id_required", "ClassLevelId must not be empty."));
        }

        if (sessionId == Guid.Empty)
        {
            return Result.Failure<Arm>(Error.Validation("arm.session_id_required", "SessionId must not be empty."));
        }

        if (!TryNormalizeLabel(label, out var normalizedLabel, out var labelError))
        {
            return Result.Failure<Arm>(labelError);
        }

        var resolvedCapacity = capacity ?? DefaultCapacity;

        if (!TryValidateCapacity(resolvedCapacity, out var capacityError))
        {
            return Result.Failure<Arm>(capacityError);
        }

        return Result.Success(new Arm(id, classLevelId, sessionId, normalizedLabel, resolvedCapacity, formTeacherAdminId));
    }

    /// <summary>Relabels the arm. The caller must already have checked uniqueness and that the arm is mutable.</summary>
    public Result ChangeLabel(string label)
    {
        if (!TryNormalizeLabel(label, out var normalized, out var error))
        {
            return Result.Failure(error);
        }

        Label = normalized;
        LabelKey = normalized.ToLowerInvariant();
        return Result.Success();
    }

    /// <summary>
    /// Changes capacity (spec 6.4.6: a SOFT limit — reducing below current occupancy is allowed and
    /// removes nobody; no occupancy check happens here because enrolment does not exist yet, Phase 2).
    /// </summary>
    public Result ChangeCapacity(int capacity)
    {
        if (!TryValidateCapacity(capacity, out var error))
        {
            return Result.Failure(error);
        }

        Capacity = capacity;
        return Result.Success();
    }

    /// <summary>
    /// Sets or clears the form teacher. Whether the target account is active, and whether the caller
    /// holds <c>arm.formteacher.assign</c>, are both the caller's job (spec 6.4.3, 6.4.9).
    /// </summary>
    public void AssignFormTeacher(Guid? formTeacherAdminId) => FormTeacherAdminId = formTeacherAdminId;

    /// <summary>Rejoins active status (spec 6.4.7) — reversible, unlike <see cref="Close"/>.</summary>
    public void Activate() => Status = ArmStatus.Active;

    /// <summary>Hides the arm from new enrolment while keeping its roster and results readable (spec 6.4.7).</summary>
    public void Deactivate() => Status = ArmStatus.Inactive;

    /// <summary>
    /// Moves to <see cref="ArmStatus.Closed"/> — called ONLY as a side effect of the owning session
    /// closing (spec 6.4.7), never directly from a caller-supplied status value.
    /// </summary>
    public void Close() => Status = ArmStatus.Closed;

    /// <summary>
    /// Spec 6.4.7: "The arm becomes read-only: no enrolment, no transfer, no mark entry, no form
    /// teacher change" once <see cref="ArmStatus.Closed"/>. The caller invokes this before applying
    /// ANY mutation, including label, capacity and status changes.
    /// </summary>
    public Result EnsureMutable(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (Status == ArmStatus.Closed)
        {
            return Result.Failure(Error.Conflict(
                "arm.closed_immutable",
                $"{displayName} is closed because its session has closed and cannot be edited."));
        }

        return Result.Success();
    }

    private static bool TryValidateCapacity(int capacity, out Error error)
    {
        if (capacity is < MinCapacity or > MaxCapacity)
        {
            error = Error.Validation(
                "arm.capacity_out_of_range",
                $"Capacity must be between {MinCapacity} and {MaxCapacity}.");
            return false;
        }

        error = Error.None;
        return true;
    }

    private static bool TryNormalizeLabel(string label, out string normalized, out Error error)
    {
        ArgumentNullException.ThrowIfNull(label);

        var trimmed = label.Trim();

        if (trimmed.Length is < 1 or > LabelMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation(
                "arm.label_invalid_length",
                $"Label must be 1 to {LabelMaxLength} characters.");
            return false;
        }

        if (!LabelPattern().IsMatch(trimmed))
        {
            normalized = string.Empty;
            error = Error.Validation(
                "arm.label_invalid_characters",
                "Label may contain only letters, digits and single internal spaces.");
            return false;
        }

        // Spec 6.4.8: "Label entered as lowercase b: normalised to uppercase on save when the label is
        // a single letter. Multi-character labels keep the case as typed" — "Gold" stays "Gold".
        normalized = trimmed.Length == 1 ? trimmed.ToUpperInvariant() : trimmed;
        error = Error.None;
        return true;
    }

    // Letters/digits in runs of one or more, separated by exactly one space each — rejects leading,
    // trailing and doubled internal spaces without a separate trim-equality check.
    [GeneratedRegex(@"^[A-Za-z0-9]+( [A-Za-z0-9]+)*$")]
    private static partial Regex LabelPattern();
}
