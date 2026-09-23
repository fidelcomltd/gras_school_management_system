using System.Globalization;
using System.Security.Cryptography;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Pupils.Import;

/// <summary>
/// The bulk-import validation pass (spec 6.5.13), shared by validate and commit so both judge a file identically. It
/// builds every row's domain entities in memory, so a row is accepted only if the SAME factories an ordinary admission
/// uses accept it, and it writes nothing: commit stages the drafts itself.
/// </summary>
internal sealed class PupilImportProcessor(
    IPupilImportWorkbook workbook,
    IAcademicSessionRepository sessions,
    IClassLevelRepository classLevels,
    IArmRepository armRepository,
    IEnrolmentRepository enrolments,
    IPupilRepository pupils,
    TimeProvider timeProvider)
{
    /// <summary>Validates <paramref name="file"/>. A failure is a whole-file problem; row problems are in the report.</summary>
    public async Task<Result<PupilImportRun>> RunAsync(ReadOnlyMemory<byte> file, CancellationToken cancellationToken)
    {
        var read = workbook.Read(file, PupilImportLimits.MaxRows);
        if (read.IsFailure)
        {
            return Result.Failure<PupilImportRun>(read.Error);
        }

        var sheet = read.Value;
        if (sheet.DataRowCount == 0)
        {
            return Result.Failure<PupilImportRun>(Error.Validation(
                "import.file_empty", "The file has no pupils below the header row."));
        }

        if (sheet.DataRowCount > PupilImportLimits.MaxRows)
        {
            return Result.Failure<PupilImportRun>(Error.Validation(
                "import.too_many_rows",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The file has {sheet.DataRowCount:N0} pupils. The limit is {PupilImportLimits.MaxRows:N0} per import. Split the file.")));
        }

        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < sheet.Headers.Count; index++)
        {
            columns.TryAdd(PupilImportColumns.Key(sheet.Headers[index]), index);
        }

        var missing = PupilImportColumns.Required.Where(column => !columns.ContainsKey(PupilImportColumns.Key(column))).ToList();
        if (missing.Count > 0)
        {
            return Result.Failure<PupilImportRun>(Error.Validation(
                "import.missing_columns",
                $"The file has no {string.Join(", ", missing)} column. Use the headers from the template."));
        }

        var session = await sessions.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<PupilImportRun>(Error.Conflict(
                "import.no_active_session", "No session is active. Open a session and its arms before importing pupils."));
        }

        var arms = await ArmDirectory.LoadAsync(session, classLevels, armRepository, cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var drafts = sheet.Rows.Select(row => BuildRow(new RowReader(row, columns), session, arms, today)).ToList();

        FlagDuplicatesInFile(drafts);
        await AttachRegisterMatchesAsync(drafts, cancellationToken).ConfigureAwait(false);
        var capacity = await CapacityWarningsAsync(drafts.Where(draft => draft.IsAccepted), arms, cancellationToken).ConfigureAwait(false);

        var rows = drafts.ConvertAll(draft => draft.ToDto());
        var report = new PupilImportReportDto(
            Convert.ToHexStringLower(SHA256.HashData(file.Span)),
            rows.Count,
            rows.Count(row => row.Outcome == PupilImportRowOutcome.Accepted),
            rows.Count(row => row.Outcome == PupilImportRowOutcome.Rejected),
            rows.Count(row => row.Outcome == PupilImportRowOutcome.Accepted && row.RegisterMatches.Count > 0),
            rows,
            capacity);

        return Result.Success(new PupilImportRun(report, drafts, session, arms));
    }

    /// <summary>Arms that <paramref name="accepted"/> would take over capacity, given what each holds now.</summary>
    public async Task<IReadOnlyList<PupilImportCapacityWarningDto>> CapacityWarningsAsync(
        IEnumerable<PupilImportDraft> accepted, ArmDirectory directory, CancellationToken cancellationToken)
    {
        var warnings = new List<PupilImportCapacityWarningDto>();
        foreach (var group in accepted.GroupBy(draft => draft.Arm!.Id))
        {
            var arm = group.First().Arm!;
            var current = await enrolments.CountOpenExcludingPendingByArmAsync(arm.Id, cancellationToken).ConfigureAwait(false);
            var importing = group.Count();
            if (current + importing > arm.Capacity)
            {
                warnings.Add(new PupilImportCapacityWarningDto(
                    arm.Id.ToString("D", CultureInfo.InvariantCulture), directory.NameOf(arm), arm.Capacity, current, importing));
            }
        }

        return warnings;
    }

    private static PupilImportDraft BuildRow(RowReader row, AcademicSession session, ArmDirectory arms, DateOnly today)
    {
        var draft = new PupilImportDraft(row.SheetRow, row.Text(PupilImportColumns.Surname), row.Text(PupilImportColumns.FirstName));
        var pupilId = Guid.CreateVersion7();

        Require(draft, PupilImportColumns.Surname, draft.Surname, "Enter the surname.");
        Require(draft, PupilImportColumns.FirstName, draft.FirstName, "Enter the first name.");
        Require(draft, PupilImportColumns.StateOfOrigin, row.Text(PupilImportColumns.StateOfOrigin), "Enter the state of origin.");
        Require(draft, PupilImportColumns.Lga, row.Text(PupilImportColumns.Lga), "Enter the LGA.");
        Require(draft, PupilImportColumns.HomeAddress, row.Text(PupilImportColumns.HomeAddress), "Enter the home address.");

        if (!PupilImportCells.TrySex(row.Cell(PupilImportColumns.Sex), out var sex, out var sexError))
        {
            draft.Error(PupilImportColumns.Sex, sexError!);
        }
        else if (sex is null)
        {
            draft.Error(PupilImportColumns.Sex, "Enter Male or Female.");
        }

        if (!PupilImportCells.TryDate(row.Cell(PupilImportColumns.DateOfBirth), out var dateOfBirth, out var dobError))
        {
            draft.Error(PupilImportColumns.DateOfBirth, dobError!);
        }
        else if (dateOfBirth is null)
        {
            draft.Error(PupilImportColumns.DateOfBirth, "Enter the date of birth.");
        }

        draft.DateOfBirth = dateOfBirth;

        if (!PupilImportCells.TryDate(row.Cell(PupilImportColumns.AdmissionDate), out var dateAdmitted, out var admittedError))
        {
            draft.Error(PupilImportColumns.AdmissionDate, admittedError!);
        }

        if (!PupilImportCells.TryAdmissionType(row.Cell(PupilImportColumns.AdmissionType), out var admissionType, out var typeError))
        {
            draft.Error(PupilImportColumns.AdmissionType, typeError!);
        }

        draft.Arm = arms.Resolve(row.Text(PupilImportColumns.ClassLevel), row.Text(PupilImportColumns.ArmLabel), draft);

        // The entity's own rules, only once every field it needs could be read, so each message names one real problem.
        if (!draft.HasErrors && sex is { } pupilSex && dateOfBirth is { } dob)
        {
            var created = Pupil.Create(
                pupilId, draft.Surname!, draft.FirstName!, row.Text(PupilImportColumns.MiddleName), pupilSex, dob, today,
                row.Text(PupilImportColumns.Nationality), row.Text(PupilImportColumns.StateOfOrigin)!, row.Text(PupilImportColumns.Lga)!,
                row.Text(PupilImportColumns.HomeAddress)!, row.Text(PupilImportColumns.PreviousSchool), row.Text(PupilImportColumns.PreviousClass),
                otherInformation: null);
            if (created.IsFailure)
            {
                draft.Error(PupilColumnFor(created.Error.Code), created.Error.Description);
            }
            else
            {
                draft.Pupil = created.Value;
            }
        }

        if (draft.Arm is { } arm)
        {
            var record = AdmissionRecord.Create(
                Guid.CreateVersion7(), pupilId, session.Id, dateApplicationReceived: null, dateAdmitted, today, arm.ClassLevelId,
                admissionType, admissionTypeNote: null, assessmentRequired: false);
            if (record.IsFailure)
            {
                draft.Error(PupilImportColumns.AdmissionDate, record.Error.Description);
            }
            else
            {
                draft.Record = record.Value;
            }
        }

        BuildContacts(row, pupilId, draft);
        BuildHealth(row, pupilId, draft);
        return draft;
    }

    private static void BuildContacts(RowReader row, Guid pupilId, PupilImportDraft draft)
    {
        var present = PupilImportColumns.Contacts
            .Where(slot => Fields(slot.Prefix).Any(field => row.Text($"{slot.Prefix} {field}") is not null))
            .Select(slot => slot.Role)
            .ToHashSet();
        var adults = present.Where(PupilContact.IsResponsibleAdult).ToList();

        if (!PupilImportCells.TryPrimaryContact(row.Cell(PupilImportColumns.PrimaryContact), out var primary, out var primaryError))
        {
            draft.Error(PupilImportColumns.PrimaryContact, primaryError!);
        }
        else if (adults.Count == 0)
        {
            draft.Error("Father Name", "Enter at least one of the father, mother or guardian, with a phone number.");
        }
        else if (primary is null && adults.Count > 1)
        {
            draft.Error(PupilImportColumns.PrimaryContact, "Say which of Father, Mother or Guardian the school telephones first.");
        }
        else if (primary is { } chosen && !present.Contains(chosen))
        {
            draft.Error(PupilImportColumns.PrimaryContact, $"Primary Contact is {chosen}, but no {chosen.ToString().ToLowerInvariant()} is entered.");
        }

        var primaryRole = primary ?? (adults.Count == 1 ? adults[0] : (ContactRole?)null);
        foreach (var (role, prefix) in PupilImportColumns.Contacts.Where(slot => present.Contains(slot.Role)))
        {
            var contact = PupilContact.Create(Guid.CreateVersion7(), pupilId, role);
            var applied = contact.Apply(
                row.Text($"{prefix} Name") ?? string.Empty,
                row.Text($"{prefix} Relationship"),
                PupilImportCells.Phone(row.Cell($"{prefix} Phone")) ?? string.Empty,
                PupilImportCells.Phone(row.Cell($"{prefix} WhatsApp")),
                row.Text($"{prefix} Occupation"),
                email: null,
                isPrimary: role == primaryRole);
            if (applied.IsFailure)
            {
                draft.Error(ContactColumnFor(prefix, applied.Error.Code), applied.Error.Description);
            }
            else
            {
                draft.Contacts.Add(contact);
            }
        }
    }

    // Spec 6.5.13: a row with no health columns imports with the questions unanswered (no row at all), never No.
    private static void BuildHealth(RowReader row, Guid pupilId, PupilImportDraft draft)
    {
        if (PupilImportColumns.Health.All(column => row.Cell(column).IsBlank))
        {
            return;
        }

        var allergy = Question(row, draft, PupilImportColumns.HasAllergy, PupilImportColumns.AllergyDetails);
        var condition = Question(row, draft, PupilImportColumns.HasMedicalCondition, PupilImportColumns.MedicalConditionDetails);
        var medication = Question(row, draft, PupilImportColumns.TakesMedication, PupilImportColumns.MedicationDetails);

        if (!PupilImportCells.TryBloodGroup(row.Cell(PupilImportColumns.BloodGroup), out var bloodGroup, out var bloodError))
        {
            draft.Error(PupilImportColumns.BloodGroup, bloodError!);
        }

        if (!PupilImportCells.TryGenotype(row.Cell(PupilImportColumns.Genotype), out var genotype, out var genotypeError))
        {
            draft.Error(PupilImportColumns.Genotype, genotypeError!);
        }

        var health = PupilHealth.Create(pupilId);
        var applied = health.Apply(
            allergy, row.Text(PupilImportColumns.AllergyDetails), condition, row.Text(PupilImportColumns.MedicalConditionDetails),
            medication, row.Text(PupilImportColumns.MedicationDetails), specialInstructions: null,
            row.Text(PupilImportColumns.PreferredHospital), PupilImportCells.Phone(row.Cell(PupilImportColumns.HospitalPhone)), bloodGroup, genotype);
        if (applied.IsFailure)
        {
            draft.Error(HealthColumnFor(applied.Error.Code), applied.Error.Description);
        }
        else
        {
            draft.Health = health;
        }
    }

    // The entity drops details under a No or a blank answer; in a file that is a contradiction to fix, not data to lose.
    private static bool? Question(RowReader row, PupilImportDraft draft, string question, string details)
    {
        if (!PupilImportCells.TryYesNo(row.Cell(question), out var answer, out var error))
        {
            draft.Error(question, error!);
            return null;
        }

        if (answer != true && row.Text(details) is not null)
        {
            draft.Error(details, $"{details} is filled in but {question} is not Yes. Answer Yes, or clear the details.");
        }

        return answer;
    }

    // Spec 6.5.13: duplicates inside the file are rejected. The first occurrence stays; each later one names it.
    private static void FlagDuplicatesInFile(List<PupilImportDraft> drafts)
    {
        var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var draft in drafts)
        {
            if (draft.MatchKey is not { } key)
            {
                continue;
            }

            if (firstSeen.TryGetValue(key, out var first))
            {
                draft.Error(PupilImportColumns.Surname, string.Create(
                    CultureInfo.InvariantCulture,
                    $"Row {first} has the same surname, first name and date of birth. Remove one of the two rows."));
            }
            else
            {
                firstSeen.Add(key, draft.SheetRow);
            }
        }
    }

    // Spec 6.5.13: matches against the register are warnings; the administrator chooses skip or create per row.
    private async Task AttachRegisterMatchesAsync(List<PupilImportDraft> drafts, CancellationToken cancellationToken)
    {
        var candidates = drafts.Where(draft => draft.IsAccepted && draft.DateOfBirth is not null).ToList();
        var dates = candidates.Select(draft => draft.DateOfBirth!.Value).ToHashSet();
        var register = (await pupils.ListByDatesOfBirthAsync(dates, cancellationToken).ConfigureAwait(false))
            .ToLookup(entry => PupilImportDraft.KeyOf(entry.Surname, entry.FirstName, entry.DateOfBirth), StringComparer.Ordinal);

        foreach (var draft in candidates)
        {
            draft.RegisterMatches.AddRange(register[draft.MatchKey!].Select(entry => new PupilImportRegisterMatchDto(
                entry.Id.ToString("D", CultureInfo.InvariantCulture), entry.RegistrationNumber, entry.Status, entry.Surname,
                entry.FirstName, entry.DateOfBirth)));
        }
    }

    private static void Require(PupilImportDraft draft, string column, string? value, string message)
    {
        if (value is null)
        {
            draft.Error(column, message);
        }
    }

    private static IEnumerable<string> Fields(string prefix) => prefix is "Father" or "Mother"
        ? ["Name", "Phone", "WhatsApp", "Occupation"]
        : ["Name", "Relationship", "Phone"];

    private static string PupilColumnFor(string code) => code switch
    {
        _ when code.StartsWith("pupil.surname", StringComparison.Ordinal) => PupilImportColumns.Surname,
        _ when code.StartsWith("pupil.firstname", StringComparison.Ordinal) => PupilImportColumns.FirstName,
        _ when code.StartsWith("pupil.middlename", StringComparison.Ordinal) => PupilImportColumns.MiddleName,
        _ when code.StartsWith("pupil.date_of_birth", StringComparison.Ordinal) => PupilImportColumns.DateOfBirth,
        _ when code.StartsWith("pupil.nationality", StringComparison.Ordinal) => PupilImportColumns.Nationality,
        _ when code.StartsWith("pupil.state_of_origin", StringComparison.Ordinal) => PupilImportColumns.StateOfOrigin,
        _ when code.StartsWith("pupil.lga", StringComparison.Ordinal) => PupilImportColumns.Lga,
        _ when code.StartsWith("pupil.previous_school", StringComparison.Ordinal) => PupilImportColumns.PreviousSchool,
        _ when code.StartsWith("pupil.previous_class", StringComparison.Ordinal) => PupilImportColumns.PreviousClass,
        _ => PupilImportColumns.HomeAddress,
    };

    private static string ContactColumnFor(string prefix, string code) => code switch
    {
        _ when code.StartsWith("contact.relationship", StringComparison.Ordinal) => $"{prefix} Relationship",
        _ when code.StartsWith("contact.phone", StringComparison.Ordinal) => $"{prefix} Phone",
        _ when code.StartsWith("contact.whatsapp", StringComparison.Ordinal) => $"{prefix} WhatsApp",
        _ when code.StartsWith("contact.occupation", StringComparison.Ordinal) => $"{prefix} Occupation",
        _ => $"{prefix} Name",
    };

    private static string HealthColumnFor(string code) => code switch
    {
        _ when code.StartsWith("health.allergy", StringComparison.Ordinal) => PupilImportColumns.AllergyDetails,
        _ when code.StartsWith("health.medical_condition", StringComparison.Ordinal) => PupilImportColumns.MedicalConditionDetails,
        _ when code.StartsWith("health.medication", StringComparison.Ordinal) => PupilImportColumns.MedicationDetails,
        _ when code.StartsWith("health.hospital_phone", StringComparison.Ordinal) => PupilImportColumns.HospitalPhone,
        _ => PupilImportColumns.PreferredHospital,
    };

    /// <summary>One row's cells, looked up by template column name.</summary>
    private readonly struct RowReader(ImportSheetRow row, Dictionary<string, int> columns)
    {
        public int SheetRow => row.SheetRow;

        public ImportCell Cell(string column) =>
            columns.TryGetValue(PupilImportColumns.Key(column), out var index) && index < row.Cells.Count ? row.Cells[index] : ImportCell.Blank;

        /// <summary>The trimmed text, or null when blank.</summary>
        public string? Text(string column) => Cell(column).Text is { Length: > 0 } text ? text : null;
    }
}

