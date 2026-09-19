using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Derives a remark sheet's opaque <c>version</c> from the CONTENT of the remarks it covers — same
/// convention as <c>TraitRatingVersion</c> (TASK-0083). Shared by the class-teacher and head-teacher
/// remark sheets: each is versioned independently because each is computed over its own
/// <see cref="RemarkKind"/> slice only (spec: "A head-teacher save never stale-fails a concurrent
/// class-teacher or attendance save").
/// </summary>
internal static class RemarkVersion
{
    /// <summary>Returns the sheet's version, or <see langword="null"/> when <paramref name="rows"/> is empty — the contract's "null before any remark exists".</summary>
    public static string? Compute(IReadOnlyCollection<PupilRemarkSnapshot> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();

        foreach (var row in rows.OrderBy(row => row.PupilId))
        {
            builder
                .Append(row.PupilId.ToString("D", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.Text).Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
