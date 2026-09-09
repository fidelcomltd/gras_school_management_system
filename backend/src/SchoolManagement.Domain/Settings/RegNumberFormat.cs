using System.Globalization;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Shared constants and composition for spec 6.2.4/6.5.10's registration-number pattern:
/// <c>{abbreviation}{separator}{year}{separator}{serial, zero-padded to width}</c>, for example
/// <c>GRAS/2026/0041</c>.
/// </summary>
public static class RegNumberFormat
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
}
