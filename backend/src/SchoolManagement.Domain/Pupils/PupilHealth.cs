using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Pupils;

/// <summary>Spec 6.5.7: the eight blood groups. Free text is not accepted.</summary>
public enum BloodGroup
{
    /// <summary>A+.</summary>
    APositive,

    /// <summary>A-.</summary>
    ANegative,

    /// <summary>B+.</summary>
    BPositive,

    /// <summary>B-.</summary>
    BNegative,

    /// <summary>AB+.</summary>
    AbPositive,

    /// <summary>AB-.</summary>
    AbNegative,

    /// <summary>O+.</summary>
    OPositive,

    /// <summary>O-.</summary>
    ONegative,
}

/// <summary>Spec 6.5.7: the five genotypes.</summary>
public enum Genotype
{
    /// <summary>AA.</summary>
    AA,

    /// <summary>AS.</summary>
    AS,

    /// <summary>SS.</summary>
    SS,

    /// <summary>AC.</summary>
    AC,

    /// <summary>SC.</summary>
    SC,
}

/// <summary>
/// Section F, health and safety (spec 6.5.7). One row per pupil, created on the first save. The three yes-or-no questions
/// are nullable: null is "not asked yet", which is not the same as No, and approval requires all three answered.
/// <c>pupil.safeguarding.view</c> only; never on a result sheet, weekly report, the portal or a general export.
/// </summary>
public sealed class PupilHealth : IAuditableEntity
{
    /// <summary>Spec 6.5.7: Text 500.</summary>
    public const int ShortDetailsMaxLength = 500;

    /// <summary>Spec 6.5.7: Text 1000.</summary>
    public const int LongDetailsMaxLength = 1000;

    /// <summary>Spec 6.5.7: String 160.</summary>
    public const int HospitalMaxLength = 160;

    private PupilHealth(Guid pupilId) => PupilId = pupilId;

    // EF Core materialisation constructor.
    private PupilHealth()
    {
    }

    /// <summary>The pupil; also the key.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Null until asked.</summary>
    public bool? HasAllergy { get; private set; }

    /// <summary>Required where <see cref="HasAllergy"/> is true.</summary>
    public string? AllergyDetails { get; private set; }

    /// <summary>Null until asked.</summary>
    public bool? HasMedicalCondition { get; private set; }

    /// <summary>Required where <see cref="HasMedicalCondition"/> is true.</summary>
    public string? MedicalConditionDetails { get; private set; }

    /// <summary>Null until asked.</summary>
    public bool? TakesRegularMedication { get; private set; }

    /// <summary>Required where <see cref="TakesRegularMedication"/> is true.</summary>
    public string? MedicationDetails { get; private set; }

    /// <summary>Diet, inhaler handling and anything else.</summary>
    public string? SpecialInstructions { get; private set; }

    /// <summary>Strongly prompted, never required.</summary>
    public string? PreferredHospital { get; private set; }

    /// <summary>Canonical <c>+234</c> form.</summary>
    public string? HospitalPhone { get; private set; }

    /// <summary>Optional.</summary>
    public BloodGroup? BloodGroup { get; private set; }

    /// <summary>Optional.</summary>
    public Genotype? Genotype { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>True when all three questions have an explicit answer (spec 6.5.12's blocking rule).</summary>
    public bool IsAnswered => HasAllergy is not null && HasMedicalCondition is not null && TakesRegularMedication is not null;

    /// <summary>A new, unanswered row.</summary>
    public static PupilHealth Create(Guid pupilId) => new(pupilId);

    /// <summary>
    /// Writes the whole section. A detail is required where its question is Yes, and cleared where it is No, so a stale
    /// allergy note cannot survive a changed answer.
    /// </summary>
    public Result Apply(
        bool? hasAllergy, string? allergyDetails, bool? hasMedicalCondition, string? medicalConditionDetails, bool? takesRegularMedication,
        string? medicationDetails, string? specialInstructions, string? preferredHospital, string? hospitalPhone, BloodGroup? bloodGroup, Genotype? genotype)
    {
        var allergy = Detail(hasAllergy, allergyDetails, ShortDetailsMaxLength, "allergy", "Describe the allergy.");
        if (allergy.IsFailure)
        {
            return allergy;
        }

        var condition = Detail(hasMedicalCondition, medicalConditionDetails, LongDetailsMaxLength, "medical_condition", "Describe the medical condition.");
        if (condition.IsFailure)
        {
            return condition;
        }

        var medication = Detail(takesRegularMedication, medicationDetails, ShortDetailsMaxLength, "medication", "Describe the medication and when it is taken.");
        if (medication.IsFailure)
        {
            return medication;
        }

        var instructions = PersonFields.Clean(specialInstructions);
        if (instructions is { Length: > LongDetailsMaxLength })
        {
            return Result.Failure(Error.Validation("health.special_instructions_too_long", "Special instructions must be at most 1000 characters."));
        }

        var hospital = PersonFields.Clean(preferredHospital);
        if (hospital is { Length: > HospitalMaxLength })
        {
            return Result.Failure(Error.Validation("health.hospital_too_long", "Hospital name must be at most 160 characters."));
        }

        string? canonicalHospitalPhone = null;
        if (PersonFields.Clean(hospitalPhone) is { } phone)
        {
            if (!NigerianPhoneNumber.TryNormalize(phone, out var normalised))
            {
                return Result.Failure(Error.Validation("health.hospital_phone_invalid", "Enter the hospital's number as a Nigerian phone number."));
            }

            canonicalHospitalPhone = normalised;
        }

        HasAllergy = hasAllergy;
        AllergyDetails = allergy.Value;
        HasMedicalCondition = hasMedicalCondition;
        MedicalConditionDetails = condition.Value;
        TakesRegularMedication = takesRegularMedication;
        MedicationDetails = medication.Value;
        SpecialInstructions = instructions;
        PreferredHospital = hospital;
        HospitalPhone = canonicalHospitalPhone;
        BloodGroup = bloodGroup;
        Genotype = genotype;
        return Result.Success();
    }

    private static Result<string?> Detail(bool? answer, string? details, int maxLength, string code, string requiredMessage)
    {
        var clean = answer == true ? PersonFields.Clean(details) : null;
        if (answer == true && clean is null)
        {
            return Result.Failure<string?>(Error.Validation($"health.{code}_details_required", requiredMessage));
        }

        return clean is { Length: var length } && length > maxLength
            ? Result.Failure<string?>(Error.Validation($"health.{code}_details_too_long", $"At most {maxLength} characters."))
            : Result.Success(clean);
    }
}
