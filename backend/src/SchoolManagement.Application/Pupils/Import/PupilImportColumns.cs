using System.Globalization;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils.Import;

/// <summary>The import template's columns, in spec 6.5.13's order, and how a header in an uploaded file finds one.</summary>
internal static class PupilImportColumns
{
    public const string Surname = "Surname";
    public const string FirstName = "First Name";
    public const string MiddleName = "Middle Name";
    public const string Sex = "Sex";
    public const string DateOfBirth = "Date of Birth";
    public const string Nationality = "Nationality";
    public const string StateOfOrigin = "State of Origin";
    public const string Lga = "LGA";
    public const string HomeAddress = "Home Address";
    public const string PreviousSchool = "Previous School";
    public const string PreviousClass = "Previous Class";
    public const string AdmissionDate = "Admission Date";
    public const string AdmissionType = "Admission Type";
    public const string ClassLevel = "Class Level";
    public const string ArmLabel = "Arm Label";
    public const string PrimaryContact = "Primary Contact";
    public const string HasAllergy = "Has Allergy";
    public const string AllergyDetails = "Allergy Details";
    public const string HasMedicalCondition = "Has Medical Condition";
    public const string MedicalConditionDetails = "Medical Condition Details";
    public const string TakesMedication = "Takes Medication";
    public const string MedicationDetails = "Medication Details";
    public const string PreferredHospital = "Preferred Hospital";
    public const string HospitalPhone = "Hospital Phone";
    public const string BloodGroup = "Blood Group";
    public const string Genotype = "Genotype";

    /// <summary>The spec's column list, verbatim and in order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Surname, FirstName, MiddleName, Sex, DateOfBirth, Nationality, StateOfOrigin, Lga, HomeAddress, PreviousSchool,
        PreviousClass, AdmissionDate, AdmissionType, ClassLevel, ArmLabel,
        "Father Name", "Father Phone", "Father WhatsApp", "Father Occupation",
        "Mother Name", "Mother Phone", "Mother WhatsApp", "Mother Occupation",
        "Guardian Name", "Guardian Relationship", "Guardian Phone", PrimaryContact,
        "Emergency Primary Name", "Emergency Primary Relationship", "Emergency Primary Phone",
        "Emergency Alternate Name", "Emergency Alternate Relationship", "Emergency Alternate Phone",
        HasAllergy, AllergyDetails, HasMedicalCondition, MedicalConditionDetails, TakesMedication, MedicationDetails,
        PreferredHospital, HospitalPhone, BloodGroup, Genotype,
    ];

    /// <summary>A file missing any of these headers is refused whole; every other column may be absent.</summary>
    public static readonly IReadOnlyList<string> Required =
        [Surname, FirstName, Sex, DateOfBirth, StateOfOrigin, Lga, HomeAddress, ClassLevel];

    /// <summary>Spec 6.5.13: optional in the file; absent or blank stays unanswered, never No.</summary>
    public static readonly IReadOnlyList<string> Health =
    [
        HasAllergy, AllergyDetails, HasMedicalCondition, MedicalConditionDetails, TakesMedication, MedicationDetails,
        PreferredHospital, HospitalPhone, BloodGroup, Genotype,
    ];

    /// <summary>The contact slots and their column prefixes. WhatsApp and occupation exist for father and mother only.</summary>
    public static readonly IReadOnlyList<(ContactRole Role, string Prefix)> Contacts =
    [
        (ContactRole.Father, "Father"),
        (ContactRole.Mother, "Mother"),
        (ContactRole.Guardian, "Guardian"),
        (ContactRole.EmergencyPrimary, "Emergency Primary"),
        (ContactRole.EmergencyAlternate, "Emergency Alternate"),
    ];

    /// <summary>
    /// The comparison key for headers, level and arm names and duplicate detection: lower-case letters and digits only, so
    /// <c>Date of Birth</c>, <c>date_of_birth</c> and <c>DateOfBirth</c> all find the same column. Matching only; how an
    /// arm's name is DISPLAYED stays <c>ArmDisplayName.Compose</c>'s alone.
    /// </summary>
    public static string Key(string text) =>
        string.Concat(text.Where(character => char.IsLetter(character) || char.IsDigit(character))).ToLower(CultureInfo.InvariantCulture);
}
