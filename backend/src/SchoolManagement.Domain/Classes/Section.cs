using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Classes;

/// <summary>
/// The admin-editable section list a <see cref="ClassLevel"/> belongs to (spec 6.4.2, 6.4.9). Seeded
/// with Nursery and Primary; a school may add more. Deliberately tiny — no status, no delete route
/// (spec 6.4.9 lists only <c>GET</c>/<c>POST</c>/<c>PATCH</c> for sections).
/// </summary>
/// <remarks>
/// Spec gives no explicit length/uniqueness rule for a section's own <c>name</c> (only for a level's
/// name) — <see cref="NameMaxLength"/> and the case-insensitive-unique rule below are this card's own
/// reasonable default, mirroring <c>Role.Name</c>'s equivalent field. Recorded as an assumption
/// (<c>backend/docs/ASSUMPTIONS.md</c> §2), not a stop-and-ask: nothing in spec 6.4 suggests a
/// school would ever need a section name longer than a level's own.
/// </remarks>
public sealed class Section : Entity<Guid>, IAuditableEntity
{
    /// <summary>See the type remarks — not spec-fixed, chosen to match <see cref="ClassLevel.NameMaxLength"/>.</summary>
    public const int NameMaxLength = 40;

    private Section(Guid id, string name)
        : base(id)
    {
        Name = name;
        NameKey = name.ToLowerInvariant();
    }

    // EF Core materialisation constructor.
    private Section()
        : base()
    {
        Name = null!;
        NameKey = null!;
    }

    /// <summary>Display name, 2..40 characters, trimmed.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Lower-invariant projection of <see cref="Name"/>, a persistence detail letting case-insensitive
    /// uniqueness be a plain unique index — same technique as <c>Role.NameKey</c>.
    /// </summary>
    public string NameKey { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>Creates a new section. The caller must already have checked name uniqueness.</summary>
    public static Result<Section> Create(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Section>(Error.Validation("section.id_required", "Id must not be empty."));
        }

        if (!TryNormalizeName(name, out var trimmed, out var error))
        {
            return Result.Failure<Section>(error);
        }

        return Result.Success(new Section(id, trimmed));
    }

    /// <summary>Renames the section. The caller must already have checked name uniqueness.</summary>
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

    private static bool TryNormalizeName(string name, out string normalized, out Error error)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();

        if (trimmed.Length is < 2 or > NameMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation(
                "section.name_invalid_length",
                $"Name must be 2 to {NameMaxLength} characters.");
            return false;
        }

        normalized = trimmed;
        error = Error.None;
        return true;
    }
}
