namespace SchoolManagement.Application.Pupils;

/// <summary>
/// Decides which field a search term matched, for <see cref="PupilDto.MatchedField"/> (spec 6.5.15:
/// "Results show which field matched"). A PURE function over plain strings so it is unit-testable
/// without a database, and reusable from both the ordinary list query and the duplicate-candidate
/// query.
/// </summary>
public static class PupilSearchMatcher
{
    /// <summary>
    /// Returns the name of the first field (in <see cref="PupilDto"/> declaration order: surname,
    /// first name, middle name, then registration number) whose value contains
    /// <paramref name="searchTerm"/>, case-insensitively — or <see langword="null"/> when
    /// <paramref name="searchTerm"/> is null/blank or none of them match.
    /// </summary>
    public static string? Resolve(
        string surname,
        string firstName,
        string? middleName,
        string? registrationNumber,
        string? searchTerm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surname);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        if (Contains(surname, searchTerm))
        {
            return nameof(PupilDto.Surname);
        }

        if (Contains(firstName, searchTerm))
        {
            return nameof(PupilDto.FirstName);
        }

        if (middleName is not null && Contains(middleName, searchTerm))
        {
            return nameof(PupilDto.MiddleName);
        }

        // Ordinary substring containment already satisfies spec 6.5.15's own worked example
        // ("typing 41 finds GRAS/2026/0041" — "41" is a substring of "...0041") without a separate
        // serial-extraction step; see NigerianGeography-adjacent remarks in ASSUMPTIONS.md §2.27 for
        // the fuller reasoning and its one accepted looseness (a term matching the year segment too).
        if (registrationNumber is not null && Contains(registrationNumber, searchTerm))
        {
            return nameof(PupilDto.RegistrationNumber);
        }

        return null;
    }

    private static bool Contains(string value, string searchTerm) =>
        value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
}
