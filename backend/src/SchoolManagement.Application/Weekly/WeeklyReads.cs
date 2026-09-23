using FluentValidation;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Weekly;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Application.Weekly;

/// <summary><c>GET /api/v1/pupils/{pupilId}/weekly?termId=</c> (spec 6.10.11): one pupil's whole term, week by week.</summary>
/// <param name="PupilId">From the route.</param>
/// <param name="TermId">The term.</param>
public sealed record GetPupilWeeklyQuery(string PupilId, string TermId) : IQuery<Result<PupilWeeklyTermDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetPupilWeeklyQueryValidator : AbstractValidator<GetPupilWeeklyQuery>
{
    public GetPupilWeeklyQueryValidator()
    {
        RuleFor(query => query.PupilId).Must(value => Guid.TryParse(value, out _)).WithMessage("PupilId must be a valid identifier.");
        RuleFor(query => query.TermId).Must(value => Guid.TryParse(value, out _)).WithMessage("TermId must be a valid identifier.");
    }
}

/// <summary>Handles <see cref="GetPupilWeeklyQuery"/>.</summary>
/// <remarks>
/// The privilege is checked here rather than by a route-level pupil scope, which cannot resolve a pupil with no open
/// enrolment and would refuse even a school-wide grant for a leaver whose earlier weeks still exist.
/// </remarks>
internal sealed class GetPupilWeeklyHandler(Pupils.Records.PupilRecordAccess access, ITermRepository terms, IWeeklyReportRepository weekly)
    : IRequestHandler<GetPupilWeeklyQuery, Result<PupilWeeklyTermDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilWeeklyTermDto>> HandleAsync(GetPupilWeeklyQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(Guid.Parse(request.PupilId), Domain.Security.Privileges.Weekly.View, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilWeeklyTermDto>(allowed.Error);
        }

        var pupil = allowed.Value;

        if (await terms.FindReadOnlyByIdAsync(Guid.Parse(request.TermId), cancellationToken).ConfigureAwait(false) is not { } term)
        {
            return Result.Failure<PupilWeeklyTermDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var reports = await weekly.ListPupilTermAsync(pupil.Id, term.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(new PupilWeeklyTermDto(
            WeeklyProjection.Id(pupil.Id),
            pupil.RegistrationNumber,
            WeeklyProjection.DisplayName(pupil.Surname, pupil.FirstName, pupil.MiddleName),
            WeeklyProjection.Id(term.Id),
            PupilWeeks(TermWeeks.Derive(term.StartDate, term.EndDate), reports)));
    }

    /// <summary>One row per derived week, plus any outside-term week holding notes. Shared with the portal.</summary>
    internal static IReadOnlyList<PupilWeeklyWeekDto> PupilWeeks(IReadOnlyList<TermWeek> derived, IReadOnlyList<WeeklyReportSnapshot> reports)
    {
        var byWeek = reports.ToDictionary(report => report.WeekNumber);
        var weeks = derived
            .Select(week => Week(week.Number, week.StartDate, week.EndDate, byWeek.GetValueOrDefault(week.Number), outside: false))
            .ToList();
        weeks.AddRange(reports.Where(report => report.WeekNumber > derived.Count)
            .Select(report => Week(report.WeekNumber, report.StartDate, report.EndDate, report, outside: true)));
        return weeks;

        PupilWeeklyWeekDto Week(int number, DateOnly start, DateOnly end, WeeklyReportSnapshot? report, bool outside) =>
            new(number, report?.StartDate ?? start, report?.EndDate ?? end, outside || (report is not null && report.StartDate != start),
                report is null ? null : WeeklyProjection.Id(report.ArmId), report?.State == WeeklyReportState.Published,
                report is null ? null : WeeklyProjection.Days(report, report.StartDate));
    }
}

/// <summary><c>GET /api/v1/reports/weekly-completion?termId=</c> (spec 6.10.12): per arm per week, who wrote what.</summary>
/// <param name="TermId">The term.</param>
/// <param name="WeekNumber">Optional: one week only.</param>
public sealed record GetWeeklyCompletionQuery(string TermId, int? WeekNumber) : IQuery<Result<WeeklyCompletionReportDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetWeeklyCompletionQueryValidator : AbstractValidator<GetWeeklyCompletionQuery>
{
    public GetWeeklyCompletionQueryValidator()
    {
        RuleFor(query => query.TermId).Must(value => Guid.TryParse(value, out _)).WithMessage("TermId must be a valid identifier.");
        RuleFor(query => query.WeekNumber).InclusiveBetween(1, TermWeeks.MaxWeeks).When(query => query.WeekNumber is not null);
    }
}

/// <summary>Handles <see cref="GetWeeklyCompletionQuery"/>. Every active arm of the term's session appears for every week.</summary>
internal sealed class GetWeeklyCompletionHandler(
    ITermRepository terms,
    IArmRepository arms,
    IClassLevelRepository levels,
    IWeeklyReportRepository weekly,
    IWeeklyNameLookup names)
    : IRequestHandler<GetWeeklyCompletionQuery, Result<WeeklyCompletionReportDto>>
{
    /// <inheritdoc />
    public async Task<Result<WeeklyCompletionReportDto>> HandleAsync(GetWeeklyCompletionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await terms.FindReadOnlyByIdAsync(Guid.Parse(request.TermId), cancellationToken).ConfigureAwait(false) is not { } term)
        {
            return Result.Failure<WeeklyCompletionReportDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var armNames = await WeeklyArmNames.ForSessionAsync(arms, levels, term.SessionId, cancellationToken).ConfigureAwait(false);
        var totals = (await weekly.SummariseAsync(term.Id, armId: null, cancellationToken).ConfigureAwait(false))
            .ToDictionary(total => (total.ArmId, total.WeekNumber));
        var editors = await names.StaffNamesAsync(
            totals.Values.Select(total => total.LastEditedById).OfType<string>().Distinct(StringComparer.Ordinal).ToList(), cancellationToken)
            .ConfigureAwait(false);
        var weeks = TermWeeks.Derive(term.StartDate, term.EndDate).Where(week => request.WeekNumber is null || week.Number == request.WeekNumber).ToList();

        var rosters = await names.RosterCountsAsync(armNames.Keys, cancellationToken).ConfigureAwait(false);
        var items = new List<WeeklyCompletionRowDto>();
        foreach (var (armId, armName) in armNames.OrderBy(pair => pair.Value, StringComparer.Ordinal))
        {
            var onRoll = rosters.GetValueOrDefault(armId);
            foreach (var week in weeks)
            {
                var total = totals.GetValueOrDefault((armId, week.Number));
                items.Add(new WeeklyCompletionRowDto(
                    WeeklyProjection.Id(armId), armName, week.Number, week.StartDate, week.EndDate, onRoll,
                    total?.PupilsWithNotes ?? 0, total?.CellsFilled ?? 0, onRoll * 5 * Enum.GetValues<WeeklyField>().Length,
                    total?.Published ?? false, total?.LastEditedAt,
                    total?.LastEditedById is { } editor ? editors.GetValueOrDefault(editor) : null));
            }
        }

        return Result.Success(new WeeklyCompletionReportDto(WeeklyProjection.Id(term.Id), items));
    }
}

/// <summary>
/// <c>GET /api/v1/reports/weekly-illness?termId=</c> (spec 6.10.12): pupils with symptoms recorded on two or more days of
/// the term, with the dates and the text. Health observation about children, so the route also needs the safeguarding
/// privilege.
/// </summary>
/// <param name="TermId">The term.</param>
public sealed record GetWeeklyIllnessQuery(string TermId) : IQuery<Result<WeeklyIllnessReportDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetWeeklyIllnessQueryValidator : AbstractValidator<GetWeeklyIllnessQuery>
{
    public GetWeeklyIllnessQueryValidator() =>
        RuleFor(query => query.TermId).Must(value => Guid.TryParse(value, out _)).WithMessage("TermId must be a valid identifier.");
}

/// <summary>Handles <see cref="GetWeeklyIllnessQuery"/>.</summary>
internal sealed class GetWeeklyIllnessHandler(
    ITermRepository terms, IArmRepository arms, IClassLevelRepository levels, IWeeklyReportRepository weekly, IWeeklyNameLookup names)
    : IRequestHandler<GetWeeklyIllnessQuery, Result<WeeklyIllnessReportDto>>
{
    /// <summary>Spec 6.10.12: two or more days.</summary>
    public const int MinimumDays = 2;

    /// <inheritdoc />
    public async Task<Result<WeeklyIllnessReportDto>> HandleAsync(GetWeeklyIllnessQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await terms.FindReadOnlyByIdAsync(Guid.Parse(request.TermId), cancellationToken).ConfigureAwait(false) is not { } term)
        {
            return Result.Failure<WeeklyIllnessReportDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var armNames = await WeeklyArmNames.ForSessionAsync(arms, levels, term.SessionId, cancellationToken).ConfigureAwait(false);
        var groups = (await weekly.ListSymptomsAsync(term.Id, cancellationToken).ConfigureAwait(false))
            .GroupBy(entry => entry.PupilId).Where(group => group.Count() >= MinimumDays).ToList();
        var pupils = (await names.PupilsAsync(groups.Select(group => group.Key).ToList(), cancellationToken).ConfigureAwait(false))
            .ToDictionary(pupil => pupil.PupilId);
        var items = new List<WeeklyIllnessRowDto>();
        foreach (var group in groups)
        {
            if (pupils.GetValueOrDefault(group.Key) is not { } pupil)
            {
                continue;
            }

            var ordered = group.OrderBy(entry => entry.Date).ToList();
            items.Add(new WeeklyIllnessRowDto(
                WeeklyProjection.Id(pupil.PupilId),
                pupil.RegistrationNumber,
                pupil.DisplayName,
                armNames.GetValueOrDefault(ordered[^1].ArmId) ?? string.Empty,
                ordered.Select(entry => new WeeklyIllnessObservationDto(entry.Date, entry.Text)).ToList()));
        }

        return Result.Success(new WeeklyIllnessReportDto(
            WeeklyProjection.Id(term.Id), items.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToList()));
    }
}

/// <summary>A pupil's name for a weekly-report row.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="RegistrationNumber">Null only if unissued.</param>
/// <param name="DisplayName">"Surname First Middle".</param>
public sealed record WeeklyPupilName(Guid PupilId, string? RegistrationNumber, string DisplayName);

/// <summary>Batched lookups for the weekly reads, so no report runs one query per arm or per pupil.</summary>
public interface IWeeklyNameLookup
{
    /// <summary>Id to staff name, for the ids that exist.</summary>
    Task<IReadOnlyDictionary<string, string>> StaffNamesAsync(IReadOnlyCollection<string> accountIds, CancellationToken cancellationToken);

    /// <summary>The named pupils, whatever their status.</summary>
    Task<IReadOnlyList<WeeklyPupilName>> PupilsAsync(IReadOnlyCollection<Guid> pupilIds, CancellationToken cancellationToken);

    /// <summary>Active-roster size per arm (open enrolment, Active pupil), for the given arms.</summary>
    Task<IReadOnlyDictionary<Guid, int>> RosterCountsAsync(IReadOnlyCollection<Guid> armIds, CancellationToken cancellationToken);
}

/// <summary>"Level plus arm label" for every arm of a session.</summary>
internal static class WeeklyArmNames
{
    public static async Task<Dictionary<Guid, string>> ForSessionAsync(
        IArmRepository arms, IClassLevelRepository levels, Guid sessionId, CancellationToken cancellationToken)
    {
        var levelNames = (await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(level => level.Id, level => level.Name);
        return (await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
            .Where(arm => arm.SessionId == sessionId && arm.Status == ArmStatus.Active)
            .ToDictionary(arm => arm.Id, arm => ArmDisplayName.Compose(levelNames.GetValueOrDefault(arm.ClassLevelId) ?? string.Empty, arm.Label));
    }
}
