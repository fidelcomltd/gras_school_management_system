using System.Globalization;
using System.Text.RegularExpressions;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils.Import;

/// <summary>
/// Cell-level parsing for bulk import (spec 6.5.13): forgiving about formatting, unforgiving about ambiguity. Each parser
/// returns <see langword="null"/> for a blank cell, and an error message instead of a guess when the text cannot be read.
/// </summary>
internal static partial class PupilImportCells
{
    /// <summary>Excel serials below this date from before 1 March 1900, where Excel's leap-year bug makes them ambiguous.</summary>
    private const double MinSerial = 61;

    /// <summary>31 December 9999.</summary>
    private const double MaxSerial = 2958465;

    private const string DateFormatMessage = "Enter the date as DD/MM/YYYY, for example 03/05/2018.";

    /// <summary>DD/MM/YYYY (also with - or .), YYYY-MM-DD (also with /), or an Excel date serial. Two-digit years are refused.</summary>
    public static bool TryDate(ImportCell cell, out DateOnly? date, out string? error)
    {
        date = null;
        error = null;
        if (cell.Date is { } typed)
        {
            date = DateOnly.FromDateTime(typed);
            return true;
        }

        if (cell.Number is { } serial)
        {
            return TrySerial(serial, out date, out error);
        }

        var text = cell.Text;
        if (text.Length == 0)
        {
            return true;
        }

        if (DayFirst().Match(text) is { Success: true } dayFirst)
        {
            return TryCompose(dayFirst.Groups["y"].Value, dayFirst.Groups["m"].Value, dayFirst.Groups["d"].Value, text, out date, out error);
        }

        if (YearFirst().Match(text) is { Success: true } yearFirst)
        {
            return TryCompose(yearFirst.Groups["y"].Value, yearFirst.Groups["m"].Value, yearFirst.Groups["d"].Value, text, out date, out error);
        }

        if (text.All(char.IsAsciiDigit) && double.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var textSerial))
        {
            return TrySerial(textSerial, out date, out error);
        }

