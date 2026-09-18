using System.Globalization;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// Spec 6.4.5: arm labels sort "collated naturally... Not alphabetical" within a level — digit runs
/// compare as numbers so a label like "A10" sorts after "A2", the way a person reading the list would
/// expect, rather than before it as plain ordinal text comparison would place it.
/// </summary>
internal sealed class ArmLabelComparer : IComparer<string>
{
    public static readonly ArmLabelComparer Instance = new();

    private ArmLabelComparer()
    {
    }

    /// <inheritdoc />
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var xRuns = SplitIntoRuns(x);
        var yRuns = SplitIntoRuns(y);

        for (var index = 0; index < Math.Min(xRuns.Count, yRuns.Count); index++)
        {
            var (xRun, xIsDigits) = xRuns[index];
            var (yRun, yIsDigits) = yRuns[index];

            int comparison;

            if (xIsDigits && yIsDigits)
            {
                comparison = int.Parse(xRun, CultureInfo.InvariantCulture)
                    .CompareTo(int.Parse(yRun, CultureInfo.InvariantCulture));
            }
            else
            {
                comparison = string.Compare(xRun, yRun, StringComparison.OrdinalIgnoreCase);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return xRuns.Count.CompareTo(yRuns.Count);
    }

    /// <summary>Splits into consecutive digit and non-digit runs, each tagged with whether it is digits.</summary>
    private static List<(string Run, bool IsDigits)> SplitIntoRuns(string value)
    {
        var runs = new List<(string, bool)>();
        var start = 0;

        while (start < value.Length)
        {
            var isDigits = char.IsAsciiDigit(value[start]);
            var end = start + 1;

            while (end < value.Length && char.IsAsciiDigit(value[end]) == isDigits)
            {
                end++;
            }

            runs.Add((value[start..end], isDigits));
            start = end;
        }

        return runs;
    }
}
