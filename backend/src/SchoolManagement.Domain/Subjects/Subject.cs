using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Subjects;

/// <summary>
/// A subject (spec 6.6.2) — "the subjects in effect for an arm in a term are the rows of every
/// result sheet produced for that arm that term. Nothing else in the product decides those rows."
/// </summary>
/// <remarks>
/// <see cref="Code"/> is NULLABLE, unseeded and NOT required on creation — a deliberate departure
/// from spec 6.6.2's <c>Req: Yes</c>, human-ruled 2026-09-16 (TASK-0070 delta amendment 1). Nothing
/// prints a code today; the one data contract that names it
/// (<c>18-appendix-c-result-sheet-contract.md:47</c>) declares it optional.
/// </remarks>
public sealed class Subject : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.6.2: <c>name</c> is <c>String 80</c>.</summary>
    public const int NameMaxLength = 80;

    /// <summary>Spec 6.6.2: <c>code</c> is <c>String 12</c>.</summary>
    public const int CodeMaxLength = 12;

    /// <summary>Spec 6.6.2: <c>description</c> is <c>String 300</c>.</summary>
    public const int DescriptionMaxLength = 300;

    private Subject(Guid id, string name, string? code, string? description)
        : base(id)
    {
        Name = name;
        NameKey = name.ToLowerInvariant();
        Code = code;
        CodeKey = code?.ToLowerInvariant();
        Description = description;
        Status = SubjectStatus.Active;
    }

    // EF Core materialisation constructor.
    private Subject()
        : base()
    {
        Name = null!;
        NameKey = null!;
    }

    /// <summary>Display name, unique case-insensitive (spec 6.6.2).</summary>
    public string Name { get; private set; }

    /// <summary>Lower-invariant projection of <see cref="Name"/> — same technique as <c>ClassLevel.NameKey</c>.</summary>
    public string NameKey { get; private set; }

    /// <summary>
    /// <see langword="null"/> unless supplied. Uppercase letters and digits when supplied; unique
    /// where supplied (never blocks on the seed, which carries none — see the type remarks).
    /// </summary>
    public string? Code { get; private set; }

    /// <summary>Lower-invariant projection of <see cref="Code"/>, or <see langword="null"/>.</summary>
    public string? CodeKey { get; private set; }

    /// <summary>Free text, for the administrator's benefit only. Never printed (spec 6.6.2).</summary>
    public string? Description { get; private set; }

    /// <summary>Active or inactive (spec 6.6.2). Defaults active.</summary>
    public SubjectStatus Status { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new, active subject. The caller must already have checked name uniqueness and, when
    /// <paramref name="code"/> is supplied, code uniqueness — neither lookup is available here.
    /// </summary>
    public static Result<Subject> Create(Guid id, string name, string? code, string? description)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Subject>(Error.Validation("subject.id_required", "Id must not be empty."));
        }

        if (!TryNormalizeName(name, out var trimmedName, out var nameError))
        {
            return Result.Failure<Subject>(nameError);
        }

        if (!TryNormalizeCode(code, out var normalizedCode, out var codeError))
        {
            return Result.Failure<Subject>(codeError);
        }

        if (!TryNormalizeDescription(description, out var normalizedDescription, out var descriptionError))
        {
            return Result.Failure<Subject>(descriptionError);
        }

        return Result.Success(new Subject(id, trimmedName, normalizedCode, normalizedDescription));
    }

    /// <summary>Edits name, code and description (spec 6.6.2, 6.6.9). The caller must already have checked uniqueness.</summary>
    public Result Edit(string name, string? code, string? description)
    {
        if (!TryNormalizeName(name, out var trimmedName, out var nameError))
        {
            return Result.Failure(nameError);
        }

        if (!TryNormalizeCode(code, out var normalizedCode, out var codeError))
        {
            return Result.Failure(codeError);
        }

        if (!TryNormalizeDescription(description, out var normalizedDescription, out var descriptionError))
        {
            return Result.Failure(descriptionError);
        }

        Name = trimmedName;
        NameKey = trimmedName.ToLowerInvariant();
        Code = normalizedCode;
        CodeKey = normalizedCode?.ToLowerInvariant();
        Description = normalizedDescription;
        return Result.Success();
    }

    /// <summary>Rejoins selectability for new mappings and exceptions (spec 6.6.6).</summary>
    public void Activate() => Status = SubjectStatus.Active;

    /// <summary>
    /// Stops new mappings and new exceptions only — every existing mapping stays exactly as it was
    /// (spec 6.6.6).
    /// </summary>
    public void Deactivate() => Status = SubjectStatus.Inactive;

    private static bool TryNormalizeName(string name, out string normalized, out Error error)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();

        if (trimmed.Length == 0 || trimmed.Length > NameMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation(
                "subject.name_invalid_length", $"Name must be 1 to {NameMaxLength} characters.");
            return false;
        }

        normalized = trimmed;
        error = Error.None;
        return true;
    }

    private static bool TryNormalizeCode(string? code, out string? normalized, out Error error)
    {
        if (code is null)
        {
            normalized = null;
            error = Error.None;
            return true;
        }

        var trimmed = code.Trim();

        if (trimmed.Length == 0)
        {
            // An empty string clears the code, same "empty clears it" convention as
            // UpdateArmCommand.FormTeacherAdminId.
            normalized = null;
            error = Error.None;
            return true;
        }

        if (trimmed.Length > CodeMaxLength)
        {
            normalized = null;
            error = Error.Validation(
                "subject.code_invalid_length", $"Code must be at most {CodeMaxLength} characters.");
            return false;
        }

        if (!trimmed.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character)))
        {
            normalized = null;
            error = Error.Validation(
                "subject.code_invalid_characters", "Code may contain only uppercase letters and digits.");
            return false;
        }

        normalized = trimmed;
        error = Error.None;
        return true;
    }

    private static bool TryNormalizeDescription(string? description, out string? normalized, out Error error)
    {
        if (description is null)
        {
            normalized = null;
            error = Error.None;
            return true;
        }

        var trimmed = description.Trim();

        if (trimmed.Length > DescriptionMaxLength)
        {
            normalized = null;
            error = Error.Validation(
                "subject.description_invalid_length",
                $"Description must be at most {DescriptionMaxLength} characters.");
            return false;
        }

        normalized = trimmed.Length == 0 ? null : trimmed;
        error = Error.None;
        return true;
    }
}