        error = DateFormatMessage;
        return false;
    }

    /// <summary>Male or Female; M and F accepted.</summary>
    public static bool TrySex(ImportCell cell, out PupilSex? sex, out string? error)
    {
        sex = Lower(cell) switch
        {
            "" => null,
            "male" or "m" => PupilSex.Male,
            "female" or "f" => PupilSex.Female,
            _ => (PupilSex?)(PupilSex)(-1),
        };
        return Check(ref sex, out error, "Enter Male or Female.");
    }

    /// <summary>Yes or No; Y, N, True and False accepted.</summary>
    public static bool TryYesNo(ImportCell cell, out bool? answer, out string? error)
    {
        switch (Lower(cell))
        {
            case "":
                answer = null;
                error = null;
                return true;
            case "yes" or "y" or "true":
                answer = true;
                error = null;
                return true;
            case "no" or "n" or "false":
                answer = false;
                error = null;
                return true;
            default:
                answer = null;
                error = "Enter Yes or No, or leave it blank if the parent has not answered.";
                return false;
        }
    }

    /// <summary>New or Returning; blank is New.</summary>
    public static bool TryAdmissionType(ImportCell cell, out AdmissionType type, out string? error)
    {
        error = null;
        switch (Lower(cell))
        {
            case "" or "new":
                type = AdmissionType.New;
                return true;
            case "returning":
                type = AdmissionType.Returning;
                return true;
            default:
                type = AdmissionType.New;
                error = "Enter New or Returning.";
                return false;
        }
    }

    /// <summary>Father, Mother or Guardian.</summary>
    public static bool TryPrimaryContact(ImportCell cell, out ContactRole? role, out string? error)
    {
        role = Lower(cell) switch
        {
            "" => null,
            "father" => ContactRole.Father,
            "mother" => ContactRole.Mother,
            "guardian" => ContactRole.Guardian,
            _ => (ContactRole?)(ContactRole)(-1),
        };
        return Check(ref role, out error, "Enter Father, Mother or Guardian: the person the school telephones first.");
    }

    /// <summary>A+ to O-, spaces ignored.</summary>
    public static bool TryBloodGroup(ImportCell cell, out BloodGroup? group, out string? error)
    {
        group = Lower(cell).Replace(" ", string.Empty, StringComparison.Ordinal) switch
        {
            "" => null,
            "a+" => BloodGroup.APositive,
            "a-" => BloodGroup.ANegative,
            "b+" => BloodGroup.BPositive,
            "b-" => BloodGroup.BNegative,
            "ab+" => BloodGroup.AbPositive,
            "ab-" => BloodGroup.AbNegative,
            "o+" => BloodGroup.OPositive,
            "o-" => BloodGroup.ONegative,
            _ => (BloodGroup?)(BloodGroup)(-1),
        };
        return Check(ref group, out error, "Enter one of A+, A-, B+, B-, AB+, AB-, O+, O-.");
    }

    /// <summary>AA, AS, SS, AC or SC.</summary>
    public static bool TryGenotype(ImportCell cell, out Genotype? genotype, out string? error)
    {
        genotype = Lower(cell) switch
        {
            "" => null,
            "aa" => Genotype.AA,
            "as" => Genotype.AS,
            "ss" => Genotype.SS,
            "ac" => Genotype.AC,
            "sc" => Genotype.SC,
            _ => (Genotype?)(Genotype)(-1),
        };
        return Check(ref genotype, out error, "Enter one of AA, AS, SS, AC, SC.");
    }

    /// <summary>
    /// Phone text tidied for <c>NigerianPhoneNumber</c>, which then judges it: spaces, dashes, dots and brackets dropped,
    /// <c>234...</c> given its plus, and the leading 0 restored that a spreadsheet strips when a phone is typed as a number.
    /// </summary>
    public static string? Phone(ImportCell cell)
    {
        var tidy = string.Concat(cell.Text.Where(character => character is not (' ' or '-' or '.' or '(' or ')')));
        if (tidy.Length == 0)
        {
            return null;
        }

        var digitsOnly = tidy.All(char.IsAsciiDigit);
        if (cell.Number is not null && digitsOnly && tidy.Length == 10)
        {
            return "0" + tidy;
        }

        return digitsOnly && tidy.Length == 13 && tidy.StartsWith("234", StringComparison.Ordinal) ? "+" + tidy : tidy;
    }

    /// <summary>The display form of every accepted blood group, for the template.</summary>
    public static readonly IReadOnlyList<string> BloodGroups = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];

    private static string Lower(ImportCell cell) => cell.Text.ToLower(CultureInfo.InvariantCulture);

    // An out-of-range enum value is this file's "unreadable" sentinel: it never escapes, Check turns it into the message.
    private static bool Check<T>(ref T? value, out string? error, string message)
        where T : struct, Enum
    {
        if (value is { } parsed && !Enum.IsDefined(parsed))
        {
            value = null;
            error = message;
            return false;
        }

        error = null;
        return true;
    }

    private static bool TrySerial(double serial, out DateOnly? date, out string? error)
    {
        if (serial is < MinSerial or > MaxSerial)
        {
            date = null;
            error = DateFormatMessage;
            return false;
        }

        date = DateOnly.FromDateTime(DateTime.FromOADate(Math.Floor(serial)));
        error = null;
        return true;
    }

    private static bool TryCompose(string year, string month, string day, string text, out DateOnly? date, out string? error)
    {
        date = null;
        if (year.Length != 4)
        {
            error = $"{text} has a {year.Length}-digit year. Enter the year in full, for example 03/05/2018.";
            return false;
        }

        var y = int.Parse(year, CultureInfo.InvariantCulture);
        var m = int.Parse(month, CultureInfo.InvariantCulture);
        var d = int.Parse(day, CultureInfo.InvariantCulture);
        if (y < 1 || m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, m))
        {
            error = $"{text} is not a real date. {DateFormatMessage}";
            return false;
        }

        date = new DateOnly(y, m, d);
        error = null;
        return true;
    }

    [GeneratedRegex(@"^(?<d>\d{1,2})[/.\-](?<m>\d{1,2})[/.\-](?<y>\d{1,4})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DayFirst();

    [GeneratedRegex(@"^(?<y>\d{4})[/\-](?<m>\d{1,2})[/\-](?<d>\d{1,2})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex YearFirst();
}
