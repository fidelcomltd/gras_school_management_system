using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Security;

/// <summary>
/// A named, admin-created set of privileges (spec 6.1.4). "Somebody has to be able to say that Mrs
/// Adeyemi may type marks for Primary 2A" — a role is the reusable set; <c>role_assignment</c>
/// (TASK-0030) is what actually grants it to an account with a scope.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsSystem"/> is <see langword="true"/> only for the seeded Super Admin role
/// (TASK-0028 dispatch 3) — never settable through <see cref="Create"/>, which always produces a
/// non-system role. "A system role cannot be edited, renamed, deleted or have privileges removed"
/// (spec 6.1.4) is enforced by the calling handler (a 409, before any other check), not here — this
/// entity has no way to know it is about to be mutated by an HTTP request versus the seeding step
/// that constructs the one system row directly.
/// </para>
/// <para>
/// Privilege ADDITION escalation (spec 6.1.7 rule 2 — "no account may add a privilege to a role
/// that it does not itself currently hold") is likewise not this entity's job: it needs the ACTOR's
/// held privileges, which this type never sees. See <see cref="RolePrivilegeEscalationGuard"/>.
/// </para>
/// </remarks>
public sealed class Role : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.1.4: <c>name</c> is <c>String 60</c>.</summary>
    public const int NameMaxLength = 60;

    /// <summary>Spec 6.1.4: <c>description</c> is <c>String 300</c>.</summary>
    public const int DescriptionMaxLength = 300;

    /// <summary>Spec 6.1.4: "Rejects the reserved name Super Admin on create," case-insensitive.</summary>
    public const string ReservedName = "Super Admin";

    private readonly List<string> _privileges = [];

    private Role(Guid id, string name, string? description, bool isSystem, IEnumerable<string> privileges)
        : base(id)
    {
        Name = name;
        NameKey = name.ToLowerInvariant();
        Description = description;
        IsSystem = isSystem;
        _privileges.AddRange(privileges);
        Status = RoleStatus.Active;
    }

    // EF Core materialisation constructor.
    private Role()
        : base()
    {
        Name = null!;
        NameKey = null!;
    }

    /// <summary>Display name, 1..60 characters (spec 6.1.4).</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Lower-invariant projection of <see cref="Name"/>, maintained alongside it. NOT part of the
    /// spec's field table — a persistence detail that lets the case-insensitive uniqueness rule
    /// (spec 6.1.4) be a plain unique index rather than a functional one, while <see cref="Name"/>
    /// itself keeps whatever casing the administrator typed.
    /// </summary>
    public string NameKey { get; private set; }

    /// <summary>Free text, 0..300 characters (spec 6.1.4).</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// True only for the seeded Super Admin role. "A system role cannot be edited, renamed, deleted
    /// or have privileges removed" (spec 6.1.4) — enforced by the calling handler, see the class remarks.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>
    /// Canonical privilege codes this role grants — never a <c>guardian.*</c> alias (resolved on the
    /// way in), at least one, sorted deterministically (spec 6.1.4).
    /// </summary>
    public IReadOnlyList<string> Privileges => _privileges;

    /// <summary>
    /// Active or archived (spec 6.1.4). "An archived role cannot be newly assigned but existing
    /// assignments continue until the session ends."
    /// </summary>
    public RoleStatus Status { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new, non-system, active role (spec 6.1.4). Privilege codes are alias-resolved,
    /// de-duplicated and sorted; an unrecognised code fails naming the offender.
    /// </summary>
    /// <param name="id">The new role's identifier.</param>
    /// <param name="name">1..60 characters, trimmed. Rejected if it is the reserved name, case-insensitive.</param>
    /// <param name="description">0..300 characters, or <see langword="null"/>.</param>
    /// <param name="privileges">
    /// The requested privilege codes. At least one is required; every code must resolve (after alias
    /// resolution) to an entry in <see cref="PrivilegeRegistry"/>.
    /// </param>
    public static Result<Role> Create(
        Guid id,
        string name,
        string? description,
        IReadOnlyCollection<string> privileges)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Role>(Error.Validation("role.id_required", "Id must not be empty."));
        }

        if (!TryNormalizeName(name, out var trimmedName, out var nameError))
        {
            return Result.Failure<Role>(nameError);
        }

        if (!TryNormalizeDescription(description, out var trimmedDescription, out var descriptionError))
        {
            return Result.Failure<Role>(descriptionError);
        }

        if (!TryNormalizePrivileges(privileges, out var normalizedPrivileges, out var privilegesError))
        {
            return Result.Failure<Role>(privilegesError);
        }

        return Result.Success(new Role(id, trimmedName, trimmedDescription, isSystem: false, normalizedPrivileges));
    }

    /// <summary>
    /// Constructs the one seeded system role directly, bypassing <see cref="Create"/>'s
    /// non-system-only guarantee (TASK-0028 dispatch 3, spec 4.5). Never reachable from an HTTP
    /// endpoint.
    /// </summary>
    /// <param name="id">The role's fixed, documented identifier.</param>
    /// <param name="name">Must be <see cref="ReservedName"/> — this factory exists only to seed it.</param>
    /// <param name="description">0..300 characters, or <see langword="null"/>.</param>
    /// <param name="privileges">Every code in the register — not validated for completeness here.</param>
    public static Result<Role> CreateSystemRole(
        Guid id,
        string name,
        string? description,
        IReadOnlyCollection<string> privileges)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Role>(Error.Validation("role.id_required", "Id must not be empty."));
        }

        if (!string.Equals(name.Trim(), ReservedName, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<Role>(Error.Validation(
                "role.system_role_name_invalid",
                $"The system role must be named '{ReservedName}'."));
        }

        if (!TryNormalizeDescription(description, out var trimmedDescription, out var descriptionError))
        {
            return Result.Failure<Role>(descriptionError);
        }

        if (!TryNormalizePrivileges(privileges, out var normalizedPrivileges, out var privilegesError))
        {
            return Result.Failure<Role>(privilegesError);
        }

        return Result.Success(new Role(id, name.Trim(), trimmedDescription, isSystem: true, normalizedPrivileges));
    }

    /// <summary>
    /// Renames and/or redescribes the role (spec 6.1.9's edit flow). The caller must have already
    /// rejected this on an <see cref="IsSystem"/> role (409, before this is ever invoked) and checked
    /// name uniqueness (needs a repository lookup this entity cannot perform).
    /// </summary>
    public Result Rename(string name, string? description)
    {
        if (!TryNormalizeName(name, out var trimmedName, out var nameError))
        {
            return Result.Failure(nameError);
        }

        if (!TryNormalizeDescription(description, out var trimmedDescription, out var descriptionError))
        {
            return Result.Failure(descriptionError);
        }

        Name = trimmedName;
        NameKey = trimmedName.ToLowerInvariant();
        Description = trimmedDescription;
        return Result.Success();
    }

    /// <summary>
    /// Replaces the privilege set (spec 6.1.4). Escalation (rule 2) and the system-role guard are the
    /// caller's job — this only re-validates non-emptiness and register membership.
    /// </summary>
    public Result SetPrivileges(IReadOnlyCollection<string> privileges)
    {
        if (!TryNormalizePrivileges(privileges, out var normalized, out var error))
        {
            return Result.Failure(error);
        }

        _privileges.Clear();
        _privileges.AddRange(normalized);
        return Result.Success();
    }

    /// <summary>Moves between <see cref="RoleStatus.Active"/> and <see cref="RoleStatus.Archived"/>.</summary>
    public void ChangeStatus(RoleStatus status) => Status = status;

    private static bool TryNormalizeName(string name, out string normalized, out Error error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            normalized = string.Empty;
            error = Error.Validation("role.name_required", "Name must not be empty.");
            return false;
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation("role.name_too_long", $"Name must be at most {NameMaxLength} characters.");
            return false;
        }

        if (string.Equals(trimmed, ReservedName, StringComparison.OrdinalIgnoreCase))
        {
            normalized = string.Empty;
            error = Error.Validation(
                "role.name_reserved",
                $"'{ReservedName}' is a reserved role name and cannot be used.");
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
                "role.description_too_long",
                $"Description must be at most {DescriptionMaxLength} characters.");
            return false;
        }

        normalized = trimmed.Length == 0 ? null : trimmed;
        error = Error.None;
        return true;
    }

    private static bool TryNormalizePrivileges(
        IReadOnlyCollection<string> privileges,
        out IReadOnlyList<string> normalized,
        out Error error)
    {
        ArgumentNullException.ThrowIfNull(privileges);

        if (privileges.Count == 0)
        {
            normalized = [];
            error = Error.Validation("role.privileges_empty", "A role must have at least one privilege.");
            return false;
        }

        var unknown = new List<string>();
        var resolved = new List<string>();

        foreach (var raw in privileges)
        {
            var canonical = PrivilegeAliases.Resolve(raw);

            if (!PrivilegeRegistry.TryGet(canonical, out _))
            {
                unknown.Add(raw);
                continue;
            }

            resolved.Add(canonical);
        }

        if (unknown.Count > 0)
        {
            normalized = [];

            var distinctUnknown = unknown
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
            var joined = string.Join(", ", distinctUnknown);

            error = Error.Validation(
                "role.unknown_privilege",
                distinctUnknown.Length == 1
                    ? $"'{joined}' is not a recognised privilege code."
                    : $"'{joined}' are not recognised privilege codes.");
            return false;
        }

        normalized = resolved
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        error = Error.None;
        return true;
    }
}
