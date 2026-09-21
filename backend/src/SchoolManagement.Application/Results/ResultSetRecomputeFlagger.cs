using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// The shared "flag every already-locked result set" step behind TASK-0088 AC A1/A2/A3 — a §6.2.9
/// settings save and a subject-mapping change both end by handing their caller's already row-locked
/// (AC A4) result sets to <see cref="FlagAsync"/>, so the drop-and-audit rule (AC A3) lives in
/// exactly ONE place rather than twelve. Callers are responsible for locking the sets themselves
/// (<see cref="Abstractions.Results.IResultSetRepository.LockNonPublishedInSessionAsync"/> or
/// <see cref="Abstractions.Results.IResultSetRepository.LockNonPublishedByTermAndClassLevelsAsync"/>)
/// — this type never queries.
/// </summary>
internal static class ResultSetRecomputeFlagger
{
    private const string EntityType = "result_set";

    /// <summary>
    /// Flags every set in <paramref name="lockedResultSets"/> (spec 6.7.11's "Any state -&gt; Same
    /// state with needs_recompute true | System" row), and for the ONE state that also moves —
    /// Returned for Correction dropping to Draft, the state machine's separate System row — records a
    /// dedicated audit event with <c>before_json</c>/<c>after_json</c> (standing obligation, spec
    /// 14 §9.3). A set that only gains the flag is not separately audited: it is not a state change,
    /// and the settings/mapping save that triggered it already carries its own audit event.
    /// </summary>
    public static async Task FlagAsync(
        IEnumerable<ResultSet> lockedResultSets,
        ISystemAuditSink auditSink,
        CancellationToken cancellationToken)
    {
        foreach (var resultSet in lockedResultSets)
        {
            var droppedToDraft = resultSet.FlagNeedsRecomputeBySystem();

            if (!droppedToDraft)
            {
                continue;
            }

            await auditSink.RecordAsync(
                "result_set.returned_for_correction_reverted_to_draft",
                EntityType,
                resultSet.Id.ToString("D", CultureInfo.InvariantCulture),
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["state"] = nameof(ResultSetState.Draft),
                    ["needsRecompute"] = true,
                },
                actorAdminId: null,
                cancellationToken,
                beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["state"] = nameof(ResultSetState.ReturnedForCorrection),
                    ["needsRecompute"] = false,
                }).ConfigureAwait(false);
        }
    }
}
