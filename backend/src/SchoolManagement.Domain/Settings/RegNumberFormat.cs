using System.Globalization;
using System.Text.RegularExpressions;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Shared constants and composition for spec 6.2.4/6.5.10's registration-number pattern:
/// <c>{abbreviation}{separator}{year}{separator}{serial, zero-padded to width}</c>, for example
/// <c>GRAS/2026/0041</c>.
/// </summary>
public static partial class RegNumberFormat
{
    /// <summary>Spec 6.2.4: <c>serial_width</c> floor.</summary>
    public const int MinSerialWidth = 3;

    /// <summary>Spec 6.2.4: <c>serial_width</c> ceiling.</summary>
    public const int MaxSerialWidth = 6;

    private static readonly char[] ValidSeparators = ['/', '-', '.'];

    /// <summary>Spec 6.2.4: <c>separator</c> is one of <c>/</c>, <c>-</c>, <c>.</c> — exactly one character, no others.</summary>
    public static bool IsValidSeparator(string? separator) =>
        separator is { Length: 1 } && ValidSeparators.Contains(separator[0]);

    /// <summary>
    /// Composes the pattern for <paramref name="serial"/>, zero-padded to <paramref name="serialWidth"/>.
    /// Used by TASK-0005c's preview and, later, TASK-0051's real issuance — one place that knows the
    /// shape of a registration number.
    /// </summary>
    public static string Compose(string abbreviation, string separator, int year, int serialWidth, int serial) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{abbreviation}{separator}{year}{separator}{serial.ToString($"D{serialWidth.ToString(CultureInfo.InvariantCulture)}", CultureInfo.InvariantCulture)}");

    /// <summary>How many digits <paramref name="serial"/> needs, unpadded — the minimum width spec 6.2.10's width-reduction message suggests.</summary>
    public static int DigitCount(int serial) => serial.ToString(CultureInfo.InvariantCulture).Length;

    /// <summary>
    /// Shape validation for a TYPED registration number (TASK-0063, spec 6.5.10's correction path:
    /// "The administrator types the new number in full. The system does not generate it"). Deliberately
    /// NOT the inverse of <see cref="Compose"/> — a correction never runs the composer, since the whole
    /// point is a deliberately chosen serial, so this checks only the general
    /// <c>abbreviation{separator}year{separator}serial</c> shape every issued number has, never a
    /// specific school's saved abbreviation/width/reset settings. The one place that knows what a
    /// registration number looks like, shared between composition and validation without either
    /// depending on the other.
    /// </summary>
    /// <param name="candidate">The string to check, exactly as typed — leading/trailing whitespace is NOT tolerated here; the caller trims first.</param>
    public static bool IsWellFormed(string candidate) =>
        !string.IsNullOrEmpty(candidate) && ShapePattern().IsMatch(candidate);

    // abbreviation (no separator character), one separator, a 4-digit year, the same separator again,
    // one or more digits — the literal shape spec 6.5.10 describes and GRAS/2026/0041 exemplifies.
    [GeneratedRegex(@"^[^/\-.]+(?<sep>[/\-.])\d{4}\k<sep>\d+$")]
    private static partial Regex ShapePattern();
}
