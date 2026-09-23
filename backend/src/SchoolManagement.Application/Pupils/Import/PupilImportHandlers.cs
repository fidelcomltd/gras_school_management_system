using System.Globalization;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Import;

/// <summary>Handles <see cref="GetPupilImportTemplateQuery"/>.</summary>
internal sealed class GetPupilImportTemplateHandler(
    IPupilImportWorkbook workbook, IAcademicSessionRepository sessions, IClassLevelRepository classLevels, IArmRepository arms)
    : IRequestHandler<GetPupilImportTemplateQuery, Result<SpreadsheetFile>>
{
    /// <summary>Suggestions only: relationship is free text on the contact (spec 6.5.5).</summary>
    private static readonly IReadOnlyList<string> Relationships =
    [
        "Mother", "Father", "Aunt", "Uncle", "Grandmother", "Grandfather", "Sister", "Brother", "Cousin", "Stepmother",
        "Stepfather", "Family friend", "Neighbour", "Guardian",
    ];

    /// <inheritdoc />
    public async Task<Result<SpreadsheetFile>> HandleAsync(GetPupilImportTemplateQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // No active session still downloads a template; its arm columns are simply empty.
        var session = await sessions.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        var directory = session is null ? null : await ArmDirectory.LoadAsync(session, classLevels, arms, cancellationToken).ConfigureAwait(false);
        var armList = directory?.Arms ?? [];

        var accepted = new ImportReferenceSheet(
            "Accepted values",
            ["Sex", "State of Origin", "Relationship", "Blood Group", "Genotype", "Admission Type", "Yes or No", "Primary Contact",
                "Class Level", "Arm Label", "Arm"],
            [
                ["Male", "Female"],
                NigerianGeography.States,
                Relationships,
                PupilImportCells.BloodGroups,
                Enum.GetNames<Genotype>(),
                ["New", "Returning"],
                ["Yes", "No"],
                ["Father", "Mother", "Guardian"],
                directory?.LevelNames ?? [],
                armList.Select(arm => arm.Label).ToList(),
                armList.Select(arm => directory!.NameOf(arm)).ToList(),
            ]);

        var pairs = NigerianGeography.States.SelectMany(state => NigerianGeography.LgasOf(state).Select(lga => (State: state, Lga: lga))).ToList();
        var lgas = new ImportReferenceSheet(
            "LGAs", ["State of Origin", "LGA"], [pairs.ConvertAll(pair => pair.State), pairs.ConvertAll(pair => pair.Lga)]);

        var content = workbook.WriteTemplate(PupilImportColumns.All, [accepted, lgas]);
        return Result.Success(new SpreadsheetFile("pupil-import-template.xlsx", content));
    }
}

/// <summary>Handles <see cref="ValidatePupilImportQuery"/>.</summary>
internal sealed class ValidatePupilImportHandler(PupilImportProcessor processor)
    : IRequestHandler<ValidatePupilImportQuery, Result<PupilImportReportDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilImportReportDto>> HandleAsync(ValidatePupilImportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = await processor.RunAsync(request.File, cancellationToken).ConfigureAwait(false);
        return run.IsFailure ? Result.Failure<PupilImportReportDto>(run.Error) : Result.Success(run.Value.Report);
    }
}

