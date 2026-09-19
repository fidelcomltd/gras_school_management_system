using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SchoolManagement.Application.Abstractions.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Derives a trait-rating grid's opaque <c>version</c> from the CONTENT of the ratings it covers —
/// same convention as <c>ScoreSheetVersion</c> (TASK-0076), computed rather than stored because there
/// is no single database row for "the grid".
/// </summary>
/// <remarks>
/// Simpler than <c>ScoreSheetVersion</c>: each row here is three scalar FK ids with no <c>jsonb</c>
/// round-trip to normalise, so the raw snapshot values hash directly — no re-parse-and-resort step is
/// needed the way <c>ComponentMarksJson</c> requires one.
/// </remarks>
internal static class TraitRatingVersion
{
    /// <summary>Returns the grid's version, or <see langword="null"/> when <paramref name="rows"/> is empty — the contract's "null before any rating exists".</summary>
    public static string? Compute(IReadOnlyCollection<TraitRatingSnapshot> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();

        foreach (var row in rows
            .OrderBy(row => row.PupilId)
            .ThenBy(row => row.TraitId))
        {
            builder
                .Append(row.PupilId.ToString("D", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.TraitId.ToString("D", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.RatingScalePointId.ToString("D", CultureInfo.InvariantCulture)).Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
