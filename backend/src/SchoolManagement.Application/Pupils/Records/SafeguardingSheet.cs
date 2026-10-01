using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Records;

/// <summary>
/// <c>GET /api/v1/reports/safeguarding?armId=</c> (spec 15 section 10.2): the class safeguarding sheet, on screen. A command,
/// not a query, because every generation writes an audit event, and only commands commit (as <c>ReadPupilHealthCommand</c>).
/// </summary>
/// <param name="ArmId">The class, from the query string; validated here so a bad value is a 422, not a binding 400.</param>
public sealed record GenerateSafeguardingSheetCommand(string? ArmId) : ICommand<Result<SafeguardingSheetDto>>;

/// <summary><c>GET /api/v1/reports/safeguarding/pdf?armId=</c>: the same sheet as a printable PDF, for the gate or an excursion.</summary>
/// <param name="ArmId">The class.</param>
public sealed record GenerateSafeguardingSheetPdfCommand(string? ArmId) : ICommand<Result<SchoolImageContent>>;

/// <summary>The arm is required and must be a GUID.</summary>
internal sealed class GenerateSafeguardingSheetCommandValidator : AbstractValidator<GenerateSafeguardingSheetCommand>
{
    public GenerateSafeguardingSheetCommandValidator() =>
        RuleFor(command => command.ArmId).Must(SafeguardingSheetBuilder.IsArmId).WithMessage("armId must name a class (a GUID).");
}

/// <summary>The arm is required and must be a GUID.</summary>
internal sealed class GenerateSafeguardingSheetPdfCommandValidator : AbstractValidator<GenerateSafeguardingSheetPdfCommand>
{
    public GenerateSafeguardingSheetPdfCommandValidator() =>
        RuleFor(command => command.ArmId).Must(SafeguardingSheetBuilder.IsArmId).WithMessage("armId must name a class (a GUID).");
}

/// <summary>The class safeguarding sheet: every active pupil in one arm with their health and collection data.</summary>
/// <param name="ArmId">The class.</param>
/// <param name="ArmName">E.g. "Primary 2 Gold".</param>
/// <param name="SessionName">The session the arm belongs to.</param>
/// <param name="GeneratedAtUtc">When this copy was generated; each generation is audited.</param>
/// <param name="Pupils">By surname, then first name.</param>
public sealed record SafeguardingSheetDto(
    string ArmId, string ArmName, string SessionName, DateTimeOffset GeneratedAtUtc, IReadOnlyList<SafeguardingSheetRowDto> Pupils);

/// <summary>One pupil's line. Health answers read "None" or "Not asked" when there is no detail, never blank.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="RegistrationNumber">The pupil's number.</param>
/// <param name="Name">Surname first.</param>
/// <param name="Thumbnail">
/// The 96 pixel photograph as a <c>data:image/jpeg</c> URL, or null (none, or it could not be fetched). Carried in the sheet,
/// so the photograph is governed by the sheet's own privilege in both formats (spec 10.2 lists it as a column).
/// </param>
/// <param name="Allergies">"None", "Not asked", or the detail.</param>
/// <param name="MedicalConditions">"None", "Not asked", or the detail.</param>
/// <param name="Medication">"None", "Not asked", or the detail.</param>
/// <param name="SpecialInstructions">Free text, or empty.</param>
/// <param name="Hospital">Preferred hospital and its phone, or empty.</param>
/// <param name="PickupPersons">"Name (relationship) phone", in the parent's order.</param>
/// <param name="BarredMarker">A marker only, never names: "Yes: see office", "No" or "Not asked".</param>
public sealed record SafeguardingSheetRowDto(
    string PupilId,
    string? RegistrationNumber,
    string Name,
    string? Thumbnail,
    string Allergies,
    string MedicalConditions,
    string Medication,
    string SpecialInstructions,
    string Hospital,
    IReadOnlyList<string> PickupPersons,
    string BarredMarker);

/// <summary>Handles <see cref="GenerateSafeguardingSheetCommand"/>.</summary>
internal sealed class GenerateSafeguardingSheetHandler(SafeguardingSheetBuilder builder)
    : IRequestHandler<GenerateSafeguardingSheetCommand, Result<SafeguardingSheetDto>>
{
    /// <inheritdoc />
    public async Task<Result<SafeguardingSheetDto>> HandleAsync(GenerateSafeguardingSheetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var built = await builder.BuildAsync(Guid.Parse(request.ArmId!), "screen", cancellationToken).ConfigureAwait(false);
        return built.IsFailure ? Result.Failure<SafeguardingSheetDto>(built.Error) : Result.Success(built.Value.Sheet);
    }
}

