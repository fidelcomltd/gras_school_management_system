using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Classes;

/// <summary>
/// A year group (spec 6.4.1, 6.4.2): persists across sessions and sits in an ordered progression
/// chain. Lives in <c>Domain/Classes/</c> rather than <c>Domain/Levels/</c> deliberately — TASK-0039's
/// <c>Arm</c> joins this same namespace.
/// </summary>
/// <remarks>
/// <para>
/// <c>IsEntryLevel</c>/<c>IsGraduatingLevel</c> are NOT properties on this type. Spec 6.4.2: "Not
/// stored. Computed as the active level that no other active level points at" — storing them is the
/// same class of defect 6.4.3 forbids for <c>Arm.display_name</c>. They are computed at read time,
/// over the full active set, by <c>LevelMapper</c> in the Application layer (this entity
/// cannot see its siblings).
/// </para>
/// <para>
/// Cross-entity chain integrity (no self-reference aside, which IS local and checked below) is
/// <see cref="ProgressionChainGuard"/>'s job, not this type's — validating rules 2 through 8 needs the
/// whole active set, which a single entity never has.
/// </para>
/// </remarks>
public sealed class ClassLevel : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.4.2: <c>name</c> is <c>String 40</c>, minimum 2.</summary>
    public const int NameMaxLength = 40;

    /// <summary>Spec 6.4.2: <c>name</c> is <c>String 40</c>, minimum 2.</summary>
    public const int NameMinLength = 2;

    private ClassLevel(Guid id, string name, Guid sectionId, int progressionOrder, Guid? nextLevelId)
        : base(id)
    {
        Name = name;
        NameKey = name.ToLowerInvariant();
        SectionId = sectionId;
        ProgressionOrder = progressionOrder;
        NextLevelId = nextLevelId;
        Status = LevelStatus.Active;
    }

    // EF Core materialisation constructor.
    private ClassLevel()
        : base()
    {
        Name = null!;
        NameKey = null!;
    }

    /// <summary>Display name, 2..40 characters, trimmed (spec 6.4.2).</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Lower-invariant projection of <see cref="Name"/> — persistence detail for the case-insensitive
    /// unique index, same technique as <c>Role.NameKey</c>.
    /// </summary>
    public string NameKey { get; private set; }

    /// <summary>The owning <see cref="Section"/> (spec 6.4.2). The chain is not scoped by section.</summary>
    public Guid SectionId { get; private set; }

    /// <summary>1 upward. Unique across ACTIVE levels only (spec 6.4.2) — a database partial index enforces it.</summary>
    public int ProgressionOrder { get; private set; }

    /// <summary>
    /// <see langword="null"/> on exactly one active level, the graduating level (spec 6.4.2). Never
    /// equal to <see cref="Entity{TId}.Id"/> (rule 1, enforced locally below) — every other chain rule
    /// needs the full active set and lives in <see cref="ProgressionChainGuard"/> instead.
    /// </summary>
    public Guid? NextLevelId { get; private set; }

    /// <summary>Active or inactive (spec 6.4.2). Defaults active.</summary>
    public LevelStatus Status { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new, active level. The caller must already have checked name uniqueness and that
    /// <paramref name="sectionId"/> references a real section — this entity cannot perform either
    /// lookup. Cross-entity chain rules (2 through 8) are the caller's job too, via
    /// <see cref="ProgressionChainGuard"/>, run over the full active set after this succeeds.
    /// </summary>
    public static Result<ClassLevel> Create(
        Guid id,
        string name,
        Guid sectionId,
        int progressionOrder,
        Guid? nextLevelId)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<ClassLevel>(Error.Validation("level.id_required", "Id must not be empty."));
        }

        if (!TryNormalizeName(name, out var trimmed, out var nameError))
        {
            return Result.Failure<ClassLevel>(nameError);
        }

        if (progressionOrder < 1)
        {
            return Result.Failure<ClassLevel>(Error.Validation(
                "level.progression_order_invalid",
                "Progression order must be 1 or greater."));
        }

        if (nextLevelId == id)
        {
            return Result.Failure<ClassLevel>(SelfReferenceError(trimmed));
        }

        return Result.Success(new ClassLevel(id, trimmed, sectionId, progressionOrder, nextLevelId));
    }

    /// <summary>Renames the level. The caller must already have checked name uniqueness.</summary>
    public Result Rename(string name)
    {
        if (!TryNormalizeName(name, out var trimmed, out var error))
        {
            return Result.Failure(error);
        }

        Name = trimmed;
        NameKey = trimmed.ToLowerInvariant();
        return Result.Success();
    }

    /// <summary>Moves the level to a different section (spec 6.4.2, 6.4.9). Existence is the caller's job.</summary>
    public void ChangeSection(Guid sectionId) => SectionId = sectionId;

    /// <summary>
    /// Points this level at <paramref name="nextLevelId"/>, or clears it (<see langword="null"/>) to
    /// make it a graduating candidate. Only rule 1 (no self-reference) is checked here — every other
    /// chain rule needs the full active set (<see cref="ProgressionChainGuard"/>).
    /// </summary>
    public Result SetNextLevel(Guid? nextLevelId)
    {
        if (nextLevelId == Id)
        {
            return Result.Failure(SelfReferenceError(Name));
        }

        NextLevelId = nextLevelId;
        return Result.Success();
    }

    /// <summary>Directly sets the progression order (spec 6.4.2: "editable directly... for the administrator who prefers typing").</summary>
    public void SetProgressionOrder(int progressionOrder) => ProgressionOrder = progressionOrder;

    /// <summary>Rejoins the active chain (spec 6.4.2).</summary>
    public void Activate() => Status = LevelStatus.Active;

    /// <summary>
    /// Removes the level from the active chain (spec 6.4.2). The caller must already have re-run
    /// <see cref="ProgressionChainGuard"/> over the remaining active levels — this entity has no way
    /// to know whether doing so would strand a sibling.
    /// </summary>
    public void Deactivate() => Status = LevelStatus.Inactive;

    private static Error SelfReferenceError(string name) =>
        Error.Validation("level.chain_self_reference", $"{name} cannot be its own next level.");

    private static bool TryNormalizeName(string name, out string normalized, out Error error)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();

        if (trimmed.Length < NameMinLength || trimmed.Length > NameMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation(
                "level.name_invalid_length",
                $"Name must be {NameMinLength} to {NameMaxLength} characters.");
            return false;
        }

        normalized = trimmed;
        error = Error.None;
        return true;
    }
}
