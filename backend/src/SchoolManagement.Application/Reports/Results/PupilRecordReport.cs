using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Pupil cumulative record filters.</summary>
/// <param name="PupilId">The pupil.</param>
public sealed record PupilRecordFilters(string? PupilId) : IReportFilters;

/// <summary>
/// Spec 15 section 10, pupil cumulative record: one pupil across every session at the school, term by term (class, average,
/// grade, positions), each session closed by its annual result and promotion outcome. What the school produces when a former
/// pupil asks for a record. An arm-restricted holder sees only the terms spent in their arms.
/// </summary>
internal sealed class PupilRecordReport(IReportReader reader) : ReportBuilder<PupilRecordFilters>
{
    public override string Key => "pupil-record";

    protected override async Task<Result<ReportDto>> BuildAsync(PupilRecordFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.PupilId, "pupilId", out var pupilId, out var error))
        {
            return Result.Failure<ReportDto>(error);
        }

        var pupil = (await reader.FindPupilsAsync([pupilId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(pupilId);
        if (pupil is null)
        {
            return Result.Failure<ReportDto>(NotFound("No pupil was found with that id."));
        }

        var terms = (await reader.ListPupilTermsAsync(pupilId, cancellationToken).ConfigureAwait(false)).Where(term => context.Scope.Allows(term.ArmId)).ToList();
        var annual = (await reader.ListPupilAnnualAsync(pupilId, cancellationToken).ConfigureAwait(false)).Where(entry => context.Scope.Allows(entry.Annual.ArmId)).ToList();
        if (context.Scope.Arms is not null && terms.Count == 0 && annual.Count == 0)
        {
            return Result.Failure<ReportDto>(OutOfScope());
        }

        var sessionIds = terms.Select(term => term.SessionId).Concat(annual.Select(entry => entry.SessionId)).Distinct().ToList();
        var sessions = await reader.FindSessionsAsync(sessionIds, cancellationToken).ConfigureAwait(false);
        var arms = await reader.FindArmsAsync([.. terms.Select(term => term.ArmId).Concat(annual.Select(entry => entry.Annual.ArmId)).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        var decisions = await reader.ListPupilDecisionsAsync(pupilId, cancellationToken).ConfigureAwait(false);

        var rows = new List<ReportRowDto>();
        foreach (var sessionId in sessionIds.OrderBy(id => sessions.GetValueOrDefault(id)?.StartDate ?? DateOnly.MaxValue))
        {
            rows.Add(new(ReportRowKind.Heading, [sessions.GetValueOrDefault(sessionId)?.Name ?? "Unknown session", null, null, null, null, null, null]));
            foreach (var term in terms.Where(term => term.SessionId == sessionId).OrderBy(term => term.TermOrdinal))
            {
                rows.Add(new(ReportRowKind.Data,
                [
                    term.TermName,
                    arms.GetValueOrDefault(term.ArmId)?.Name,
                    ReportText.Decimal(term.Average),
                    term.Grade,
                    ReportText.Position(term.ArmPosition, term.ArmTied),
                    ReportText.Position(term.LevelPosition, term.LevelTied),
                    term.State == ResultSetState.Published ? "Published" : $"Not published ({ReportText.State(term.State)})",
                ]));
            }

            if (annual.FirstOrDefault(entry => entry.SessionId == sessionId) is { Annual: { } year })
            {
                rows.Add(new(ReportRowKind.Subtotal,
                [
                    "Annual",
                    arms.GetValueOrDefault(year.ArmId)?.Name,
                    ReportText.Decimal(year.CumulativeAverage),
                    year.Grade,
                    ReportText.Position(year.Position, year.Tied),
                    null,
                    AnnualText.Status(year, arms.GetValueOrDefault(year.ArmId), decisions.GetValueOrDefault(sessionId)),
                ]));
            }
        }

        var notes = new List<string>();
        if (rows.Count == 0)
        {
            notes.Add("No computed results are on record for this pupil.");
        }

        if (context.Scope.Arms is not null)
        {
            notes.Add("Only the terms spent in the classes you have access to are shown.");
        }

        return Result.Success(Report(
            context,
            "Pupil cumulative record",
            [$"Pupil: {pupil.DisplayName}", $"Reg. no.: {pupil.RegistrationNumber ?? "not issued"}"],
            [
                new("Term", ReportAlign.Left),
                new("Class", ReportAlign.Left),
                new("Average", ReportAlign.Right),
                new("Grade", ReportAlign.Center),
                new("Class pos.", ReportAlign.Right),
                new("Level pos.", ReportAlign.Right),
                new("Status", ReportAlign.Left),
            ],
            rows,
            notes));
    }
}