/// <summary>Handles <see cref="GenerateSafeguardingSheetPdfCommand"/>.</summary>
internal sealed class GenerateSafeguardingSheetPdfHandler(
    SafeguardingSheetBuilder builder, ISchoolProfileRepository schoolProfiles, ISafeguardingSheetRenderer renderer)
    : IRequestHandler<GenerateSafeguardingSheetPdfCommand, Result<SchoolImageContent>>
{
    /// <inheritdoc />
    public async Task<Result<SchoolImageContent>> HandleAsync(GenerateSafeguardingSheetPdfCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var built = await builder.BuildAsync(Guid.Parse(request.ArmId!), "pdf", cancellationToken).ConfigureAwait(false);
        if (built.IsFailure)
        {
            return Result.Failure<SchoolImageContent>(built.Error);
        }

        var (sheet, photos) = built.Value;
        var profile = await schoolProfiles.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var rows = sheet.Pupils.Select(pupil => new SafeguardingSheetRow(
                pupil.Name, photos.TryGetValue(pupil.PupilId, out var photo) ? photo : (ReadOnlyMemory<byte>?)null, pupil.Allergies, pupil.MedicalConditions,
                pupil.Medication, pupil.SpecialInstructions, pupil.Hospital, pupil.PickupPersons, pupil.BarredMarker))
            .ToList();

        var document = new SafeguardingSheetDocument(
            profile.SchoolName, sheet.ArmName, sheet.SessionName, sheet.GeneratedAtUtc, rows);
        var bytes = renderer.Render(document);
        return Result.Success(new SchoolImageContent(new MemoryStream(bytes, writable: false), "application/pdf", "safeguarding-sheet.pdf"));
    }
}

