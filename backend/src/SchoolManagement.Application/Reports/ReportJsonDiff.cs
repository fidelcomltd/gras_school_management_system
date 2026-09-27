using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SchoolManagement.Application.Reports;

/// <summary>
/// Plain-language differences between two configuration snapshots (spec 15 section 10, settings change history: "Grading band C
/// lower bound changed from 60 to 58"). Walks both JSON documents; list items that carry a natural key (a grade letter, a name,
/// a label, a code) are matched by it, so a reordered or inserted band does not read as every band changing.
/// </summary>
public static class ReportJsonDiff
{
    private static readonly string[] IdentityKeys = ["gradeLetter", "letter", "name", "label", "pointCode", "code", "key", "id"];

    /// <summary>The changes from <paramref name="beforeJson"/> to <paramref name="afterJson"/>, at most <paramref name="max"/> plus a count of the rest.</summary>
    public static IReadOnlyList<string> Describe(string? beforeJson, string afterJson, int max = 8)
    {
        ArgumentNullException.ThrowIfNull(afterJson);
        if (beforeJson is null)
        {
            return ["First saved configuration."];
        }

        using var before = JsonDocument.Parse(beforeJson);
        using var after = JsonDocument.Parse(afterJson);
        var changes = new List<string>();
        Compare(before.RootElement, after.RootElement, [], changes);
        if (changes.Count <= max)
        {
            return changes.Count == 0 ? ["No setting changed (saved as it was)."] : changes;
        }

        return [.. changes.Take(max), $"and {(changes.Count - max).ToString(CultureInfo.InvariantCulture)} more changes"];
    }

    private static void Compare(JsonElement before, JsonElement after, List<string> path, List<string> changes)
    {
        if (before.ValueKind == JsonValueKind.Object && after.ValueKind == JsonValueKind.Object)
        {
            var names = before.EnumerateObject().Select(property => property.Name)
                .Concat(after.EnumerateObject().Select(property => property.Name))
                .Distinct(StringComparer.Ordinal);
            // The per-group version counters move on every save; they are not a change anyone made.
            foreach (var name in names.Where(name => !name.EndsWith("VersionNumber", StringComparison.OrdinalIgnoreCase)))
            {
                var had = before.TryGetProperty(name, out var old);
                var has = after.TryGetProperty(name, out var now);
                path.Add(Words(name));
                if (had && has)
                {
                    Compare(old, now, path, changes);
                }
                else
                {
                    changes.Add($"{Path(path)} {(has ? "added" : "removed")}");
                }

                path.RemoveAt(path.Count - 1);
            }

            return;
        }

        if (before.ValueKind == JsonValueKind.Array && after.ValueKind == JsonValueKind.Array)
        {
            var key = IdentityKey(before, after);
            if (key is null)
            {
                var length = Math.Max(before.GetArrayLength(), after.GetArrayLength());
                for (var index = 0; index < length; index++)
                {
                    path.Add((index + 1).ToString(CultureInfo.InvariantCulture));
                    if (index < before.GetArrayLength() && index < after.GetArrayLength())
                    {
                        Compare(before[index], after[index], path, changes);
                    }
                    else
                    {
                        changes.Add($"{Path(path)} {(index < after.GetArrayLength() ? "added" : "removed")}");
                    }

                    path.RemoveAt(path.Count - 1);
                }

                return;
            }

            var olds = before.EnumerateArray().ToDictionary(item => Text(item.GetProperty(key)), StringComparer.Ordinal);
            var nows = after.EnumerateArray().ToDictionary(item => Text(item.GetProperty(key)), StringComparer.Ordinal);
            foreach (var id in olds.Keys.Concat(nows.Keys).Distinct(StringComparer.Ordinal))
            {
                path.Add(id);
                if (olds.TryGetValue(id, out var old) && nows.TryGetValue(id, out var now))
                {
                    Compare(old, now, path, changes);
                }
                else
                {
                    changes.Add($"{Path(path)} {(nows.ContainsKey(id) ? "added" : "removed")}");
                }

                path.RemoveAt(path.Count - 1);
            }

            return;
        }

        var from = Text(before);
        var to = Text(after);
        if (!string.Equals(from, to, StringComparison.Ordinal))
        {
            changes.Add($"{Path(path)} changed from {Short(from)} to {Short(to)}");
        }
    }

    // A key present, scalar and unique on every item of BOTH lists: the items' identity. Two traits may share a name in
    // different domains, so a key that repeats is no identity, and the lists are compared by position instead.
    private static string? IdentityKey(JsonElement before, JsonElement after) =>
        IdentityKeys.FirstOrDefault(key => Identifies(before, key) && Identifies(after, key) && (before.GetArrayLength() + after.GetArrayLength()) > 0);

    private static bool Identifies(JsonElement array, string key)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(key, out var value)
                || value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) || !values.Add(Text(value)))
            {
                return false;
            }
        }

        return true;
    }

    private static string Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => "blank",
        JsonValueKind.True => "yes",
        JsonValueKind.False => "no",
        _ => value.GetRawText(),
    };

    private static string Short(string value) => value.Length <= 40 ? value : value[..37] + "…";

    private static string Path(List<string> path) => path.Count == 0 ? "The configuration" : string.Join(" › ", path);

    // camelCase to words: "lowerBound" -> "lower bound".
    private static string Words(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        foreach (var character in name)
        {
            if (char.IsUpper(character) && builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
