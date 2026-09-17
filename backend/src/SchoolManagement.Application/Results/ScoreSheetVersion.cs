using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SchoolManagement.Application.Results;

/// <summary>One row's content, as it will be (or is) persisted, for <see cref="ScoreSheetVersion.Compute"/>.</summary>
public readonly record struct ScoreSheetVersionRow(Guid PupilId, string ComponentMarksJson, int? ExamMark, bool ExamAbsent);

/// <summary>
/// Derives a score sheet's opaque <c>version</c> from the CONTENT of the rows it covers (TASK-0076
/// dispatch B's contract delta: "Sheet version is derived from the rows it covers"). There is no
/// single database row for "the sheet" — a subject's sheet is the set of <c>subject_score</c> rows for
/// one result set and subject — so the version is computed, not stored.
/// </summary>
/// <remarks>
/// <para>
/// CONTENT-based (pupil id, marks, exam mark, absence flag), not the per-row optimistic-concurrency
/// token every <c>IAuditableEntity</c> already carries via <c>ApplicationDbContext.ApplyConcurrencyTokens</c>.
/// That token is stamped by <c>AuditingInterceptor</c> INSIDE <c>UnitOfWork.ExecuteAtomicallyAsync</c>,
/// strictly after a command handler returns its <c>Result</c> — so a handler can never read the
/// post-save token in time to put it in the very response it is building. Content has no such
/// timing problem: the handler already knows, before it returns, exactly what it is about to persist,
/// and that is bit-for-bit what a later read will see. The per-row concurrency token still protects
/// the write itself (EF's own optimistic-concurrency check on each tracked row); this is a separate,
/// sheet-level "did anything I can see change" signal built on top of it.
/// </para>
/// <para>
/// Deterministic and order-independent (rows are sorted by pupil id before hashing) so reading the
/// same rows twice always yields the same string, and any add, edit or removal changes it. Used
/// identically by the GET handler (reporting the sheet's current version) and the PUT handler (both
/// to detect a stale save before touching anything, and to report the refreshed version afterward).
/// </para>
/// <para>
/// <c>ComponentMarksJson</c> is stored in a <c>jsonb</c> column, and <c>jsonb</c> does NOT preserve
/// the exact text it was given — Postgres re-serialises objects with its own key order and spacing
/// on every read. A save's in-memory <see cref="System.Text.Json.JsonSerializer"/> output is therefore
/// not byte-identical to what the very next read gets back for the same content, so hashing the raw
/// string would make a save's OWN returned version fail that save's own staleness check on the next
/// call. Each component-marks payload is parsed and its keys sorted before hashing so the version
/// depends only on CONTENT, never on which serialiser (or which side of a jsonb round-trip) produced
/// the text.
/// </para>
/// </remarks>
internal static class ScoreSheetVersion
{
    /// <summary>
    /// Returns the sheet's version, or <see langword="null"/> when <paramref name="rows"/> is empty —
    /// the contract's "null before any row exists".
    /// </summary>
    public static string? Compute(IReadOnlyCollection<ScoreSheetVersionRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();

        foreach (var row in rows.OrderBy(row => row.PupilId))
        {
            builder.Append(row.PupilId).Append('|');

            var marks = JsonSerializer.Deserialize<Dictionary<string, int?>>(row.ComponentMarksJson)
                ?? new Dictionary<string, int?>(StringComparer.Ordinal);

            foreach (var key in marks.Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                builder
                    .Append(key)
                    .Append('=')
                    .Append(marks[key]?.ToString(CultureInfo.InvariantCulture) ?? "-")
                    .Append(';');
            }

            builder
                .Append('|')
                .Append(row.ExamMark?.ToString(CultureInfo.InvariantCulture) ?? "-")
                .Append('|')
                .Append(row.ExamAbsent)
                .Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
