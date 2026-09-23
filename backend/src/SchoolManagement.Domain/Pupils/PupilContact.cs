using System.Net.Mail;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Pupils;

/// <summary>The five contact slots of the admission form (spec 6.5.5), sections C and D.</summary>
public enum ContactRole
{
    /// <summary>Section C.</summary>
    Father,

    /// <summary>Section C.</summary>
    Mother,

    /// <summary>Section C, "if applicable".</summary>
    Guardian,

    /// <summary>Section D. Required for approval.</summary>
    EmergencyPrimary,

    /// <summary>Section D. Optional but prompted.</summary>
    EmergencyAlternate,
}

/// <summary>Shared checks for the people recorded against a pupil (contacts, pickup and barred persons).</summary>
public static class PersonFields
{
    /// <summary>Spec 6.5.5/6.5.6: String 120.</summary>
    public const int FullNameMaxLength = 120;

    /// <summary>Spec 6.5.5/6.5.6: String 60.</summary>
    public const int RelationshipMaxLength = 60;

    /// <summary>"Two words minimum" (spec 6.5.5).</summary>
    public static bool IsFullName(string? value) =>
        value is not null && value.Trim().Length <= FullNameMaxLength
        && value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= 2;

    /// <summary>Trims, and turns blank into null.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// One contact for a pupil (spec 6.5.5): father, mother, guardian, or one of the two emergency contacts. At most one row
/// per role per pupil. The phone is stored in canonical <c>+234</c> form.
/// </summary>
public sealed class PupilContact : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.5.5: String 80.</summary>
    public const int OccupationMaxLength = 80;

    /// <summary>Spec 6.5.5: String 160.</summary>
    public const int EmailMaxLength = 160;

    private PupilContact(Guid id, Guid pupilId, ContactRole role)
        : base(id)
    {
        PupilId = pupilId;
        Role = role;
        FullName = string.Empty;
        Phone = string.Empty;
    }

    // EF Core materialisation constructor.
    private PupilContact()
        : base()
    {
        FullName = string.Empty;
        Phone = string.Empty;
    }

    /// <summary>The pupil.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Which slot.</summary>
    public ContactRole Role { get; private set; }

    /// <summary>Two words minimum.</summary>
    public string FullName { get; private set; }

    /// <summary>Required for guardian and the emergency roles; null for father and mother.</summary>
    public string? Relationship { get; private set; }

    /// <summary>Canonical <c>+234</c> form.</summary>
    public string Phone { get; private set; }

    /// <summary>Father and mother only.</summary>
    public string? WhatsappNumber { get; private set; }

    /// <summary>Father and mother only.</summary>
    public string? Occupation { get; private set; }

    /// <summary>Optional.</summary>
    public string? Email { get; private set; }

    /// <summary>The person the school telephones first. Exactly one per pupil, and never an emergency row.</summary>
    public bool IsPrimaryContact { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>Whether <paramref name="role"/> is a responsible adult (section C) rather than an emergency contact.</summary>
    public static bool IsResponsibleAdult(ContactRole role) => role is ContactRole.Father or ContactRole.Mother or ContactRole.Guardian;

    /// <summary>A new, empty contact row; <see cref="Apply"/> fills it.</summary>
    public static PupilContact Create(Guid id, Guid pupilId, ContactRole role) => new(id, pupilId, role);

    /// <summary>Validates and writes every field. Father and mother drop relationship; the others drop WhatsApp and occupation.</summary>
    public Result Apply(string fullName, string? relationship, string phone, string? whatsappNumber, string? occupation, string? email, bool isPrimary)
    {
        if (!PersonFields.IsFullName(fullName))
        {
            return Result.Failure(Error.Validation("contact.full_name_invalid", "Enter the contact's full name: at least two words, at most 120 characters."));
        }

        var parent = Role is ContactRole.Father or ContactRole.Mother;
        var cleanRelationship = parent ? null : PersonFields.Clean(relationship);
        if (!parent && cleanRelationship is null)
        {
            return Result.Failure(Error.Validation("contact.relationship_required", "Enter how this person is related to the pupil."));
        }

        if (cleanRelationship is { Length: > PersonFields.RelationshipMaxLength })
        {
            return Result.Failure(Error.Validation("contact.relationship_too_long", "Relationship must be at most 60 characters."));
        }

        if (!NigerianPhoneNumber.TryNormalize(phone, out var canonicalPhone))
        {
            return Result.Failure(Error.Validation("contact.phone_invalid", "Enter a Nigerian phone number, for example 08031234567."));
        }

        string? canonicalWhatsapp = null;
        if (parent && PersonFields.Clean(whatsappNumber) is { } whatsapp)
        {
            if (!NigerianPhoneNumber.TryNormalize(whatsapp, out var normalised))
            {
                return Result.Failure(Error.Validation("contact.whatsapp_invalid", "Enter the WhatsApp number as a Nigerian phone number."));
            }

            canonicalWhatsapp = normalised;
        }

        var cleanOccupation = parent ? PersonFields.Clean(occupation) : null;
        if (cleanOccupation is { Length: > OccupationMaxLength })
        {
            return Result.Failure(Error.Validation("contact.occupation_too_long", "Occupation must be at most 80 characters."));
        }

        var cleanEmail = PersonFields.Clean(email);
        if (cleanEmail is not null && (cleanEmail.Length > EmailMaxLength || !MailAddress.TryCreate(cleanEmail, out _)))
        {
            return Result.Failure(Error.Validation("contact.email_invalid", "Enter a valid email address, or leave it blank."));
        }

        if (isPrimary && !IsResponsibleAdult(Role))
        {
            return Result.Failure(Error.Validation("contact.primary_not_adult", "The primary contact must be the father, mother or guardian."));
        }

        FullName = fullName.Trim();
        Relationship = cleanRelationship;
        Phone = canonicalPhone;
        WhatsappNumber = canonicalWhatsapp;
        Occupation = cleanOccupation;
        Email = cleanEmail;
        IsPrimaryContact = isPrimary;
        return Result.Success();
    }
}