/// <summary>A validated file: the report, plus the drafts commit stages.</summary>
/// <param name="Report">What validate returns.</param>
/// <param name="Drafts">Every row, in file order.</param>
/// <param name="Session">The active session the arms belong to.</param>
/// <param name="Arms">The arm lookup used.</param>
internal sealed record PupilImportRun(
    PupilImportReportDto Report, IReadOnlyList<PupilImportDraft> Drafts, AcademicSession Session, ArmDirectory Arms);

/// <summary>One row being validated: the entities built so far and every problem found.</summary>
internal sealed class PupilImportDraft(int sheetRow, string? surname, string? firstName)
{
    private readonly List<PupilImportIssueDto> _errors = [];

    public int SheetRow { get; } = sheetRow;

    public string? Surname { get; } = surname;

    public string? FirstName { get; } = firstName;

    public DateOnly? DateOfBirth { get; set; }

    public Arm? Arm { get; set; }

    public string? ArmName { get; set; }

    public Pupil? Pupil { get; set; }

    public AdmissionRecord? Record { get; set; }

    public List<PupilContact> Contacts { get; } = [];

    public PupilHealth? Health { get; set; }

    public List<PupilImportRegisterMatchDto> RegisterMatches { get; } = [];

    public bool HasErrors => _errors.Count > 0;

    public bool IsAccepted => !HasErrors && Pupil is not null && Record is not null && Arm is not null;

    /// <summary>The in-file and register duplicate key, when name and date of birth could be read.</summary>
    public string? MatchKey => Surname is not null && FirstName is not null && DateOfBirth is { } dob ? KeyOf(Surname, FirstName, dob) : null;

    public static string KeyOf(string surname, string firstName, DateOnly dateOfBirth) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{PupilImportColumns.Key(surname)}|{PupilImportColumns.Key(firstName)}|{dateOfBirth:yyyy-MM-dd}");

    public void Error(string column, string message) => _errors.Add(new PupilImportIssueDto(column, message));

    public PupilImportRowDto ToDto() => new(
        SheetRow, Surname, FirstName, DateOfBirth, Arm?.Id.ToString("D", CultureInfo.InvariantCulture), ArmName,
        IsAccepted ? PupilImportRowOutcome.Accepted : PupilImportRowOutcome.Rejected,
        [.. _errors], IsAccepted ? [.. RegisterMatches] : []);
}
