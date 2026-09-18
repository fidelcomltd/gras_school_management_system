using SchoolManagement.Application.Abstractions.Results;

namespace SchoolManagement.Application.Subjects;

/// <summary>Spec-verbatim message builders shared by the grid save, copy and prefill handlers.</summary>
internal static class SubjectMappingMessages
{
    /// <summary>
    /// Spec 6.6.6, verbatim shape: "First Term 2026/2027 is closed. Its subject mappings cannot be
    /// changed."
    /// </summary>
    public static string TermClosed(string termName, string sessionName) =>
        $"{termName} {sessionName} is closed. Its subject mappings cannot be changed.";

    /// <summary>
    /// Spec 6.6.6, verbatim shape: "Marks have been entered for Handwriting in Primary 4A (28 pupils)
    /// and Primary 4B (26 pupils) this term. Ending this mapping now would hide marks that have
    /// already been recorded. End it for Second Term instead, or void the marks first."
    /// </summary>
    public static string MarksRecorded(string subjectName, IReadOnlyList<SubjectMarkArmSummary> arms)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectName);
        ArgumentNullException.ThrowIfNull(arms);

        var armPhrases = arms.Select(arm => $"{arm.ArmDisplayName} ({arm.PupilCount} pupils)").ToArray();

        return $"Marks have been entered for {subjectName} in {JoinWithAnd(armPhrases)} this term. " +
               "Ending this mapping now would hide marks that have already been recorded. End it for " +
               "Second Term instead, or void the marks first.";
    }

    private static string JoinWithAnd(string[] items)
    {
        return items.Length switch
        {
            0 => string.Empty,
            1 => items[0],
            _ => string.Join(", ", items.Take(items.Length - 1)) + " and " + items[^1],
        };
    }
}
