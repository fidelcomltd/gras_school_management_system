using System.Text.RegularExpressions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Pupils;

/// <summary>
/// The pupil register's core record (spec 6.5.4). TASK-0050 builds this entity and its read
/// surface only — <c>registration_number</c> stays <see langword="null"/> forever within this
/// card (issued at admission approval, TASK-0051), <see cref="Status"/> stays
/// <see cref="PupilStatus.Pending"/> forever within this card (no status-change endpoint exists
/// yet), and no arm/enrolment reference exists on this type at all: spec 6.5.4's "class as the
/// composed arm display name" comes from the pupil's OPEN ENROLMENT, an entity this card
/// deliberately does not build (see the card's own "Do not model an enrolment").
/// </summary>
/// <remarks>
/// <c>blood_group</c>, <c>genotype</c> and <c>medical_note</c> are NOT on this entity — spec 6.5.4:
/// they moved to <c>pupil_health</c> (the next card). <c>photograph</c> is also absent: upload is
/// explicitly out of this card's scope and a column nobody can ever populate is a stub, which the
/// card's own guidance says to leave absent rather than ship.
/// </remarks>
public sealed partial class Pupil : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.5.4: <c>surname</c>/<c>first_name</c>/<c>middle_name</c> are <c>String 60</c>.</summary>
    public const int NameMaxLength = 60;

    /// <summary>Spec 6.5.4: <c>registration_number</c> is <c>String 24</c>.</summary>
    public const int RegistrationNumberMaxLength = 24;

    /// <summary>Spec 6.5.4: <c>nationality</c>/<c>state_of_origin</c> are <c>String 60</c>.</summary>
    public const int NationalityMaxLength = 60;

    /// <summary>Spec 6.5.4: <c>lga</c> is <c>String 80</c>.</summary>
    public const int LgaMaxLength = 80;

    /// <summary>Spec 6.5.4: <c>home_address</c> is <c>String 300</c>.</summary>
    public const int HomeAddressMaxLength = 300;

    /// <summary>Spec 6.5.4: <c>previous_school</c> is <c>String 160</c>.</summary>
    public const int PreviousSchoolMaxLength = 160;

    /// <summary>Spec 6.5.4: <c>previous_class</c> is <c>String 60</c>.</summary>
    public const int PreviousClassMaxLength = 60;

    /// <summary>Spec 6.5.4: <c>other_information</c> is <c>Text 1000</c>.</summary>
    public const int OtherInformationMaxLength = 1000;

    /// <summary>Spec 6.5.4: "Must give an age between 2 and 20." Lower bound, inclusive.</summary>
    public const int MinAgeYears = 2;

    /// <summary>Spec 6.5.4: "Must give an age between 2 and 20." Upper bound, inclusive.</summary>
    public const int MaxAgeYears = 20;

    /// <summary>Nigerian nationality's spelling, used whenever the caller omits one (spec 6.5.4: "Defaults Nigerian").</summary>
    public const string DefaultNationality = "Nigerian";

    private Pupil(
        Guid id,
        string surname,
        string firstName,
        string? middleName,
        PupilSex sex,
        DateOnly dateOfBirth,
        string nationality,
        string stateOfOrigin,
        string lga,
        string homeAddress,
        string? previousSchool,
        string? previousClass,
        string? otherInformation)
        : base(id)
    {
        Surname = surname;
        FirstName = firstName;
        MiddleName = middleName;
        Sex = sex;
        DateOfBirth = dateOfBirth;
        Nationality = nationality;
        StateOfOrigin = stateOfOrigin;
        Lga = lga;
        HomeAddress = homeAddress;
        PreviousSchool = previousSchool;
        PreviousClass = previousClass;
        OtherInformation = otherInformation;
        Status = PupilStatus.Pending;
        RegistrationNumber = null;
    }

    // EF Core materialisation constructor.
    private Pupil()
        : base()
    {
        Surname = null!;
        FirstName = null!;
        Nationality = null!;
        StateOfOrigin = null!;
        Lga = null!;
        HomeAddress = null!;
    }

    /// <summary>
    /// Null while pending. Issued once at admission approval (spec 6.5.10, TASK-0051) and immutable
    /// from that point on — this entity exposes no method that ever changes it once set.
    /// </summary>
    public string? RegistrationNumber { get; private set; }

    /// <summary>Letters, spaces, hyphens, apostrophes. Trimmed. Stored as typed (spec 6.5.4).</summary>
    public string Surname { get; private set; }

    /// <summary>Same character rule as <see cref="Surname"/>.</summary>
    public string FirstName { get; private set; }

    /// <summary>Optional. Same character rule as <see cref="Surname"/>.</summary>
    public string? MiddleName { get; private set; }

    /// <summary>Male or female (spec 6.5.4).</summary>
    public PupilSex Sex { get; private set; }

    /// <summary>In the past; must give an age between <see cref="MinAgeYears"/> and <see cref="MaxAgeYears"/>.</summary>
    public DateOnly DateOfBirth { get; private set; }

    /// <summary>Free text with an autocomplete on the client; defaults <see cref="DefaultNationality"/>.</summary>
    public string Nationality { get; private set; }

    /// <summary>One of <see cref="NigerianGeography.States"/> — never free text (spec 6.5.4).</summary>
    public string StateOfOrigin { get; private set; }

    /// <summary>One of <see cref="StateOfOrigin"/>'s LGAs per <see cref="NigerianGeography"/> — never free text.</summary>
    public string Lga { get; private set; }

    /// <summary>The child's own home address — distinct from any contact's address (spec 6.5.4).</summary>
    public string HomeAddress { get; private set; }

    /// <summary>Optional. Required (not enforced by this card — see the entity's remarks) where returning or admitted above entry level.</summary>
    public string? PreviousSchool { get; private set; }

    /// <summary>Optional. Same conditional rule as <see cref="PreviousSchool"/>.</summary>
    public string? PreviousClass { get; private set; }

    /// <summary>Defaults <see cref="PupilStatus.Pending"/> and stays there for every record this card creates.</summary>
    public PupilStatus Status { get; private set; }

    /// <summary>Section G free text. Optional.</summary>
    public string? OtherInformation { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new, pending pupil record. The caller (the command handler) has already resolved
    /// <paramref name="stateOfOrigin"/>/<paramref name="lga"/> to canonical spellings via
    /// <see cref="NigerianGeography"/> — this method re-validates them anyway, because a domain
    /// invariant that only the caller enforces is not really an invariant.
    /// </summary>
    /// <param name="id">A fresh <see cref="Guid.CreateVersion7()"/> value.</param>
    /// <param name="surname">1..60 characters, letters/spaces/hyphens/apostrophes.</param>
    /// <param name="firstName">Same rule as <paramref name="surname"/>.</param>
    /// <param name="middleName">Optional. Same rule as <paramref name="surname"/> when supplied.</param>
    /// <param name="sex">Male or female.</param>
    /// <param name="dateOfBirth">In the past; must give an age between <see cref="MinAgeYears"/> and <see cref="MaxAgeYears"/> as of <paramref name="asOfDate"/>.</param>
    /// <param name="asOfDate">"Now", for the age-range check — never computed internally (spec: inject <c>TimeProvider</c>, never call it here).</param>
    /// <param name="nationality"><see langword="null"/> or blank defaults to <see cref="DefaultNationality"/>.</param>
    /// <param name="stateOfOrigin">Must resolve to one of <see cref="NigerianGeography.States"/>.</param>
    /// <param name="lga">Must resolve to one of <paramref name="stateOfOrigin"/>'s LGAs.</param>
    /// <param name="homeAddress">The child's own address. Multi-line permitted.</param>
    /// <param name="previousSchool">Optional.</param>
    /// <param name="previousClass">Optional.</param>
    /// <param name="otherInformation">Optional, section G free text.</param>
    public static Result<Pupil> Create(
        Guid id,
        string surname,
        string firstName,
        string? middleName,
        PupilSex sex,
        DateOnly dateOfBirth,
        DateOnly asOfDate,
        string? nationality,
        string stateOfOrigin,
        string lga,
        string homeAddress,
        string? previousSchool,
        string? previousClass,
        string? otherInformation)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Pupil>(Error.Validation("pupil.id_required", "Id must not be empty."));
        }

        if (!TryNormalizeName(surname, "Surname", out var normalizedSurname, out var surnameError))
        {
            return Result.Failure<Pupil>(surnameError);
        }

        if (!TryNormalizeName(firstName, "FirstName", out var normalizedFirstName, out var firstNameError))
        {
            return Result.Failure<Pupil>(firstNameError);
        }

        string? normalizedMiddleName = null;

        if (!string.IsNullOrWhiteSpace(middleName) &&
            !TryNormalizeName(middleName, "MiddleName", out normalizedMiddleName, out var middleNameError))
        {
            return Result.Failure<Pupil>(middleNameError);
        }

        if (!TryValidateAge(dateOfBirth, asOfDate, out var ageError))
        {
            return Result.Failure<Pupil>(ageError);
        }

        var resolvedNationality = string.IsNullOrWhiteSpace(nationality) ? DefaultNationality : nationality.Trim();

        if (resolvedNationality.Length > NationalityMaxLength)
        {
            return Result.Failure<Pupil>(Error.Validation(
                "pupil.nationality_too_long", $"Nationality must be at most {NationalityMaxLength} characters."));
        }

        if (!NigerianGeography.TryNormalizeState(stateOfOrigin, out var canonicalState))
        {
            return Result.Failure<Pupil>(Error.Validation(
                "pupil.state_of_origin_invalid",
                "State of origin must be one of the 36 states or the Federal Capital Territory."));
        }

        if (!NigerianGeography.TryNormalizeLga(canonicalState, lga, out var canonicalLga))
        {
            return Result.Failure<Pupil>(Error.Validation(
                "pupil.lga_invalid", $"{lga} is not a Local Government Area of {canonicalState}."));
        }

        if (string.IsNullOrWhiteSpace(homeAddress))
        {
            return Result.Failure<Pupil>(Error.Validation("pupil.home_address_required", "Home address is required."));
        }

        var trimmedAddress = homeAddress.Trim();

        if (trimmedAddress.Length > HomeAddressMaxLength)
        {
            return Result.Failure<Pupil>(Error.Validation(
                "pupil.home_address_too_long", $"Home address must be at most {HomeAddressMaxLength} characters."));
        }

        var trimmedPreviousSchool = string.IsNullOrWhiteSpace(previousSchool) ? null : previousSchool.Trim();
        var trimmedPreviousClass = string.IsNullOrWhiteSpace(previousClass) ? null : previousClass.Trim();
        var trimmedOtherInformation = string.IsNullOrWhiteSpace(otherInformation) ? null : otherInformation.Trim();

        return Result.Success(new Pupil(
            id,
            normalizedSurname,
            normalizedFirstName,
            normalizedMiddleName,
            sex,
            dateOfBirth,
            resolvedNationality,
            canonicalState,
            canonicalLga,
            trimmedAddress,
            trimmedPreviousSchool,
            trimmedPreviousClass,
            trimmedOtherInformation));
    }

    /// <summary>
    /// Edits biographical fields only (spec 6.5.10: "no ordinary edit path exists" for
    /// <see cref="RegistrationNumber"/> — the caller never offers it here). Every parameter is
    /// independently optional; <see langword="null"/> leaves the field unchanged, matching
    /// <c>UpdateArmCommand</c>'s convention.
    /// </summary>
    public Result UpdateBiographical(
        string? surname,
        string? firstName,
        string? middleName,
        PupilSex? sex,
        DateOnly? dateOfBirth,
        DateOnly asOfDate,
        string? nationality,
        string? stateOfOrigin,
        string? lga,
        string? homeAddress,
        string? previousSchool,
        string? previousClass,
        string? otherInformation)
    {
        var nextSurname = Surname;
        var nextFirstName = FirstName;
        var nextMiddleName = MiddleName;
        var nextSex = Sex;
        var nextDateOfBirth = DateOfBirth;
        var nextNationality = Nationality;
        var nextState = StateOfOrigin;
        var nextLga = Lga;

        if (surname is not null)
        {
            if (!TryNormalizeName(surname, "Surname", out nextSurname, out var error))
            {
                return Result.Failure(error);
            }
        }

        if (firstName is not null)
        {
            if (!TryNormalizeName(firstName, "FirstName", out nextFirstName, out var error))
            {
                return Result.Failure(error);
            }
        }

        if (middleName is not null)
        {
            if (middleName.Length == 0)
            {
                nextMiddleName = null;
            }
            else if (!TryNormalizeName(middleName, "MiddleName", out nextMiddleName, out var error))
            {
                return Result.Failure(error);
            }
        }

        if (sex is { } requestedSex)
        {
            nextSex = requestedSex;
        }

        if (dateOfBirth is { } requestedDateOfBirth)
        {
            if (!TryValidateAge(requestedDateOfBirth, asOfDate, out var error))
            {
                return Result.Failure(error);
            }

            nextDateOfBirth = requestedDateOfBirth;
        }

        if (nationality is not null)
        {
            var resolved = string.IsNullOrWhiteSpace(nationality) ? DefaultNationality : nationality.Trim();

            if (resolved.Length > NationalityMaxLength)
            {
                return Result.Failure(Error.Validation(
                    "pupil.nationality_too_long", $"Nationality must be at most {NationalityMaxLength} characters."));
            }

            nextNationality = resolved;
        }

        if (stateOfOrigin is not null)
        {
            if (!NigerianGeography.TryNormalizeState(stateOfOrigin, out nextState))
            {
                return Result.Failure(Error.Validation(
                    "pupil.state_of_origin_invalid",
                    "State of origin must be one of the 36 states or the Federal Capital Territory."));
            }
        }

        // A new lga is validated against the FINAL state (either the one just supplied above, or the
        // one already on the record) — never the pre-update state, so "change both in one request"
        // works and "change lga only" is checked against the record's existing state.
        if (lga is not null)
        {
            if (!NigerianGeography.TryNormalizeLga(nextState, lga, out nextLga))
            {
                return Result.Failure(Error.Validation(
                    "pupil.lga_invalid", $"{lga} is not a Local Government Area of {nextState}."));
            }
        }
        else if (stateOfOrigin is not null && !string.Equals(nextState, StateOfOrigin, StringComparison.Ordinal))
        {
            // The state changed but lga did not — the OLD lga is not guaranteed to belong to the NEW
            // state, so re-validate it rather than silently carrying over a now-mismatched value.
            if (!NigerianGeography.TryNormalizeLga(nextState, Lga, out nextLga))
            {
                return Result.Failure(Error.Validation(
                    "pupil.lga_invalid_after_state_change",
                    $"{Lga} is not a Local Government Area of {nextState}. Supply a new lga in the same request."));
            }
        }

        string? nextHomeAddress = HomeAddress;

        if (homeAddress is not null)
        {
            if (string.IsNullOrWhiteSpace(homeAddress))
            {
                return Result.Failure(Error.Validation("pupil.home_address_required", "Home address is required."));
            }

            var trimmed = homeAddress.Trim();

            if (trimmed.Length > HomeAddressMaxLength)
            {
                return Result.Failure(Error.Validation(
                    "pupil.home_address_too_long", $"Home address must be at most {HomeAddressMaxLength} characters."));
            }

            nextHomeAddress = trimmed;
        }

        Surname = nextSurname;
        FirstName = nextFirstName;
        MiddleName = nextMiddleName;
        Sex = nextSex;
        DateOfBirth = nextDateOfBirth;
        Nationality = nextNationality;
        StateOfOrigin = nextState;
        Lga = nextLga;
        HomeAddress = nextHomeAddress!;

        if (previousSchool is not null)
        {
            PreviousSchool = previousSchool.Length == 0 ? null : previousSchool.Trim();
        }

        if (previousClass is not null)
        {
            PreviousClass = previousClass.Length == 0 ? null : previousClass.Trim();
        }

        if (otherInformation is not null)
        {
            OtherInformation = otherInformation.Length == 0 ? null : otherInformation.Trim();
        }

        return Result.Success();
    }

    /// <summary>
    /// Transitions <see cref="PupilStatus.Pending"/> to <see cref="PupilStatus.Active"/> (spec 6.5.14:
    /// "the only route into active for a new record"). Guarded: fails if the pupil is not currently
    /// pending — TASK-0051's admission approval checks this itself before calling, so this is defence
    /// in depth, not the primary check. Deliberately does NOT set <see cref="RegistrationNumber"/> —
    /// see <see cref="IssueRegistrationNumber"/>, called separately so the counter's retry-on-conflict
    /// loop (spec 6.5.10 rule 4) can re-attempt just the number, against the SAME already-approved
    /// entity, without re-running this guard a second time.
    /// </summary>
    public Result Approve()
    {
        if (Status != PupilStatus.Pending)
        {
            return Result.Failure(Error.Conflict(
                "pupil.already_approved", "This pupil is not pending — it has already been approved, declined, or otherwise resolved."));
        }

        Status = PupilStatus.Active;
        return Result.Success();
    }

    /// <summary>
    /// Writes (or, on a retry, OVERWRITES) the registration number issued by admission approval (spec
    /// 6.5.10). Deliberately UNGUARDED and callable more than once: the counter's retry-on-conflict
    /// loop (rule 4) needs to try a fresh serial against this SAME tracked entity without re-running
    /// <see cref="Approve"/>'s pending check, which would incorrectly fail once <see cref="Approve"/>
    /// has already flipped <see cref="Status"/>. Nothing outside admission approval ever calls this —
    /// that absence of a second caller, not a check here, is what keeps "immutable once issued" true
    /// once the enclosing transaction actually commits.
    /// </summary>
    public void IssueRegistrationNumber(string registrationNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationNumber);
        RegistrationNumber = registrationNumber;
    }

    /// <summary>
    /// Overwrites an already-issued number (spec 6.5.10, "Immutability and correction") — TASK-0063,
    /// the ONE other caller of a setter on <see cref="RegistrationNumber"/> besides <see
    /// cref="IssueRegistrationNumber"/>. Guarded, unlike that method: a pending pupil has never had a
    /// number issued, so there is nothing here for a correction to replace. The caller
    /// (<c>CorrectRegistrationNumberHandler</c>) reads <see cref="RegistrationNumber"/> BEFORE calling
    /// this, to capture the old value for the permanent history row — this method does not return it,
    /// since a <see cref="Result"/> carries only a domain entity's own outcome, never a DTO-shaped
    /// payload (see this type's own remarks on <see cref="Result{TValue}"/>'s intended use elsewhere).
    /// </summary>
    public Result CorrectRegistrationNumber(string newRegistrationNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newRegistrationNumber);

        if (RegistrationNumber is null)
        {
            return Result.Failure(Error.Conflict(
                "pupil.registration_number_not_issued",
                "This pupil has no registration number to correct yet — approve the admission first."));
        }

        RegistrationNumber = newRegistrationNumber;
        return Result.Success();
    }

    /// <summary>
    /// Declines a pending admission (spec 6.5.14: pending -> withdrawn). Guarded: fails if the pupil
    /// is not currently pending. Issues no registration number and leaves the counter untouched —
    /// this method never touches <see cref="RegistrationNumber"/> at all.
    /// </summary>
    public Result DeclineAdmission()
    {
        if (Status != PupilStatus.Pending)
        {
            return Result.Failure(Error.Conflict(
                "pupil.not_pending", "Only a pending admission can be declined."));
        }

        Status = PupilStatus.Withdrawn;
        return Result.Success();
    }

    /// <summary>
    /// Whole years between <paramref name="dateOfBirth"/> and <paramref name="asOfDate"/> — the same
    /// computation <see cref="Create"/>/<see cref="UpdateBiographical"/> use to enforce the age range,
    /// exposed so the read side (<c>PupilMapper</c>) never re-derives it a second way.
    /// </summary>
    public static int CalculateAgeYears(DateOnly dateOfBirth, DateOnly asOfDate)
    {
        var age = asOfDate.Year - dateOfBirth.Year;

        if (dateOfBirth > asOfDate.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static bool TryValidateAge(DateOnly dateOfBirth, DateOnly asOfDate, out Error error)
    {
        var age = CalculateAgeYears(dateOfBirth, asOfDate);

        if (age is >= MinAgeYears and <= MaxAgeYears)
        {
            error = Error.None;
            return true;
        }

        error = Error.Validation(
            "pupil.date_of_birth_out_of_range",
            $"A date of birth of {dateOfBirth:dd/MM/yyyy} makes this pupil {age} years old. Check the date.");
        return false;
    }

    private static bool TryNormalizeName(string value, string fieldName, out string normalized, out Error error)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();

        if (trimmed.Length is 0 or > NameMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation(
                $"pupil.{fieldName.ToLowerInvariant()}_invalid_length",
                $"{fieldName} must be 1 to {NameMaxLength} characters.");
            return false;
        }

        if (!NamePattern().IsMatch(trimmed))
        {
            normalized = string.Empty;
            error = Error.Validation(
                $"pupil.{fieldName.ToLowerInvariant()}_invalid_characters",
                $"{fieldName} may contain only letters, spaces, hyphens and apostrophes.");
            return false;
        }

        normalized = trimmed;
        error = Error.None;
        return true;
    }

    // Letters (any script), spaces, hyphens and apostrophes only (spec 6.5.4).
    [GeneratedRegex(@"^[\p{L} '-]+$")]
    private static partial Regex NamePattern();
}
