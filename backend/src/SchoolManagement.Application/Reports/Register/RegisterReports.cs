using System.Globalization;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Application.Weekly;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Reports.Register;

/// <summary>Nominal roll filters.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="LevelId">One level.</param>
/// <param name="ArmId">One arm.</param>
/// <param name="Status">A pupil status; Active when absent.</param>
/// <param name="Sex">Male or Female.</param>
public sealed record NominalRollFilters(string? SessionId, string? LevelId, string? ArmId, string? Status, string? Sex) : IReportFilters;

/// <summary>Enrolment summary filters.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="Status">A pupil status; Active when absent.</param>
public sealed record EnrolmentSummaryFilters(string? SessionId, string? Status) : IReportFilters;

/// <summary>Guardian contact list filters.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="ArmId">The arm.</param>
public sealed record GuardianContactFilters(string? SessionId, string? ArmId) : IReportFilters;

/// <summary>Outstanding admission documents filters.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="LevelId">One level.</param>
/// <param name="ArmId">One arm.</param>
/// <param name="DocumentType">One document type (<c>BirthCertificate</c>, ...).</param>
public sealed record OutstandingDocumentsFilters(string? SessionId, string? LevelId, string? ArmId, string? DocumentType) : IReportFilters;

/// <summary>Admissions pipeline filters.</summary>
/// <param name="LevelId">One level applied for.</param>
/// <param name="MinDays">Only records at least this many days old.</param>
public sealed record AdmissionsPipelineFilters(string? LevelId, int? MinDays) : IReportFilters;

/// <summary>What the register reports share: filters, the primary guardian, age.</summary>
internal static class RegisterText
{
    private static readonly ContactRole[] ResponsibleAdults = [ContactRole.Father, ContactRole.Mother, ContactRole.Guardian];

    /// <summary>
    /// The primary guardian (the responsible adult marked primary, else father, mother, guardian in that order) and the next
    /// responsible adult, whose phone is the alternate.
    /// </summary>
    public static (PupilContact? Primary, PupilContact? Next) Guardians(IEnumerable<PupilContact> contacts)
    {
        var adults = contacts.Where(contact => ResponsibleAdults.Contains(contact.Role))
            .OrderByDescending(contact => contact.IsPrimaryContact)
            .ThenBy(contact => Array.IndexOf(ResponsibleAdults, contact.Role))
            .ToList();
        return (adults.ElementAtOrDefault(0), adults.ElementAtOrDefault(1));
    }

    /// <summary>Whole years on <paramref name="today"/>.</summary>
    public static int Age(DateOnly dateOfBirth, DateOnly today) =>
        today.Year - dateOfBirth.Year - (today < dateOfBirth.AddYears(today.Year - dateOfBirth.Year) ? 1 : 0);

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static bool TryStatus(string? value, out PupilStatus status, out Error error)
    {
        error = Error.None;
        status = PupilStatus.Active;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        var name = Enum.GetNames<PupilStatus>().FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
        if (name is null || name == nameof(PupilStatus.Pending))
        {
            error = Error.Validation("report.filter", "status must be Active, Transferred, Withdrawn or Graduated.");
            return false;
        }

        status = Enum.Parse<PupilStatus>(name);
        return true;
    }
}

/// <summary>
/// Spec 15 section 10, nominal roll: the register for an arm or a level (registration number, name, sex, date of birth, age,
/// admission date, status, primary guardian and phone), grouped by class. Arm-scoped. The register taken to any inspection.
/// </summary>
internal sealed class NominalRollReport(IReportReader reader, IPupilRecordRepository records) : ReportBuilder<NominalRollFilters>
{
    public override string Key => "nominal-roll";

