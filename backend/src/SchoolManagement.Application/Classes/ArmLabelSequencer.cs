namespace SchoolManagement.Application.Classes;

/// <summary>
/// Spec 6.4.3's "next unused label in sequence" and spec 6.4.8's "bulk creation continues from the
/// highest existing label" are the SAME rule, worked from opposite ends of the alphabet — one shared
/// implementation for <c>GET /arms/next-label</c> and <c>POST /arms/bulk</c> so they can never
/// disagree on what "the next label" means.
/// </summary>
internal static class ArmLabelSequencer
{
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// The next unused single-letter label given <paramref name="existingLabels"/> — A if none of them
    /// is a single letter, otherwise the first unused letter in sequence. Multi-character labels
    /// (spec: a school may label an arm "Gold") do not occupy a letter and are ignored here.
    /// </summary>
    public static string NextUnusedLabel(IEnumerable<string> existingLabels)
    {
        ArgumentNullException.ThrowIfNull(existingLabels);

        var usedLetters = existingLabels
            .Where(label => label.Length == 1 && char.IsAsciiLetterUpper(label[0]))
            .Select(label => label[0])
            .ToHashSet();

        foreach (var letter in Letters)
        {
            if (!usedLetters.Contains(letter))
            {
                return letter.ToString();
            }
        }

        // Every single letter A-Z already taken under one level and session — far beyond spec's
        // worked examples (three rooms). Not a reachable failure worth a dedicated error; a double
        // letter keeps the sequence going rather than throwing.
        return "AA";
    }
}
