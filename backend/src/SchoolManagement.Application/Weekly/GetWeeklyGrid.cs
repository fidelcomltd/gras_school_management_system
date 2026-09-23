using FluentValidation;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Weekly;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Application.Weekly;

/// <summary>
/// <c>GET /api/v1/arms/{armId}/weekly?termId=&amp;weekNumber=</c> (spec 6.10.11): the whole arm's grid for one week, in
/// one response. Without <c>weekNumber</c> it opens the week containing today.
/// </summary>
/// <param name="ArmId">From the route.</param>
/// <param name="TermId">The term.</param>
/// <param name="WeekNumber">1 to 20, or null for the current week.</param>
public sealed record GetWeeklyGridQuery(string ArmId, string TermId, int? WeekNumber) : IQuery<Result<WeeklyGridDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetWeeklyGridQueryValidator : AbstractValidator<GetWeeklyGridQuery>
{
    public GetWeeklyGridQueryValidator()
    {
        RuleFor(query => query.ArmId).Must(value => Guid.TryParse(value, out _)).WithMessage("ArmId must be a valid identifier.");
        RuleFor(query => query.TermId).Must(value => Guid.TryParse(value, out _)).WithMessage("TermId must be a valid identifier.");
        RuleFor(query => query.WeekNumber).InclusiveBetween(1, TermWeeks.MaxWeeks).When(query => query.WeekNumber is not null);
    }
}

/// <summary>Handles <see cref="GetWeeklyGridQuery"/>.</summary>
internal sealed class GetWeeklyGridHandler(
    IArmRepository arms,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    IEnrolmentRepository enrolments,
    IWeeklyReportRepository weekly,
    IWeeklyNameLookup names,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetWeeklyGridQuery, Result<WeeklyGridDto>>
{
    /// <summary>How many remembered phrases each line offers.</summary>
    public const int PhrasesPerField = 12;

    /// <inheritdoc />
    public async Task<Result<WeeklyGridDto>> HandleAsync(GetWeeklyGridQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        if (await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false) is not { } arm)
        {
            return Result.Failure<WeeklyGridDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        if (await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false) is not { } term || term.SessionId != arm.SessionId)
        {
            return Result.Failure<WeeklyGridDto>(Error.NotFound("term.not_found", "No term was found with that id in this arm's session."));
        }

        var derived = TermWeeks.Derive(term.StartDate, term.EndDate);
        var totals = await weekly.SummariseAsync(termId, armId, cancellationToken).ConfigureAwait(false);
        var weeks = WeeklyProjection.Weeks(derived, totals);
        var weekNumber = request.WeekNumber ?? WeeklyProjection.CurrentWeek(derived, WeeklyProjection.LagosToday(timeProvider.GetUtcNow()));
        if (weeks.FirstOrDefault(week => week.WeekNumber == weekNumber) is not { } week)
        {
            return Result.Failure<WeeklyGridDto>(Error.NotFound("weekly.week_not_found", $"This term has no week {weekNumber}."));
        }

        var reports = await weekly.ListArmWeekAsync(armId, termId, weekNumber, cancellationToken).ConfigureAwait(false);
        var byPupil = reports.ToDictionary(report => report.PupilId);
        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);

        var rows = roster
            .Select(pupil => Row(pupil.PupilId, pupil.RegistrationNumber, WeeklyProjection.DisplayName(pupil.Surname, pupil.FirstName, pupil.MiddleName), onRoll: true))
            .ToList();

        // A pupil who has notes here but has since left the arm keeps them, attributed to this arm (spec 6.10.10).
        var rosterIds = roster.Select(pupil => pupil.PupilId).ToHashSet();
        var leavers = await names.PupilsAsync(reports.Select(report => report.PupilId).Where(id => !rosterIds.Contains(id)).ToList(), cancellationToken)
            .ConfigureAwait(false);
        rows.AddRange(leavers.Select(pupil => Row(pupil.PupilId, pupil.RegistrationNumber, pupil.DisplayName, onRoll: false)));

        var session = await sessions.FindReadOnlyByIdAsync(term.SessionId, cancellationToken).ConfigureAwait(false);
        var locked = term.State == TermState.Closed || session is { State: SessionState.Closed };

        var phrases = currentUser.UserId is { } actor
            ? await weekly.ListPhrasesAsync(termId, actor, PhrasesPerField, cancellationToken).ConfigureAwait(false)
            : new Dictionary<WeeklyField, IReadOnlyList<string>>();

        return Result.Success(new WeeklyGridDto(
            WeeklyProjection.Id(armId),
            WeeklyProjection.Id(termId),
            weekNumber,
            week.StartDate,
            week.EndDate,
            week.OutsideTerm,
            week.Published,
            reports.Where(report => report.State == WeeklyReportState.Published).Max(report => report.PublishedAt),
            await weekly.IsAutoPublishAsync(armId, cancellationToken).ConfigureAwait(false),
            locked,
            rows.OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.PupilId, StringComparer.Ordinal).ToList(),
            weeks,
            WeeklyProjection.Phrases(phrases)));

        WeeklyGridRowDto Row(Guid pupilId, string? registrationNumber, string displayName, bool onRoll)
        {
            var report = byPupil.GetValueOrDefault(pupilId);
            var illnessDays = report?.Days.Count(day => day.Get(WeeklyField.SymptomsOfIllness) is not null) ?? 0;
            return new WeeklyGridRowDto(
                WeeklyProjection.Id(pupilId), registrationNumber, displayName, onRoll, illnessDays, WeeklyProjection.Days(report, week.StartDate));
        }
    }
}

/// <summary><c>GET /api/v1/terms/{termId}/weeks</c> (spec 6.10.11): the derived week list. No stored rows are needed.</summary>
/// <param name="TermId">From the route.</param>
public sealed record GetTermWeeksQuery(string TermId) : IQuery<Result<TermWeekListResponse>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetTermWeeksQueryValidator : AbstractValidator<GetTermWeeksQuery>
{
    public GetTermWeeksQueryValidator() =>
        RuleFor(query => query.TermId).Must(value => Guid.TryParse(value, out _)).WithMessage("TermId must be a valid identifier.");
}

/// <summary>Handles <see cref="GetTermWeeksQuery"/>.</summary>
internal sealed class GetTermWeeksHandler(ITermRepository terms) : IRequestHandler<GetTermWeeksQuery, Result<TermWeekListResponse>>
{
    /// <inheritdoc />
    public async Task<Result<TermWeekListResponse>> HandleAsync(GetTermWeeksQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await terms.FindReadOnlyByIdAsync(Guid.Parse(request.TermId), cancellationToken).ConfigureAwait(false) is not { } term)
        {
            return Result.Failure<TermWeekListResponse>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var items = TermWeeks.Derive(term.StartDate, term.EndDate).Select(week => new TermWeekDto(week.Number, week.StartDate, week.EndDate)).ToList();
        return Result.Success(new TermWeekListResponse(WeeklyProjection.Id(term.Id), items));
    }
}
