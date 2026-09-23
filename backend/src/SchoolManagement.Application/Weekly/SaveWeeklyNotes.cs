using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Weekly;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Application.Weekly;

/// <summary>One cell of the weekly grid: one line, one day, one pupil.</summary>
/// <param name="PupilId">On the arm's active roster, or already holding notes in this arm's week.</param>
/// <param name="DayOfWeek">Monday to Friday.</param>
/// <param name="Field">Which of the eight lines.</param>
/// <param name="Value">The text; null or blank clears the line.</param>
public sealed record WeeklyCellInput(string PupilId, WeeklyDay DayOfWeek, WeeklyField Field, string? Value);

/// <summary>
/// <c>PUT /api/v1/arms/{armId}/weekly</c> (spec 6.10.11): sparse bulk upsert of one week's grid, one transaction. Only the
/// cells sent are touched, so a Fill down writes only what it filled. Last write wins (spec 6.10.10); no version.
/// </summary>
/// <param name="ArmId">From the route.</param>
/// <param name="TermId">The term.</param>
/// <param name="WeekNumber">The week.</param>
/// <param name="Cells">The cells to write.</param>
public sealed record SaveWeeklyNotesCommand(string ArmId, string TermId, int WeekNumber, IReadOnlyList<WeeklyCellInput> Cells) : ICommand<Result>;

/// <summary>Structural checks, including each line's length.</summary>
internal sealed class SaveWeeklyNotesCommandValidator : AbstractValidator<SaveWeeklyNotesCommand>
{
    /// <summary>A whole 60-pupil arm filled on every line is 2,400 cells.</summary>
    public const int MaxCells = 5000;

    public SaveWeeklyNotesCommandValidator()
    {
        RuleFor(command => command.ArmId).Must(value => Guid.TryParse(value, out _)).WithMessage("ArmId must be a valid identifier.");
        RuleFor(command => command.TermId).Must(value => Guid.TryParse(value, out _)).WithMessage("TermId must be a valid identifier.");
        RuleFor(command => command.WeekNumber).InclusiveBetween(1, TermWeeks.MaxWeeks);
        RuleFor(command => command.Cells).NotNull().Must(cells => cells.Count <= MaxCells)
            .WithMessage($"At most {MaxCells} cells can be saved at once.");
        RuleForEach(command => command.Cells).ChildRules(cell =>
        {
            cell.RuleFor(entry => entry.PupilId).Must(value => Guid.TryParse(value, out _)).WithMessage("PupilId must be a valid identifier.");
            cell.RuleFor(entry => entry.DayOfWeek).IsInEnum();
            cell.RuleFor(entry => entry.Field).IsInEnum();
            cell.RuleFor(entry => entry)
                .Must(entry => WeeklyReportDay.Normalise(entry.Value) is not { } text || text.Length <= WeeklyReportDay.MaxLengthOf(entry.Field))
                .WithName("Value")
                .WithMessage(entry => $"This line allows at most {WeeklyReportDay.MaxLengthOf(entry.Field)} characters.");
        });
    }
}

