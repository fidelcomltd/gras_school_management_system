using System.Text.RegularExpressions;

namespace SchoolManagement.Domain.Common;

/// <summary>
/// Validates and normalises a Nigerian phone number (spec 6.1.3, reused verbatim by spec 6.2.3 for
/// the school identity's own phone field): "Accepts 08012345678 or +2348012345678 and normalises to
/// +234 form on save. Rejects fewer than 11 digits in the national form."
/// </summary>
/// <remarks>
/// FIRST IMPLEMENTATION OF THIS RULE IN THE CODEBASE. Admin-account CRUD (TASK-0027, held for §5
/// sign-off) states the identical rule but has not shipped it yet, so there is no existing type to
/// copy — this one is written to be reused there rather than re-derived when that card lands.
/// <para>
/// Accepted input, after trimming: exactly <c>0</c> followed by 10 digits (11 total, the "national
/// form"), or exactly <c>+234</c> followed by 10 digits (the "international form"). Anything else —
/// including a national-form string with fewer than 11 digits, spaces, or a different country code —
/// is rejected. This is an authored interpretation of the spec's two named examples, not a literal
/// quote; recorded in <c>backend/docs/ASSUMPTIONS.md</c>.
/// </para>
/// </remarks>
public static class NigerianPhoneNumber
{
    private static readonly Regex NationalForm = new(
        "^0[0-9]{10}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InternationalForm = new(
        "^\\+234[0-9]{10}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Attempts to validate and normalise <paramref name="raw"/> to the <c>+234</c> form.
    /// </summary>
    /// <param name="raw">The candidate phone number, in either accepted form.</param>
    /// <param name="normalized">
    /// The <c>+234</c>-form result on success; <see cref="string.Empty"/> on failure.
    /// </param>
    /// <returns><see langword="true"/> when <paramref name="raw"/> matches one of the two accepted forms.</returns>
    public static bool TryNormalize(string raw, out string normalized)
    {
        var trimmed = raw?.Trim() ?? string.Empty;

        if (InternationalForm.IsMatch(trimmed))
        {
            normalized = trimmed;
            return true;
        }

        if (NationalForm.IsMatch(trimmed))
        {
            normalized = "+234" + trimmed[1..];
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}
