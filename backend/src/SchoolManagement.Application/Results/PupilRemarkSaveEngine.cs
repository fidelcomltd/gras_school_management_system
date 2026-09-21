using System.Globalization;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// The row-application logic shared by <c>SaveClassTeacherRemarksHandler</c> and
/// <c>SaveHeadTeacherRemarksHandler</c> (TASK-0086 stage A) — unlike
/// <c>SaveTraitRatingsHandler</c>/<c>SaveDevelopmentRatingsHandler</c>, which duplicate their
/// per-cell logic because the two ratings shapes genuinely differ (a nested per-trait dictionary
/// versus a per-indicator cell with a comment), the two remark sheets are IDENTICAL in shape and
/// differ only in <see cref="RemarkKind"/>, privilege and lock states — both handler-owned, not
/// this engine's job. Sharing this piece removes the chance of the upsert/clear/audit-snapshot
/// logic drifting between the two copies.
/// </summary>
internal static class PupilRemarkSaveEngine
{
    /// <summary>Roster membership and length checks — every data-dependent rule this sheet has.</summary>
    public static Dictionary<string, string[]> ValidateRows(IReadOnlyList<SaveRemarkRowInput> rows, HashSet<Guid> rosterPupilIds)
    {
        var failures = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void AddFailure(string path, string message)
        {
            if (!failures.TryGetValue(path, out var list))
            {
                list = [];
                failures[path] = list;
            }

            if (!list.Contains(message, StringComparer.Ordinal))
            {
                list.Add(message);
            }
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            if (!Guid.TryParse(row.PupilId, out var pupilId) || !rosterPupilIds.Contains(pupilId))
            {
                AddFailure($"Rows[{index}].PupilId", "This pupil is not on the arm's active roster.");
                continue;
            }

            var trimmed = row.Remark?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > PupilRemark.TextMaxLength)
            {
                AddFailure($"Rows[{index}].Remark", $"Remark must be {PupilRemark.TextMaxLength} characters or fewer.");
            }
        }

        return failures.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Applies every row: an omitted pupil is untouched, a blank remark clears (deletes) the row, a
    /// non-blank remark sets or replaces it. Creates the result set on the first remark of either
    /// kind for this arm/term, exactly as <c>SaveTraitRatingsHandler</c> does. Populates
    /// <paramref name="beforeChanges"/>/<paramref name="afterChanges"/> ONLY for rows that actually
    /// changed something (appendix C.6: an unchanged resave must not disturb the writtenBy/writtenAt
    /// snapshot, and must not appear in the audit trail as a change either).
    /// </summary>
    public static async Task<Result<ResultSet?>> ApplyAsync(
        IReadOnlyList<SaveRemarkRowInput> rows,
        Dictionary<Guid, PupilRemark> byPupil,
        ResultSet? resultSetRef,
        Guid armId,
        Guid termId,
        RemarkKind kind,
        Guid writtenByAdminId,
        string writtenByName,
        DateTimeOffset writtenAtUtc,
        IResultSetRepository resultSets,
        IPupilRemarkRepository remarks,
        List<object?> beforeChanges,
        List<object?> afterChanges,
        CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            var pupilId = Guid.Parse(row.PupilId);
            var hasExisting = byPupil.TryGetValue(pupilId, out var existing);
            var trimmed = row.Remark?.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                if (hasExisting)
                {
                    beforeChanges.Add(Snapshot(pupilId, existing!.Text));
                    afterChanges.Add(Snapshot(pupilId, remark: null));
                    await remarks.RemoveAsync(existing!, cancellationToken).ConfigureAwait(false);
                    byPupil.Remove(pupilId);
                }

                continue;
            }

            if (hasExisting)
            {
                var before = existing!.Text;
                var changed = existing.UpdateText(trimmed, writtenByAdminId, writtenByName, writtenAtUtc);
                if (changed)
                {
                    beforeChanges.Add(Snapshot(pupilId, before));
                    afterChanges.Add(Snapshot(pupilId, trimmed));
                }

                continue;
            }

            beforeChanges.Add(Snapshot(pupilId, remark: null));

            if (resultSetRef is null)
            {
                var creation = ResultSet.Create(Guid.CreateVersion7(), armId, termId);
                if (creation.IsFailure)
                {
                    return Result.Failure<ResultSet?>(creation.Error);
                }

                resultSetRef = creation.Value;
                await resultSets.AddAsync(resultSetRef, cancellationToken).ConfigureAwait(false);
            }

            var remarkCreation = PupilRemark.Create(
                Guid.CreateVersion7(), resultSetRef.Id, pupilId, kind, trimmed, writtenByAdminId, writtenByName, writtenAtUtc);
            if (remarkCreation.IsFailure)
            {
                return Result.Failure<ResultSet?>(remarkCreation.Error);
            }

            byPupil[pupilId] = remarkCreation.Value;
            await remarks.AddAsync(remarkCreation.Value, cancellationToken).ConfigureAwait(false);
            afterChanges.Add(Snapshot(pupilId, trimmed));
        }

        return Result.Success(resultSetRef);
    }

    /// <summary>
    /// Ruling H's fill-all action (head teacher only): sets <paramref name="fillText"/> for every
    /// roster pupil who STILL has no remark after <see cref="ApplyAsync"/> has run (rows apply
    /// first, then the fill — delta item 3). Never overwrites an existing remark.
    /// </summary>
    public static async Task<Result<ResultSet?>> FillEmptyAsync(
        string fillText,
        IReadOnlyCollection<Guid> rosterPupilIds,
        Dictionary<Guid, PupilRemark> byPupil,
        ResultSet? resultSetRef,
        Guid armId,
        Guid termId,
        RemarkKind kind,
        Guid writtenByAdminId,
        string writtenByName,
        DateTimeOffset writtenAtUtc,
        IResultSetRepository resultSets,
        IPupilRemarkRepository remarks,
        List<object?> beforeChanges,
        List<object?> afterChanges,
        CancellationToken cancellationToken)
    {
        foreach (var pupilId in rosterPupilIds)
        {
            if (byPupil.ContainsKey(pupilId))
            {
                continue;
            }

            beforeChanges.Add(Snapshot(pupilId, remark: null));

            if (resultSetRef is null)
            {
                var creation = ResultSet.Create(Guid.CreateVersion7(), armId, termId);
                if (creation.IsFailure)
                {
                    return Result.Failure<ResultSet?>(creation.Error);
                }

                resultSetRef = creation.Value;
                await resultSets.AddAsync(resultSetRef, cancellationToken).ConfigureAwait(false);
            }

            var remarkCreation = PupilRemark.Create(
                Guid.CreateVersion7(), resultSetRef.Id, pupilId, kind, fillText, writtenByAdminId, writtenByName, writtenAtUtc);
            if (remarkCreation.IsFailure)
            {
                return Result.Failure<ResultSet?>(remarkCreation.Error);
            }

            byPupil[pupilId] = remarkCreation.Value;
            await remarks.AddAsync(remarkCreation.Value, cancellationToken).ConfigureAwait(false);
            afterChanges.Add(Snapshot(pupilId, fillText));
        }

        return Result.Success(resultSetRef);
    }

    private static Dictionary<string, object?> Snapshot(Guid pupilId, string? remark) => new(StringComparer.Ordinal)
    {
        ["pupilId"] = pupilId.ToString("D", CultureInfo.InvariantCulture),
        ["remark"] = remark,
    };
}