/// <summary>Handles <see cref="CommitPupilImportCommand"/>.</summary>
/// <remarks>
/// <para>
/// ALL OR NOTHING (spec 6.5.13, Appendix A entry 22): the file is re-validated from scratch, and one rejected row, one
/// undecided register match or one unconfirmed capacity breach imports nothing. Every write joins the command's single
/// ambient transaction (<c>UnitOfWorkBehavior</c>), so a failure part-way rolls back every pupil already staged.
/// </para>
/// <para>
/// Imported pupils go straight to active with a number (an existing register is not a queue of applications), issued in
/// FILE ORDER through <see cref="RegistrationNumberIssuer"/>, the same counter, composition and retry loop as admission
/// approval, one increment per row. Each opens an enrolment dated as approval dates one. The admission record keeps
/// <c>declaration_signed</c> false, which is what flags it for the office.
/// </para>
/// </remarks>
internal sealed class CommitPupilImportHandler(
    PupilImportProcessor processor,
    IPupilRepository pupils,
    IAdmissionRecordRepository admissionRecords,
    IPupilRecordRepository records,
    IEnrolmentRepository enrolments,
    ISchoolProfileRepository schoolProfiles,
    RegistrationNumberIssuer issuer,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CommitPupilImportCommand, Result<PupilImportResultDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilImportResultDto>> HandleAsync(CommitPupilImportCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validated = await processor.RunAsync(request.File, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
        {
            return Result.Failure<PupilImportResultDto>(validated.Error);
        }

        var run = validated.Value;
        if (!string.Equals(run.Report.FileSha256, request.FileSha256, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<PupilImportResultDto>(Error.Conflict(
                "import.file_changed", "This is not the file that was validated. Validate it again before importing."));
        }

        if (run.Report.RejectedCount > 0)
        {
            return Result.Failure<PupilImportResultDto>(Error.Validation(
                "import.rows_rejected",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{run.Report.RejectedCount} rows are rejected, so nothing was imported. Fix them and validate again.")));
        }

        var decided = Decide(run, request);
        if (decided.IsFailure)
        {
            return Result.Failure<PupilImportResultDto>(decided.Error);
        }

        var toCreate = decided.Value;
        if (toCreate.Count == 0)
        {
            return Result.Failure<PupilImportResultDto>(Error.Validation(
                "import.nothing_to_import", "Every row is skipped, so there is nothing to import."));
        }

        var overCapacity = await processor.CapacityWarningsAsync(toCreate, run.Arms, cancellationToken).ConfigureAwait(false);
        var capacityCheck = await CheckCapacityAsync(overCapacity, request.OverrideCapacity, cancellationToken).ConfigureAwait(false);
        if (capacityCheck.IsFailure)
        {
            return Result.Failure<PupilImportResultDto>(capacityCheck.Error);
        }

        var profile = await schoolProfiles.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var actorId = currentUser.UserId is { } actorText && Guid.TryParse(actorText, out var parsed) ? parsed : (Guid?)null;
        var now = timeProvider.GetUtcNow();
        var batchId = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture);
        var batch = new List<(Pupil Pupil, int AdmissionYear)>(toCreate.Count);

        foreach (var draft in toCreate)
        {
            var pupil = draft.Pupil!;
            var record = draft.Record!;
            var approved = pupil.Approve();
            if (approved.IsFailure)
            {
                return Result.Failure<PupilImportResultDto>(approved.Error);
            }

            record.RecordApproval(actorId, now);

            // Spec 6.5.11: "the admission date or the session start date, whichever is later", as approval.
            var effectiveFrom = record.DateAdmitted > run.Session.StartDate ? record.DateAdmitted : run.Session.StartDate;
            var enrolment = Enrolment.Open(Guid.CreateVersion7(), pupil.Id, draft.Arm!.Id, effectiveFrom);
            if (enrolment.IsFailure)
            {
                return Result.Failure<PupilImportResultDto>(enrolment.Error);
            }

            await pupils.AddAsync(pupil, cancellationToken).ConfigureAwait(false);
            await admissionRecords.AddAsync(record, cancellationToken).ConfigureAwait(false);
            foreach (var contact in draft.Contacts)
            {
                await records.AddAsync(contact, cancellationToken).ConfigureAwait(false);
            }

            if (draft.Health is { } health)
            {
                await records.AddAsync(health, cancellationToken).ConfigureAwait(false);
            }

            await enrolments.AddAsync(enrolment.Value, cancellationToken).ConfigureAwait(false);
            batch.Add((pupil, record.DateAdmitted.Year));
        }

        // One counter increment per row in file order, then one save, through approval's own issuer.
        var issued = await issuer.IssueAndSaveAsync(batch, profile, cancellationToken).ConfigureAwait(false);
        if (issued.IsFailure)
        {
            return Result.Failure<PupilImportResultDto>(issued.Error);
        }

        var imported = toCreate
            .Select((draft, index) => new PupilImportedDto(
                draft.SheetRow, draft.Pupil!.Id.ToString("D", CultureInfo.InvariantCulture), issued.Value[index]))
            .ToList();

        foreach (var warning in overCapacity)
        {
            // Spec 6.4.6: the override is written to the audit log with the arm and the resulting count.
            await auditSink.RecordAsync(
                Privileges.Arm.CapacityOverride,
                "arm",
                warning.ArmId,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["batchId"] = batchId,
                    ["capacity"] = warning.Capacity,
                    ["resultingCount"] = warning.CurrentCount + warning.ImportCount,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);
        }

        // Spec 6.1.12: a bulk action is ONE event with a null entity id and a batch id in its metadata. No names and no
        // health detail: counts, the numbers issued per admission year and the file's hash only.
        var skipped = run.Report.AcceptedCount - toCreate.Count;
        await auditSink.RecordAsync(
            Privileges.Pupil.Import,
            "pupil",
            entityId: null,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["batchId"] = batchId,
                ["imported"] = imported.Count,
                ["skipped"] = skipped,
                ["registrationNumbers"] = NumberRanges(batch, imported),
                ["fileSha256"] = run.Report.FileSha256,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new PupilImportResultDto(imported.Count, skipped, imported));
    }

    // Rows from different admission years draw from different counters, so one first-to-last pair would misstate the batch.
    private static List<string> NumberRanges(List<(Pupil Pupil, int AdmissionYear)> batch, List<PupilImportedDto> imported) =>
        imported
            .Select((pupil, index) => (Year: batch[index].AdmissionYear, pupil.RegistrationNumber))
            .GroupBy(entry => entry.Year)
            .OrderBy(group => group.Key)
            .Select(group => string.Create(
                CultureInfo.InvariantCulture,
                $"{group.First().RegistrationNumber} to {group.Last().RegistrationNumber} ({group.Count()})"))
            .ToList();

    // Every register match needs exactly one decision, and a decision on any other row means the client is out of step.
    private static Result<List<PupilImportDraft>> Decide(PupilImportRun run, CommitPupilImportCommand request)
    {
        var skip = request.SkipRows.ToHashSet();
        var create = request.CreateRows.ToHashSet();
        if (skip.Overlaps(create))
        {
            return Result.Failure<List<PupilImportDraft>>(Error.Validation(
                "import.decision_conflict", $"Row {skip.Intersect(create).Min()} is marked both skip and create. Choose one."));
        }

        var matched = run.Report.Rows.Where(row => row.RegisterMatches.Count > 0).Select(row => row.SheetRow).ToHashSet();
        var undecided = matched.Where(row => !skip.Contains(row) && !create.Contains(row)).Order().ToList();
        if (undecided.Count > 0)
        {
            return Result.Failure<List<PupilImportDraft>>(Error.Validation(
                "import.decision_missing",
                $"Rows {string.Join(", ", undecided.Take(10))} match pupils already on the register. Choose skip or create for each."));
        }

        var stray = skip.Concat(create).Where(row => !matched.Contains(row)).Order().ToList();
        if (stray.Count > 0)
        {
            return Result.Failure<List<PupilImportDraft>>(Error.Validation(
                "import.decision_unexpected",
                $"Rows {string.Join(", ", stray.Take(10))} match no pupil on the register, so they take no decision. Validate the file again."));
        }

        return Result.Success(run.Drafts.Where(draft => draft.IsAccepted && !skip.Contains(draft.SheetRow)).ToList());
    }

    private async Task<Result> CheckCapacityAsync(
        IReadOnlyList<PupilImportCapacityWarningDto> overCapacity, bool confirmed, CancellationToken cancellationToken)
    {
        if (overCapacity.Count == 0)
        {
            return Result.Success();
        }

        var first = overCapacity[0];
        if (!confirmed)
        {
            return Result.Failure(Error.Conflict(
                "import.capacity_unconfirmed",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{first.ArmName} would hold {first.CurrentCount + first.ImportCount} pupils against a capacity of " +
                    $"{first.Capacity}. Confirm the capacity override to import anyway.")));
        }

        // Spec 6.4.6, as approval: school-wide, or arm-restricted to every arm going over.
        var grants = await effectivePrivilegeProvider.GetGrantsAsync(currentUser.UserId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        var scope = PupilAccessGuard.Resolve(grants, Privileges.Arm.CapacityOverride);
        var allowedArms = scope == PupilAccessScope.ArmRestricted ? PupilAccessGuard.ResolveArmIds(grants, Privileges.Arm.CapacityOverride) : null;
        var refused = overCapacity.FirstOrDefault(warning => scope switch
        {
            PupilAccessScope.SchoolWide => false,
            PupilAccessScope.ArmRestricted => !allowedArms!.Contains(Guid.Parse(warning.ArmId)),
            _ => true,
        });

        return refused is null
            ? Result.Success()
            : Result.Failure(Error.Forbidden(
                "import.capacity_override_forbidden",
                $"{refused.ArmName} is over its capacity of {refused.Capacity}, and going over needs the capacity override " +
                "privilege. Raise the capacity, or ask someone who holds it."));
    }
}
