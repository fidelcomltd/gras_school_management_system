using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Records;

/// <summary>
/// <c>GET /api/v1/reports/incomplete-records</c> (spec 6.5.12): the office's chasing list. Every active pupil in the active
/// session with something still missing, what it is, and how many pupils share each gap.
/// </summary>
/// <param name="ArmId">Optional: one arm only.</param>
public sealed record GetIncompleteRecordsQuery(string? ArmId) : IQuery<Result<IncompleteRecordsReportDto>>;

/// <summary>The arm, when given, is a GUID.</summary>
internal sealed class GetIncompleteRecordsQueryValidator : AbstractValidator<GetIncompleteRecordsQuery>
{
    public GetIncompleteRecordsQueryValidator() =>
        RuleFor(query => query.ArmId).Must(id => Guid.TryParse(id, out _)).When(query => query.ArmId is not null)
            .WithMessage("ArmId must be a GUID.");
}

/// <summary>The report.</summary>
/// <param name="SessionName">The active session, or null when none is active (the report is then empty).</param>
/// <param name="PupilsChecked">Active pupils in scope.</param>
/// <param name="Counts">Each gap and how many of the pupils below have it, most common first.</param>
/// <param name="Pupils">Every pupil in scope with at least one gap, by arm then surname.</param>
public sealed record IncompleteRecordsReportDto(
    string? SessionName, int PupilsChecked, IReadOnlyList<IncompleteRecordsCountDto> Counts, IReadOnlyList<IncompleteRecordDto> Pupils);

/// <summary>One gap across the report.</summary>
/// <param name="Code">The completeness code, as on each pupil's items.</param>
/// <param name="Label">A short name for it, for a filter or a heading.</param>
/// <param name="Required">True for what approval requires (spec 6.5.12), missing only after an import or an override.</param>
/// <param name="Count">Pupils with this gap.</param>
public sealed record IncompleteRecordsCountDto(string Code, string Label, bool Required, int Count);

/// <summary>One pupil with gaps.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="RegistrationNumber">Their number.</param>
/// <param name="Surname">Surname.</param>
/// <param name="FirstName">First name.</param>
/// <param name="MiddleName">Middle name.</param>
/// <param name="ArmId">Their current arm.</param>
/// <param name="ArmName">Its display name.</param>
/// <param name="ChasedPercent">Completeness across the chased set, as on the record.</param>
/// <param name="Required">Required items still missing.</param>
/// <param name="Chased">Chased items missing.</param>
public sealed record IncompleteRecordDto(
    string PupilId,
    string? RegistrationNumber,
    string Surname,
    string FirstName,
    string? MiddleName,
    string ArmId,
    string ArmName,
    int ChasedPercent,
    IReadOnlyList<CompletenessItemDto> Required,
    IReadOnlyList<CompletenessItemDto> Chased);

