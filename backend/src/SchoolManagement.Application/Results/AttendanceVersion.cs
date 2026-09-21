using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SchoolManagement.Application.Abstractions.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Derives an attendance sheet's opaque <c>version</c> from the CONTENT of the entries it covers —
/// same convention as <c>TraitRatingVersion</c> (TASK-0083), computed rather than stored because
/// there is no single database row for "the sheet".
/// </summary>
internal static class AttendanceVersion
{
    /// <summary>Returns the sheet's version, or <see langword="null"/> when <paramref name="rows"/> is empty — the contract's "null before any attendance exists".</summary>
    public static string? Compute(IReadOnlyCollection<AttendanceEntrySnapshot> rows)
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
                .Append(row.TimesPresent.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
