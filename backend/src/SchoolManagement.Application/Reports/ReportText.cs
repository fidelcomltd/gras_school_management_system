using System.Globalization;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Reports;

/// <summary>The few formats every report shares, so a position or an average reads the same everywhere.</summary>
internal static class ReportText
{
    /// <summary>"3", "3=" when tied, blank when unranked.</summary>
    public static string? Position(int? position, bool tied) =>
        position is { } value ? value.ToString(CultureInfo.InvariantCulture) + (tied ? "=" : string.Empty) : null;

    /// <summary>Two decimal places, as the result sheet prints them.</summary>
    public static string Decimal(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A whole number.</summary>
    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A whole percentage, e.g. "45%".</summary>
    public static string Percent(int part, int whole) =>
        whole == 0 ? "–" : Math.Round(100m * part / whole, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>A result set's state in words.</summary>
    public static string State(ResultSetState? state) => state switch
    {
        null => "Not started",
        ResultSetState.AwaitingApproval => "Awaiting approval",
        ResultSetState.ReturnedForCorrection => "Returned for correction",
        { } other => other.ToString(),
    };
}
