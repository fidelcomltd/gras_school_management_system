using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Pupils;

/// <summary>Section E: a person the parent authorises to collect the child (spec 6.5.6). Zero or more, in the parent's order.</summary>
public sealed class AuthorisedPickupPerson : Entity<Guid>, IAuditableEntity
{
    private AuthorisedPickupPerson(Guid id, Guid pupilId, string fullName, string relationship, string phone, int displayOrder)
        : base(id)
    {
        PupilId = pupilId;
        FullName = fullName;
        Relationship = relationship;
        Phone = phone;
        DisplayOrder = displayOrder;
    }

    // EF Core materialisation constructor.
    private AuthorisedPickupPerson()
        : base()
    {
        FullName = string.Empty;
        Relationship = string.Empty;
        Phone = string.Empty;
    }

    /// <summary>The pupil.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Two words minimum.</summary>
    public string FullName { get; private set; }

    /// <summary>Free text.</summary>
    public string Relationship { get; private set; }

    /// <summary>Canonical <c>+234</c> form.</summary>
    public string Phone { get; private set; }

    /// <summary>The order the parent listed them.</summary>
    public int DisplayOrder { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>A validated row.</summary>
    public static Result<AuthorisedPickupPerson> Create(Guid id, Guid pupilId, string fullName, string relationship, string phone, int displayOrder)
    {
        if (!PersonFields.IsFullName(fullName))
        {
            return Result.Failure<AuthorisedPickupPerson>(Error.Validation("pickup.full_name_invalid", "Enter the person's full name: at least two words."));
        }

        if (PersonFields.Clean(relationship) is not { Length: <= PersonFields.RelationshipMaxLength } cleanRelationship)
        {
            return Result.Failure<AuthorisedPickupPerson>(Error.Validation("pickup.relationship_invalid", "Enter how this person is related to the pupil (at most 60 characters)."));
        }

        if (!NigerianPhoneNumber.TryNormalize(phone, out var canonicalPhone))
        {
            return Result.Failure<AuthorisedPickupPerson>(Error.Validation("pickup.phone_invalid", "Enter a Nigerian phone number, for example 08031234567."));
        }

        return Result.Success(new AuthorisedPickupPerson(id, pupilId, fullName.Trim(), cleanRelationship, canonicalPhone, displayOrder));
    }
}

/// <summary>
/// Section E's exclusion question, "Is there anyone who should NOT be allowed to collect the child?" (spec 6.5.6). One row
/// per pupil holding the explicit answer, so answered-no is distinguishable from never-asked (no row). The most sensitive
/// data in the system: <c>pupil.safeguarding.view</c> only, audited on every read, never exported, printed or sent to the portal.
/// </summary>
public sealed class BarredPersonAnswer : IAuditableEntity
{
    private BarredPersonAnswer(Guid pupilId, bool hasBarredPersons)
    {
        PupilId = pupilId;
        HasBarredPersons = hasBarredPersons;
    }

    // EF Core materialisation constructor.
    private BarredPersonAnswer()
    {
    }

    /// <summary>The pupil; also the key.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The explicit yes or no.</summary>
    public bool HasBarredPersons { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>A new answer row.</summary>
    public static BarredPersonAnswer Create(Guid pupilId, bool hasBarredPersons) => new(pupilId, hasBarredPersons);

    /// <summary>Changes the answer.</summary>
    public void Answer(bool hasBarredPersons) => HasBarredPersons = hasBarredPersons;
}

/// <summary>One person barred from collecting the child (spec 6.5.6). Present only when the answer is yes.</summary>
public sealed class BarredPerson : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.5.6: Text 500.</summary>
    public const int DetailsMaxLength = 500;

    private BarredPerson(Guid id, Guid pupilId, string fullName, string? details, int displayOrder)
        : base(id)
    {
        PupilId = pupilId;
        FullName = fullName;
        Details = details;
        DisplayOrder = displayOrder;
    }

    // EF Core materialisation constructor.
    private BarredPerson()
        : base()
    {
        FullName = string.Empty;
    }

    /// <summary>The pupil.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Required.</summary>
    public string FullName { get; private set; }

    /// <summary>The form's "name / relevant information" line.</summary>
    public string? Details { get; private set; }

    /// <summary>Entry order.</summary>
    public int DisplayOrder { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>A validated row. A single name is accepted here: a barred person may be known only by one.</summary>
    public static Result<BarredPerson> Create(Guid id, Guid pupilId, string fullName, string? details, int displayOrder)
    {
        if (PersonFields.Clean(fullName) is not { Length: <= PersonFields.FullNameMaxLength } cleanName)
        {
            return Result.Failure<BarredPerson>(Error.Validation("barred.full_name_required", "Enter the name of the person who must not collect the child."));
        }

        var cleanDetails = PersonFields.Clean(details);
        if (cleanDetails is { Length: > DetailsMaxLength })
        {
            return Result.Failure<BarredPerson>(Error.Validation("barred.details_too_long", "Details must be at most 500 characters."));
        }

        return Result.Success(new BarredPerson(id, pupilId, cleanName, cleanDetails, displayOrder));
    }
}
