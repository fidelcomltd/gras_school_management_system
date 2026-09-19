using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SchoolManagement.Application.Abstractions.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Derives a development-rating grid's opaque <c>version</c> from the CONTENT of the ratings it
/// covers — same convention as <c>TraitRatingVersion</c>, computed rather than stored because there is
/// no single database row for "the grid".
/// </summary>
/// <remarks>
/// Unlike <c>TraitRatingVersion</c>, each row here also carries a <c>comment</c>, so a comment-only
/// edit (same point, different text) must still change the hash. The comment is length-prefixed
/// before being appended, so no separator collision inside free text can make two different comments
/// hash identically.
/// </remarks>
internal static class DevelopmentRatingVersion
{
    /// <summary>Returns the grid's version, or <see langword="null"/> when <paramref name="rows"/> is empty — the contract's "null before any rating exists".</summary>
    public static string? Compute(IReadOnlyCollection<DevelopmentRatingSnapshot> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();

        foreach (var row in rows
            .OrderBy(row => row.PupilId)
            .ThenBy(row => row.IndicatorId))
        {
            var comment = row.Comment ?? string.Empty;

            builder
                .Append(row.PupilId.ToString("D", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.IndicatorId.ToString("D", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.RatingScalePointId.ToString("D", CultureInfo.InvariantCulture)).Append('|')
                .Append(comment.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(comment)
                .Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
