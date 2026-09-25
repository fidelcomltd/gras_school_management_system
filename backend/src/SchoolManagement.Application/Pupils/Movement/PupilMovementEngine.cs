using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Pupils.Movement;

/// <summary>A result set a move touches, tracked and row-locked, with its read-side description.</summary>
internal sealed record AffectedResultSet(ResultSet ResultSet, PupilMovementResultSetDto View);

/// <summary>
/// The part a status change and a transfer share: what a pupil moving into or out of an arm does to that arm's result
/// sets (spec 06 §6.4.4 steps 5 and 6, 09 §6.7.11), and the date rules.
/// </summary>
/// <remarks>
/// The roster behind every score sheet and computation is the arm's open enrolments of active pupils, read live
/// (<c>IEnrolmentRepository.ListActiveRosterByArmAsync</c>), so marks never move: they are keyed to pupil, subject and
/// term, and the pupil simply stops (or starts) appearing. Every non-Published set of an affected arm is therefore
/// stale and flagged. A Published set renders from its snapshot and is never flagged.
/// </remarks>
internal sealed class PupilMovementEngine(
    IClassLevelRepository classLevels,
    ITermRepository terms,
    IResultSetRepository resultSets,
    ISystemAuditSink auditSink)
{
    /// <summary>The composed display name of each arm, keyed by arm id.</summary>
    public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IEnumerable<Arm> arms, CancellationToken cancellationToken)
    {
        var levels = (await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(level => level.Id, level => level.Name);

        return arms
            .DistinctBy(arm => arm.Id)
            .ToDictionary(
                arm => arm.Id,
                arm => levels.TryGetValue(arm.ClassLevelId, out var levelName) ? ArmDisplayName.Compose(levelName, arm.Label) : arm.Label);
    }

    /// <summary>
    /// Row-locks every result set of <paramref name="arms"/> and says what the move does to each. With
    /// <paramref name="publishedBlocks"/>, a Published set whose term ends on or after <paramref name="effectiveDate"/>
    /// blocks (a transfer, spec 6.4.4 step 6, both directions by human ruling 2026-09-25); otherwise Published sets are
    /// left out, since a status change leaves published results published (spec 6.5.14).
    /// </summary>
    public async Task<IReadOnlyList<AffectedResultSet>> AssessAsync(
        IReadOnlyCollection<Arm> arms,
        IReadOnlyDictionary<Guid, string> armNames,
        DateOnly effectiveDate,
        bool publishedBlocks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arms);
        ArgumentNullException.ThrowIfNull(armNames);

        var locked = await resultSets.LockByArmsAsync(arms.Select(arm => arm.Id).ToArray(), cancellationToken).ConfigureAwait(false);

        if (locked.Count == 0)
        {
            return [];
        }

        var termsById = new Dictionary<Guid, Domain.Sessions.Term>();

        foreach (var sessionId in arms.Select(arm => arm.SessionId).Distinct())
        {
            foreach (var term in await terms.ListBySessionReadOnlyAsync(sessionId, cancellationToken).ConfigureAwait(false))
            {
                termsById[term.Id] = term;
            }
        }

        var affected = new List<AffectedResultSet>();

        foreach (var resultSet in locked)
        {
            if (!termsById.TryGetValue(resultSet.TermId, out var term))
            {
                continue;
            }

            PupilMovementEffect effect;

            if (resultSet.State == ResultSetState.Published)
            {
                if (!publishedBlocks || term.EndDate < effectiveDate)
                {
                    continue;
                }

                effect = PupilMovementEffect.Blocks;
            }
            else
            {
                effect = resultSet.State is ResultSetState.AwaitingApproval or ResultSetState.ReturnedForCorrection
                    ? PupilMovementEffect.RevertsToDraft
                    : PupilMovementEffect.NeedsRecompute;
            }

            affected.Add(new AffectedResultSet(
                resultSet,
                new PupilMovementResultSetDto(
                    Id(resultSet.Id),
                    Id(resultSet.ArmId),
                    armNames.TryGetValue(resultSet.ArmId, out var armName) ? armName : string.Empty,
                    Id(term.Id),
                    term.Name,
                    resultSet.State,
                    effect)));
        }

        return affected
            .OrderBy(entry => termsById[entry.ResultSet.TermId].Ordinal)
            .ThenBy(entry => entry.View.ArmName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The refusal for the first blocking set, in the spec's words (6.4.4 step 6), or success when none blocks.</summary>
    public static Result RefuseIfBlocked(IEnumerable<AffectedResultSet> affected, Guid sourceArmId)
    {
        ArgumentNullException.ThrowIfNull(affected);

        var blocker = affected.FirstOrDefault(entry => entry.View.Effect == PupilMovementEffect.Blocks);

        if (blocker is null)
        {
            return Result.Success();
        }

        var direction = blocker.ResultSet.ArmId == sourceArmId ? "out of" : "into";

        return Result.Failure(Error.Conflict(
            "pupil.transfer_blocked_by_published_results",
            $"{blocker.View.ArmName} results for {blocker.View.TermName} are published. Withdraw them before moving pupils {direction} the arm."));
    }

    /// <summary>Flags every non-blocking set, dropping Awaiting Approval and Returned for Correction to Draft with <paramref name="note"/>.</summary>
    public Task ApplyAsync(IEnumerable<AffectedResultSet> affected, string note, CancellationToken cancellationToken) =>
        ResultSetRecomputeFlagger.FlagAsync(
            affected.Where(entry => entry.View.Effect != PupilMovementEffect.Blocks).Select(entry => entry.ResultSet),
            auditSink,
            cancellationToken,
            cohortNote: note);

    /// <summary>A date is refused when it is after today: rosters read the enrolment open now (human ruling 2026-09-25).</summary>
    public static Result RefuseIfFuture(DateOnly effectiveDate, DateOnly today) =>
        effectiveDate > today
            ? Result.Failure(Error.Validation(
                "pupil.effective_date_in_future",
                $"The effective date cannot be later than today ({today:dd/MM/yyyy}). Record the change on the day it happens."))
            : Result.Success();

    /// <summary>The note a result set carries after dropping to Draft.</summary>
    public static string CohortNote(string cause, DateOnly effectiveDate) =>
        string.Create(CultureInfo.InvariantCulture, $"Cohort changed by {cause} on {effectiveDate:dd/MM/yyyy}.");

    /// <summary>The response both movement routes return.</summary>
    public static Result<PupilMovementOutcomeDto> Outcome(
        bool dryRun,
        Pupil pupil,
        PupilStatus fromStatus,
        PupilStatus toStatus,
        Arm? fromArm,
        Arm? toArm,
        IReadOnlyDictionary<Guid, string> armNames,
        DateOnly effectiveDate,
        DateOnly? enrolmentClosesOn,
        IReadOnlyList<AffectedResultSet> affected,
        Classes.ArmCapacityCheck? capacity,
        DateOnly today) =>
        Result.Success(new PupilMovementOutcomeDto(
            dryRun,
            PupilMapper.ToDto(pupil, today),
            fromStatus,
            toStatus,
            fromArm?.Id.ToString("D", CultureInfo.InvariantCulture),
            fromArm is null ? null : armNames[fromArm.Id],
            toArm?.Id.ToString("D", CultureInfo.InvariantCulture),
            toArm is null ? null : armNames[toArm.Id],
            effectiveDate,
            enrolmentClosesOn,
            affected.Select(entry => entry.View).ToList(),
            capacity is null ? null : new PupilMovementCapacityDto(capacity.Capacity, capacity.EnrolledAfter, capacity.OverCapacity, capacity.CanOverride)));

    private static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);
}