    protected override async Task<Result<ReportDto>> BuildAsync(NominalRollFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.SessionId, "sessionId", out var sessionId, out var error)
            || !TryOptionalId(filters.LevelId, "levelId", out var levelId, out error)
            || !TryOptionalId(filters.ArmId, "armId", out var armId, out error)
            || !RegisterText.TryStatus(filters.Status, out var status, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        PupilSex? sex = null;
        if (!string.IsNullOrEmpty(filters.Sex))
        {
            if (!Enum.TryParse<PupilSex>(filters.Sex, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(filters.Sex, out _))
            {
                return Result.Failure<ReportDto>(Error.Validation("report.filter", "sex must be Male or Female."));
            }

            sex = parsed;
        }

        if (armId is { } wanted && !context.Scope.Allows(wanted))
        {
            return Result.Failure<ReportDto>(OutOfScope());
        }

        var session = await reader.FindSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<ReportDto>(NotFound("No session was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(sessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => (armId is null || arm.ArmId == armId) && (levelId is null || arm.LevelId == levelId) && context.Scope.Allows(arm.ArmId))
            .ToList();
        if ((armId is not null || levelId is not null) && arms.Count == 0)
        {
            return Result.Failure<ReportDto>(NotFound("No class you can see matches those filters in that session."));
        }

        var armIndex = arms.Select((arm, index) => (arm.ArmId, index)).ToDictionary(entry => entry.ArmId, entry => entry.index);
        var pupils = (await reader.ListRegisterAsync(sessionId, cancellationToken).ConfigureAwait(false))
            .Where(pupil => armIndex.ContainsKey(pupil.ArmId) && pupil.Status == status && (sex is null || pupil.Sex == sex))
            .OrderBy(pupil => armIndex[pupil.ArmId])
            .ThenBy(pupil => pupil.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var set = await records.LoadForPupilsAsync([.. pupils.Select(pupil => pupil.PupilId)], cancellationToken).ConfigureAwait(false);
        var today = WeeklyProjection.LagosToday(context.Now);

        var rows = new List<ReportRowDto>();
        foreach (var arm in arms)
        {
            var inArm = pupils.Where(pupil => pupil.ArmId == arm.ArmId).ToList();
            if (inArm.Count == 0)
            {
                continue;
            }

            rows.Add(new(ReportRowKind.Heading, [arm.Name, null, null, null, null, null, null, null, null]));
            foreach (var pupil in inArm)
            {
                var (guardian, _) = RegisterText.Guardians(set.Contacts[pupil.PupilId]);
                rows.Add(new(ReportRowKind.Data,
                [
                    pupil.RegistrationNumber,
                    pupil.DisplayName,
                    pupil.Sex.ToString(),
                    RegisterText.Date(pupil.DateOfBirth),
                    ReportText.Number(RegisterText.Age(pupil.DateOfBirth, today)),
                    set.Admissions.GetValueOrDefault(pupil.PupilId) is { } admission ? RegisterText.Date(admission.DateAdmitted) : null,
                    pupil.Status.ToString(),
                    guardian?.FullName,
                    guardian?.Phone,
                ]));
            }

            rows.Add(new(ReportRowKind.Subtotal, [$"{ReportText.Number(inArm.Count)} pupils", null, null, null, null, null, null, null, null]));
        }

        var filterLines = new List<string> { $"Session: {session.Name}", $"Status: {status}" };
        if (armId is not null)
        {
            filterLines.Add($"Class: {arms[0].Name}");
        }
        else if (levelId is not null)
        {
            filterLines.Add($"Level: {arms[0].LevelName}");
        }

        if (sex is not null)
        {
            filterLines.Add($"Sex: {sex}");
        }

        return Result.Success(Report(
            context,
            "Nominal roll",
            filterLines,
            [
                new("Reg. no.", ReportAlign.Left),
                new("Name", ReportAlign.Left),
                new("Sex", ReportAlign.Left),
                new("Date of birth", ReportAlign.Left),
                new("Age", ReportAlign.Right),
                new("Admitted", ReportAlign.Left),
                new("Status", ReportAlign.Left),
                new("Primary guardian", ReportAlign.Left),
                new("Phone", ReportAlign.Left),
            ],
            rows,
            [$"Ages are as at {RegisterText.Date(today)}."],
            ReportOrientation.Landscape));
    }
}

/// <summary>
/// Spec 15 section 10, enrolment summary: one row per arm (boys, girls, total, capacity, space left), a subtotal per level and
/// a school total. How full the school is.
/// </summary>
internal sealed class EnrolmentSummaryReport(IReportReader reader) : ReportBuilder<EnrolmentSummaryFilters>
{
    public override string Key => "enrolment-summary";

    protected override async Task<Result<ReportDto>> BuildAsync(EnrolmentSummaryFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.SessionId, "sessionId", out var sessionId, out var error) || !RegisterText.TryStatus(filters.Status, out var status, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        var session = await reader.FindSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<ReportDto>(NotFound("No session was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(sessionId, cancellationToken).ConfigureAwait(false)).Where(arm => context.Scope.Allows(arm.ArmId)).ToList();
        var pupils = (await reader.ListRegisterAsync(sessionId, cancellationToken).ConfigureAwait(false)).Where(pupil => pupil.Status == status).ToList();

        var rows = new List<ReportRowDto>();
        var total = (Boys: 0, Girls: 0, Capacity: 0);
        foreach (var level in arms.GroupBy(arm => arm.LevelId))
        {
            var sum = (Boys: 0, Girls: 0, Capacity: 0);
            foreach (var arm in level)
            {
                var inArm = pupils.Where(pupil => pupil.ArmId == arm.ArmId).ToList();
                var counts = (Boys: inArm.Count(pupil => pupil.Sex == PupilSex.Male), Girls: inArm.Count(pupil => pupil.Sex == PupilSex.Female), Capacity: arm.Capacity ?? 0);
                rows.Add(Row(ReportRowKind.Data, arm.Name, counts));
                sum = (sum.Boys + counts.Boys, sum.Girls + counts.Girls, sum.Capacity + counts.Capacity);
            }

            rows.Add(Row(ReportRowKind.Subtotal, level.First().LevelName, sum));
            total = (total.Boys + sum.Boys, total.Girls + sum.Girls, total.Capacity + sum.Capacity);
        }

        if (arms.Count > 0)
        {
            rows.Add(Row(ReportRowKind.Total, context.Scope.Arms is null ? "School" : "Your classes", total));
        }

        return Result.Success(Report(
            context,
            "Enrolment summary",
            [$"Session: {session.Name}", $"Status: {status}"],
            [
                new("Class", ReportAlign.Left),
                new("Boys", ReportAlign.Right),
                new("Girls", ReportAlign.Right),
                new("Total", ReportAlign.Right),
                new("Capacity", ReportAlign.Right),
                new("Space left", ReportAlign.Right),
            ],
            rows,
            ["Space left is capacity less the pupils counted here; negative means over capacity."]));
    }

    private static ReportRowDto Row(ReportRowKind kind, string label, (int Boys, int Girls, int Capacity) counts) => new(kind,
    [
        label,
        ReportText.Number(counts.Boys),
        ReportText.Number(counts.Girls),
        ReportText.Number(counts.Boys + counts.Girls),
        ReportText.Number(counts.Capacity),
        (counts.Capacity - counts.Boys - counts.Girls).ToString(CultureInfo.InvariantCulture),
    ]);
}

/// <summary>
/// Spec 15 section 10, guardian contact list: per arm, pupil, primary guardian, relationship, phone and alternate phone.
/// Deliberately narrow (no address, occupation or second guardian): a form teacher telephoning parents. <c>contact.view</c>
/// (the spec's <c>guardian.view</c>), arm-scoped.
/// </summary>
internal sealed class GuardianContactReport(IReportReader reader, IPupilRecordRepository records) : ReportBuilder<GuardianContactFilters>
{
    public override string Key => "guardian-contacts";

    public override string ViewPrivilege => Privileges.Contact.View;

    protected override async Task<Result<ReportDto>> BuildAsync(GuardianContactFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.SessionId, "sessionId", out var sessionId, out var error) || !TryId(filters.ArmId, "armId", out var armId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        if (!context.Scope.Allows(armId))
        {
            return Result.Failure<ReportDto>(OutOfScope());
        }

        var arm = (await reader.ListArmsAsync(sessionId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.ArmId == armId);
        if (arm is null)
        {
            return Result.Failure<ReportDto>(NotFound("No class was found for that session."));
        }

        var pupils = (await reader.ListRegisterAsync(sessionId, cancellationToken).ConfigureAwait(false))
            .Where(pupil => pupil.ArmId == armId && pupil.Status == PupilStatus.Active)
            .OrderBy(pupil => pupil.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var set = await records.LoadForPupilsAsync([.. pupils.Select(pupil => pupil.PupilId)], cancellationToken).ConfigureAwait(false);

        var rows = pupils.ConvertAll(pupil =>
        {
            var (guardian, next) = RegisterText.Guardians(set.Contacts[pupil.PupilId]);
            return new ReportRowDto(ReportRowKind.Data, [pupil.DisplayName, guardian?.FullName, guardian?.Relationship ?? guardian?.Role.ToString(), guardian?.Phone, next?.Phone]);
        });

        return Result.Success(Report(
            context,
            "Guardian contact list",
            [$"Class: {arm.Name}"],
            [
                new("Pupil", ReportAlign.Left),
                new("Guardian", ReportAlign.Left),
                new("Relationship", ReportAlign.Left),
                new("Phone", ReportAlign.Left),
                new("Alternate phone", ReportAlign.Left),
            ],
            rows,
            ["The alternate phone is the next parent or guardian on the record. No address or occupation, by design (spec 15)."]));
    }
}

/// <summary>
/// Spec 15 section 10.2, outstanding admission documents: one row per active pupil per document not yet received, from the
/// same completeness check the pupil record uses, oldest record first so the longest gaps are chased first.
/// </summary>
internal sealed class OutstandingDocumentsReport(IReportReader reader, IPupilRepository pupils, IPupilRecordRepository records)
    : ReportBuilder<OutstandingDocumentsFilters>
{
    public override string Key => "outstanding-documents";

    protected override async Task<Result<ReportDto>> BuildAsync(OutstandingDocumentsFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.SessionId, "sessionId", out var sessionId, out var error)
            || !TryOptionalId(filters.LevelId, "levelId", out var levelId, out error)
            || !TryOptionalId(filters.ArmId, "armId", out var armId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        var documentType = Enum.GetNames<PupilDocumentType>().FirstOrDefault(name => string.Equals(name, filters.DocumentType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(filters.DocumentType) && documentType is null)
        {
            return Result.Failure<ReportDto>(Error.Validation("report.filter", $"documentType must be one of {string.Join(", ", Enum.GetNames<PupilDocumentType>())}."));
        }

        if (armId is { } wanted && !context.Scope.Allows(wanted))
        {
            return Result.Failure<ReportDto>(OutOfScope());
        }

        var session = await reader.FindSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<ReportDto>(NotFound("No session was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(sessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => (armId is null || arm.ArmId == armId) && (levelId is null || arm.LevelId == levelId) && context.Scope.Allows(arm.ArmId))
            .ToDictionary(arm => arm.ArmId);
        var enrolled = (await pupils.ListActiveEnrolledInSessionAsync(sessionId, cancellationToken).ConfigureAwait(false))
            .Where(row => arms.ContainsKey(row.ArmId))
            .ToList();
        var set = await records.LoadForPupilsAsync([.. enrolled.Select(row => row.Pupil.Id)], cancellationToken).ConfigureAwait(false);

        var rows = enrolled
            .SelectMany(row =>
            {
                var completeness = AdmissionCompleteness.Evaluate(row.Pupil, set);
                return completeness.Blocking.Concat(completeness.Chased)
                    .Where(item => item.Code.StartsWith("documents.", StringComparison.Ordinal)
                        && (documentType is null || item.Code == $"documents.{documentType}"))
                    .Select(item => (row.Pupil, row.ArmId, Item: item));
            })
            .OrderBy(entry => entry.Pupil.CreatedAtUtc)
            .ThenBy(entry => entry.Pupil.Surname, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new ReportRowDto(ReportRowKind.Data,
            [
                RegisterText.Date(DateOnly.FromDateTime(entry.Pupil.CreatedAtUtc.ToOffset(WeeklyProjection.LagosOffset).DateTime)),
                $"{entry.Pupil.Surname.ToUpperInvariant()} {entry.Pupil.FirstName}",
                entry.Pupil.RegistrationNumber,
                arms[entry.ArmId].Name,
                entry.Item.Message.TrimEnd('.'),
            ]))
            .ToList();

        var filterLines = new List<string> { $"Session: {session.Name}" };
        if (documentType is not null)
        {
            filterLines.Add($"Document: {documentType}");
        }

        return Result.Success(Report(
            context,
            "Outstanding admission documents",
            filterLines,
            [
                new("Record created", ReportAlign.Left),
                new("Pupil", ReportAlign.Left),
                new("Reg. no.", ReportAlign.Left),
                new("Class", ReportAlign.Left),
                new("Document", ReportAlign.Left),
            ],
            rows,
            ["Active pupils only; a pending admission's documents are on the admissions pipeline."]));
    }
}

/// <summary>
/// Spec 15 section 10.2, admissions pipeline: every pending admission by the level applied for, the step it is held at (the
/// first step with something blocking approval), days since the record was created, and what blocks approval. A pending
/// record has no class yet, so an arm-restricted holder sees none of them.
/// </summary>
internal sealed class AdmissionsPipelineReport(IReportReader reader, IPupilRepository pupils, IPupilRecordRepository records)
    : ReportBuilder<AdmissionsPipelineFilters>
{
    public override string Key => "admissions-pipeline";

    protected override async Task<Result<ReportDto>> BuildAsync(AdmissionsPipelineFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryOptionalId(filters.LevelId, "levelId", out var levelId, out var error))
        {
            return Result.Failure<ReportDto>(error);
        }

        if (filters.MinDays is < 0 or > 3650)
        {
            return Result.Failure<ReportDto>(Error.Validation("report.filter", "minDays must be between 0 and 3650."));
        }

        var notes = new List<string>();
        var pending = new List<Pupil>();
        if (context.Scope.Arms is null)
        {
            foreach (var id in await reader.ListPendingPupilIdsAsync(cancellationToken).ConfigureAwait(false))
            {
                if (await pupils.FindReadOnlyByIdAsync(id, cancellationToken).ConfigureAwait(false) is { } pupil)
                {
                    pending.Add(pupil);
                }
            }
        }
        else
        {
            notes.Add("Pending admissions have no class yet, so they show only to a school-wide holder of report.view.");
        }

        var set = await records.LoadForPupilsAsync([.. pending.Select(pupil => pupil.Id)], cancellationToken).ConfigureAwait(false);
        var levelNames = await reader.FindLevelNamesAsync([.. set.Admissions.Values.Select(admission => admission.ClassAdmittedInto).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        var today = WeeklyProjection.LagosToday(context.Now);

        var rows = pending
            .Select(pupil =>
            {
                var admission = set.Admissions.GetValueOrDefault(pupil.Id);
                var created = DateOnly.FromDateTime(pupil.CreatedAtUtc.ToOffset(WeeklyProjection.LagosOffset).DateTime);
                var completeness = AdmissionCompleteness.Evaluate(pupil, set);
                return (Pupil: pupil, LevelId: admission?.ClassAdmittedInto, Created: created, Days: today.DayNumber - created.DayNumber, completeness.Blocking);
            })
            .Where(entry => (levelId is null || entry.LevelId == levelId) && (filters.MinDays is null || entry.Days >= filters.MinDays))
            .OrderBy(entry => entry.LevelId is { } id ? levelNames.GetValueOrDefault(id) : null, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(entry => entry.Days)
            .Select(entry => new ReportRowDto(ReportRowKind.Data,
            [
                entry.LevelId is { } id ? levelNames.GetValueOrDefault(id, "Unknown level") : "Not chosen yet",
                $"{entry.Pupil.Surname.ToUpperInvariant()} {entry.Pupil.FirstName}",
                RegisterText.Date(entry.Created),
                ReportText.Number(entry.Days),
                entry.Blocking.Count == 0 ? "Ready to approve" : $"Step {entry.Blocking.Min(item => item.Step).ToString(CultureInfo.InvariantCulture)}",
                entry.Blocking.Count == 0 ? null : string.Join("; ", entry.Blocking.Select(item => item.Message.TrimEnd('.'))),
            ]))
            .ToList();

        var filterLines = new List<string>();
        if (levelId is { } chosen)
        {
            filterLines.Add($"Level: {levelNames.GetValueOrDefault(chosen, "that level")}");
        }

        if (filters.MinDays is { } days)
        {
            filterLines.Add($"At least {days.ToString(CultureInfo.InvariantCulture)} days old");
        }

        return Result.Success(Report(
            context,
            "Admissions pipeline",
            filterLines,
            [
                new("Level", ReportAlign.Left),
                new("Pupil", ReportAlign.Left),
                new("Created", ReportAlign.Left),
                new("Days", ReportAlign.Right),
                new("Held at", ReportAlign.Left),
                new("Blocking approval", ReportAlign.Left),
            ],
            rows,
            notes,
            ReportOrientation.Landscape));
    }
}