/// <summary>Handles <see cref="GetIncompleteRecordsQuery"/>.</summary>
/// <remarks>
/// <c>report.view</c> is arm-scopable: a school-wide grant sees every arm, an arm-restricted one only its arms, checked here
/// because the report spans arms. Codes only, never any health or barred-person content, so the report needs no
/// safeguarding privilege; a missing health answer is shown as missing, never what an answer says.
/// </remarks>
internal sealed class GetIncompleteRecordsHandler(
    IAcademicSessionRepository sessions,
    IClassLevelRepository classLevels,
    IArmRepository arms,
    IPupilRepository pupils,
    IPupilRecordRepository records,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser)
    : IRequestHandler<GetIncompleteRecordsQuery, Result<IncompleteRecordsReportDto>>
{
    /// <inheritdoc />
    public async Task<Result<IncompleteRecordsReportDto>> HandleAsync(GetIncompleteRecordsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var grants = await effectivePrivilegeProvider.GetGrantsAsync(currentUser.UserId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        var scope = PupilAccessGuard.Resolve(grants, Privileges.Report.View);
        var allowedArms = scope == PupilAccessScope.ArmRestricted ? PupilAccessGuard.ResolveArmIds(grants, Privileges.Report.View) : null;
        var armFilter = request.ArmId is null ? (Guid?)null : Guid.Parse(request.ArmId);
        if (scope == PupilAccessScope.Forbidden || (armFilter is { } wanted && allowedArms is not null && !allowedArms.Contains(wanted)))
        {
            return Result.Failure<IncompleteRecordsReportDto>(Error.Forbidden(
                "report.forbidden", "You do not have access to that arm's records."));
        }

        var session = await sessions.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Success(new IncompleteRecordsReportDto(null, 0, [], []));
        }

        var enrolled = (await pupils.ListActiveEnrolledInSessionAsync(session.Id, cancellationToken).ConfigureAwait(false))
            .Where(row => (armFilter is null || row.ArmId == armFilter) && (allowedArms is null || allowedArms.Contains(row.ArmId)))
            .ToList();
        var set = await records.LoadForPupilsAsync([.. enrolled.Select(row => row.Pupil.Id)], cancellationToken).ConfigureAwait(false);
        var names = await ArmNamesAsync(cancellationToken).ConfigureAwait(false);

        var incomplete = enrolled
            .Select(row => (row.Pupil, row.ArmId, Completeness: AdmissionCompleteness.Evaluate(row.Pupil, set)))
            .Where(row => row.Completeness.Blocking.Count + row.Completeness.Chased.Count > 0)
            .OrderBy(row => names.GetValueOrDefault(row.ArmId).Order)
            .ThenBy(row => row.Pupil.Surname, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Pupil.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var counts = incomplete
            .SelectMany(row => row.Completeness.Blocking.Select(item => (Item: item, Required: true))
                .Concat(row.Completeness.Chased.Select(item => (Item: item, Required: false))))
            .GroupBy(entry => entry.Item.Code, StringComparer.Ordinal)
            .Select(group => new IncompleteRecordsCountDto(group.Key, LabelFor(group.First().Item), group.First().Required, group.Count()))
            .OrderByDescending(count => count.Required)
            .ThenByDescending(count => count.Count)
            .ThenBy(count => count.Label, StringComparer.Ordinal)
            .ToList();

        var rows = incomplete.ConvertAll(row => new IncompleteRecordDto(
            row.Pupil.Id.ToString("D", CultureInfo.InvariantCulture),
            row.Pupil.RegistrationNumber,
            row.Pupil.Surname,
            row.Pupil.FirstName,
            row.Pupil.MiddleName,
            row.ArmId.ToString("D", CultureInfo.InvariantCulture),
            names.TryGetValue(row.ArmId, out var arm) ? arm.Name : string.Empty,
            row.Completeness.ChasedPercent,
            row.Completeness.Blocking,
            row.Completeness.Chased));

        return Result.Success(new IncompleteRecordsReportDto(session.Name, enrolled.Count, counts, rows));
    }

    // Short names for a count or a filter; the item's own message stays the per-pupil instruction.
    private static string LabelFor(CompletenessItemDto item) => item.Code switch
    {
        "contacts.responsible_adult" => "No father, mother or guardian",
        "contacts.emergency_primary" => "No primary emergency contact",
        "contacts.primary" => "No primary contact marked",
        "collection.barred_unanswered" => "Barred-persons question unanswered",
        AdmissionCompleteness.HealthUnansweredCode => "Health questions unanswered",
        "declaration.unsigned" => "Declaration not recorded",
        "assessment.outcome" => "Assessment outcome not recorded",
        "pupil.previous_school" => "No previous school",
        "collection.pickup" => "No authorised pickup persons",
        "health.hospital" => "No preferred hospital",
        "health.blood_group" => "No blood group",
        "health.genotype" => "No genotype",
        "pupil.other_information" => "No other information",
        _ when item.Code.StartsWith("documents.", StringComparison.Ordinal) => $"{item.Message.TrimEnd('.')} not received",
        _ => item.Message.TrimEnd('.'),
    };

    private async Task<Dictionary<Guid, (string Name, int Order)>> ArmNamesAsync(CancellationToken cancellationToken)
    {
        var levels = (await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(level => level.Id);
        var ordered = (await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
            .Where(arm => levels.ContainsKey(arm.ClassLevelId))
            .OrderBy(arm => levels[arm.ClassLevelId].ProgressionOrder)
            .ThenBy(arm => arm.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return ordered
            .Select((arm, index) => (arm, index))
            .ToDictionary(entry => entry.arm.Id, entry => (ArmDisplayName.Compose(levels[entry.arm.ClassLevelId].Name, entry.arm.Label), entry.index));
    }
}