/// <summary>
/// Assembles the sheet once for both formats: the arm's active pupils, their health, pickup and barred answers, formatted as
/// the sheet prints them, and their thumbnails. Checks <c>pupil.safeguarding.view</c> over the arm and audits every
/// generation, with counts only: never a name or a health detail in the audit log.
/// </summary>
internal sealed class SafeguardingSheetBuilder(
    IArmRepository arms,
    IClassLevelRepository classLevels,
    IAcademicSessionRepository sessions,
    IPupilRepository pupils,
    IPupilRecordRepository records,
    ISchoolImageStore store,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
{
    /// <summary>The audit action for each generation of the sheet.</summary>
    public const string AuditAction = "pupil.safeguarding.sheet";

    /// <summary>Thumbnails fetched at once; enough to keep a class of forty quick without flooding the store.</summary>
    private const int PhotoConcurrency = 6;

    /// <summary>The validators' rule: present and a GUID.</summary>
    public static bool IsArmId(string? value) => Guid.TryParse(value, out var id) && id != Guid.Empty;

    /// <summary>The sheet, and each pupil's thumbnail bytes for the PDF.</summary>
    public async Task<Result<(SafeguardingSheetDto Sheet, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Photos)>> BuildAsync(
        Guid armId, string format, CancellationToken cancellationToken)
    {
        var grants = await effectivePrivilegeProvider.GetAllGrantsAsync(currentUser.UserId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);

        // Only grants in the class's session count (TASK-0060).
        var scope = PupilAccessGuard.Resolve(grants, Privileges.Pupil.SafeguardingView, arm?.SessionId);
        if (scope == PupilAccessScope.Forbidden
            || (scope == PupilAccessScope.ArmRestricted
                && !PupilAccessGuard.ResolveArmIds(grants, Privileges.Pupil.SafeguardingView, arm?.SessionId).Contains(armId)))
        {
            return Failure(Error.Forbidden("report.forbidden", $"You do not hold {Privileges.Pupil.SafeguardingView} for this class."));
        }

        if (arm is null)
        {
            return Failure(Error.NotFound("arm.not_found", "No class was found with that id."));
        }

        var levels = await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var levelName = levels.FirstOrDefault(level => level.Id == arm.ClassLevelId)?.Name;
        var armName = levelName is null ? arm.Label : ArmDisplayName.Compose(levelName, arm.Label);
        var session = await sessions.FindReadOnlyByIdAsync(arm.SessionId, cancellationToken).ConfigureAwait(false);

        var enrolled = (await pupils.ListActiveEnrolledInArmAsync(armId, cancellationToken).ConfigureAwait(false))
            .OrderBy(pupil => pupil.Surname, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pupil => pupil.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var set = await records.LoadSafeguardingForPupilsAsync([.. enrolled.Select(pupil => pupil.Id)], cancellationToken).ConfigureAwait(false);
        var photos = await ReadThumbnailsAsync(enrolled, cancellationToken).ConfigureAwait(false);

        var rows = enrolled.ConvertAll(pupil => Row(pupil, set, photos.TryGetValue(Id(pupil.Id), out var photo) ? photo : (ReadOnlyMemory<byte>?)null));
        var now = timeProvider.GetUtcNow();

        await auditSink.RecordAsync(
            AuditAction, "arm", Id(armId),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["format"] = format, ["pupils"] = rows.Count },
            currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        var sheet = new SafeguardingSheetDto(Id(armId), armName, session?.Name ?? string.Empty, now, rows);
        return Result.Success<(SafeguardingSheetDto, IReadOnlyDictionary<string, ReadOnlyMemory<byte>>)>((sheet, photos));
    }

    /// <summary>
    /// The class's thumbnails, a few at a time. One that cannot be fetched is left out (the sheet prints "No photo"): a gate
    /// list must never fail over a single photograph.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, ReadOnlyMemory<byte>>> ReadThumbnailsAsync(
        IReadOnlyList<Pupil> enrolled, CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(PhotoConcurrency);
        var fetches = enrolled
            .Where(pupil => pupil.PhotoThumbnailAssetId is not null)
            .Select(async pupil =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await using var stream = await store.OpenAsync(pupil.PhotoThumbnailAssetId!, cancellationToken).ConfigureAwait(false);
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                    return (Id: Id(pupil.Id), Bytes: (ReadOnlyMemory<byte>?)buffer.ToArray());
                }
                catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException && !cancellationToken.IsCancellationRequested)
                {
                    return (Id: Id(pupil.Id), Bytes: (ReadOnlyMemory<byte>?)null);
                }
                finally
                {
                    gate.Release();
                }
            });

        var results = await Task.WhenAll(fetches).ConfigureAwait(false);
        return results.Where(result => result.Bytes is not null)
            .ToDictionary(result => result.Id, result => result.Bytes!.Value, StringComparer.Ordinal);
    }

    private static Result<(SafeguardingSheetDto Sheet, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Photos)> Failure(Error error) =>
        Result.Failure<(SafeguardingSheetDto, IReadOnlyDictionary<string, ReadOnlyMemory<byte>>)>(error);

    private static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private static SafeguardingSheetRowDto Row(Pupil pupil, SafeguardingRecordSet set, ReadOnlyMemory<byte>? photo)
    {
        var health = set.Health.GetValueOrDefault(pupil.Id);
        var barred = set.Barred.GetValueOrDefault(pupil.Id);
        var hospital = string.Join(
            ", ", new[] { health?.PreferredHospital, LocalPhone(health?.HospitalPhone) }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return new SafeguardingSheetRowDto(
            Id(pupil.Id),
            pupil.RegistrationNumber,
            string.Join(' ', new[] { pupil.Surname.ToUpperInvariant(), pupil.FirstName, pupil.MiddleName }.Where(part => !string.IsNullOrWhiteSpace(part))),
            // An empty photo (never expected) is no photo, as the PDF renderer treats it.
            photo is { Length: > 0 } bytes ? "data:image/jpeg;base64," + Convert.ToBase64String(bytes.Span) : null,
            Answer(health?.HasAllergy, health?.AllergyDetails),
            Answer(health?.HasMedicalCondition, health?.MedicalConditionDetails),
            Answer(health?.TakesRegularMedication, health?.MedicationDetails),
            health?.SpecialInstructions ?? string.Empty,
            hospital,
            [.. set.Pickup[pupil.Id].OrderBy(person => person.DisplayOrder).Select(person => $"{person.FullName} ({person.Relationship}) {LocalPhone(person.Phone)}")],
            barred is null ? "Not asked" : barred.HasBarredPersons ? "Yes: see office" : "No");
    }

    /// <summary>A stored "+234…" number in the local "0…" form staff dial, as the rest of the product shows it.</summary>
    private static string? LocalPhone(string? phone) =>
        phone is not null && phone.StartsWith("+234", StringComparison.Ordinal) ? "0" + phone[4..] : phone;

    /// <summary>A yes/no health answer as the sheet prints it: never blank, so an unasked question is never read as "no".</summary>
    private static string Answer(bool? yes, string? details) => yes switch
    {
        null => "Not asked",
        false => "None",
        true => string.IsNullOrWhiteSpace(details) ? "Yes (no detail recorded)" : details,
    };
}