/// <summary>Handles <see cref="SaveWeeklyNotesCommand"/>. The day rows are created with the report on the first note.</summary>
internal sealed class SaveWeeklyNotesHandler(
    IArmRepository arms,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    IEnrolmentRepository enrolments,
    IWeeklyReportRepository weekly,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<SaveWeeklyNotesCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> HandleAsync(SaveWeeklyNotesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        if (await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false) is not { } arm)
        {
            return Result.Failure(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        if (await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false) is not { } term || term.SessionId != arm.SessionId)
        {
            return Result.Failure(Error.NotFound("term.not_found", "No term was found with that id in this arm's session."));
        }

        if (await sessions.FindReadOnlyByIdAsync(arm.SessionId, cancellationToken).ConfigureAwait(false) is { State: SessionState.Closed })
        {
            return Result.Failure(Error.Conflict("weekly.session_closed", "This arm's session is closed. Weekly notes cannot be edited."));
        }

        if (term.State == TermState.Closed)
        {
            return Result.Failure(Error.Conflict("weekly.term_closed", $"{term.Name} is closed. Weekly notes cannot be edited."));
        }

        var derivedWeek = TermWeeks.Derive(term.StartDate, term.EndDate).FirstOrDefault(week => week.Number == request.WeekNumber);
        var armWeek = await weekly.ListArmWeekTrackedAsync(armId, termId, request.WeekNumber, cancellationToken).ConfigureAwait(false);
        if (derivedWeek is null && armWeek.Count == 0)
        {
            return Result.Failure(Error.NotFound("weekly.week_not_found", $"This term has no week {request.WeekNumber}."));
        }

        var roster = (await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false))
            .Select(pupil => pupil.PupilId).ToHashSet();
        var pupilIds = request.Cells.Select(cell => Guid.Parse(cell.PupilId)).ToHashSet();
        var reports = (await weekly.ListTrackedAsync(termId, request.WeekNumber, pupilIds, cancellationToken).ConfigureAwait(false))
            .ToDictionary(report => report.PupilId);

        var failures = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var index = 0; index < request.Cells.Count; index++)
        {
            var pupilId = Guid.Parse(request.Cells[index].PupilId);
            if (reports.TryGetValue(pupilId, out var existing) && existing.ArmId != armId)
            {
                failures[$"Cells[{index}].PupilId"] = ["This pupil's week is recorded against another arm."];
            }
            else if (existing is null && !roster.Contains(pupilId))
            {
                failures[$"Cells[{index}].PupilId"] = ["This pupil is not on the arm's active roster."];
            }
            else if (existing is null && derivedWeek is null)
            {
                failures[$"Cells[{index}].PupilId"] = ["This week falls outside the term's dates; only existing notes can be edited."];
            }
        }

        if (failures.Count > 0)
        {
            return Result.Failure(new ValidationError(failures));
        }

        var published = armWeek.FirstOrDefault(report => report.State == WeeklyReportState.Published);
        var changes = new List<object?>();
        var before = new List<object?>();
        foreach (var cell in request.Cells)
        {
            var pupilId = Guid.Parse(cell.PupilId);
            if (!reports.TryGetValue(pupilId, out var report))
            {
                if (WeeklyReportDay.Normalise(cell.Value) is null)
                {
                    continue; // Clearing a cell that was never written creates nothing.
                }

                var creation = WeeklyReport.Create(Guid.CreateVersion7(), pupilId, armId, termId, derivedWeek!, published is not null, published?.PublishedAtUtc);
                if (creation.IsFailure)
                {
                    return Result.Failure(creation.Error);
                }

                report = creation.Value;
                await weekly.AddAsync(report, cancellationToken).ConfigureAwait(false);
                reports[pupilId] = report;
            }

            var previous = report.Day(cell.DayOfWeek).Get(cell.Field);
            if (report.SetNote(cell.DayOfWeek, cell.Field, cell.Value))
            {
                before.Add(Change(cell, previous));
                changes.Add(Change(cell, report.Day(cell.DayOfWeek).Get(cell.Field)));
            }
        }

        // Spec 6.10.10: a Parent's Comment a teacher should not have transcribed is edited, and the audit log keeps the
        // previous value.
        if (changes.Count > 0)
        {
            await auditSink.RecordAsync(
                Privileges.Weekly.Enter,
                "weekly_report",
                entityId: null,
                Metadata(request, changes),
                currentUser.UserId,
                cancellationToken,
                reason: null,
                beforeMetadata: Metadata(request, before))
                .ConfigureAwait(false);
        }

        return Result.Success();
    }

    private static Dictionary<string, object?> Change(WeeklyCellInput cell, string? value) => new(StringComparer.Ordinal)
    {
        ["pupilId"] = cell.PupilId,
        ["day"] = cell.DayOfWeek.ToString(),
        ["field"] = cell.Field.ToString(),
        ["value"] = value,
    };

    private static Dictionary<string, object?> Metadata(SaveWeeklyNotesCommand request, List<object?> changes) => new(StringComparer.Ordinal)
    {
        ["armId"] = request.ArmId,
        ["termId"] = request.TermId,
        ["weekNumber"] = request.WeekNumber,
        ["changes"] = changes,
    };
}
