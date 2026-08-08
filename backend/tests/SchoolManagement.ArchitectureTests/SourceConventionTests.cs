using System.Text.RegularExpressions;

namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// Conventions checked against source text, for rules the type graph cannot express.
/// </summary>
public sealed partial class SourceConventionTests
{
    [Fact]
    public void NoAmbientClockAccess()
    {
        // Ambient time is what makes a test suite that passes all day fail at midnight, and a
        // month-boundary bug impossible to reproduce. Every clock read goes through the injected
        // TimeProvider, which tests replace with FakeTimeProvider.
        //
        // The sole exception is Program.cs registering TimeProvider.System — the one place that is
        // ALLOWED to name the real clock, because it is the place that chooses it.
        var violations = FindViolations(
            AmbientClockPattern(),
            allowedPaths: ["src/SchoolManagement.Api/Program.cs"]);

        violations.ShouldBeEmpty(
            "Use the injected TimeProvider instead of reading the clock directly." +
            Environment.NewLine + Describe(violations));
    }

    [Fact]
    public void NoSynchronousBlockingOnTasks()
    {
        // .Result and .Wait() on a task block a thread pool thread while it waits. Under load that
        // exhausts the pool, and the classic symptom is a service that deadlocks or stops responding
        // entirely under exactly the traffic it was built for. CA1849 catches some of these; this
        // catches the rest.
        var violations = FindViolations(BlockingCallPattern(), allowedPaths: []);

        violations.ShouldBeEmpty(
            "Await the task instead of blocking on it with .Result or .Wait()." +
            Environment.NewLine + Describe(violations));
    }

    [Fact]
    public void NoAsyncVoid()
    {
        // An exception thrown from an async void method cannot be caught by the caller — it is raised
        // on the synchronization context and typically crashes the process. There is no situation in
        // this codebase that needs one.
        var violations = FindViolations(AsyncVoidPattern(), allowedPaths: []);

        violations.ShouldBeEmpty(
            "Return Task instead of void from an async method; an exception from async void cannot be " +
            "caught and will take the process down." + Environment.NewLine + Describe(violations));
    }

    [Fact]
    public void NoTodoWithoutATaskCardReference()
    {
        // A TODO with no task number is a note to nobody: it never gets scheduled and it accumulates.
        // Requiring TASK-#### means the work is tracked somewhere a human will look.
        var violations = FindViolations(UntrackedTodoPattern(), allowedPaths: []);

        violations.ShouldBeEmpty(
            "Every TODO/FIXME/HACK must reference a task card, for example: // TODO(TASK-0042): ..." +
            Environment.NewLine + Describe(violations));
    }

    [Fact]
    public void EndpointsDoNotReferenceDomainEntitiesDirectly()
    {
        // Endpoints speak in DTOs. A domain entity in an endpoint signature leaks the persistence shape
        // into the contract and turns any schema change into a breaking API change.
        var entityNames = ArchitectureAssemblies.Domain
            .GetTypes()
            .Where(type => type.IsClass && type.Namespace?.StartsWith(
                "SchoolManagement.Domain.Reference", StringComparison.Ordinal) == true)
            .Select(type => type.Name)
            .ToArray();

        entityNames.ShouldNotBeEmpty("Expected to find domain entities to check against.");

        var endpointFiles = SourceTree.ProductionFiles
            .Where(file => file.RelativePath.Contains("/Endpoints/", StringComparison.Ordinal));

        var violations = new List<string>();

        foreach (var file in endpointFiles)
        {
            for (var index = 0; index < file.Lines.Length; index++)
            {
                var line = file.Lines[index];

                if (IsCommentOrString(line))
                {
                    continue;
                }

                foreach (var entityName in entityNames)
                {
                    if (Regex.IsMatch(line, $@"\b{Regex.Escape(entityName)}\b"))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: {line.Trim()}");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            "Endpoints must not name domain entities; use a DTO." +
            Environment.NewLine + Describe(violations));
    }

    private static List<string> FindViolations(Regex pattern, string[] allowedPaths)
    {
        var violations = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            if (allowedPaths.Contains(file.RelativePath, StringComparer.Ordinal))
            {
                continue;
            }

            for (var index = 0; index < file.Lines.Length; index++)
            {
                var line = file.Lines[index];

                // Comments are skipped so a rule can be DESCRIBED in prose without tripping itself —
                // this file and several doc comments legitimately mention DateTime.UtcNow by name.
                if (IsCommentOrString(line))
                {
                    continue;
                }

                if (pattern.IsMatch(line))
                {
                    violations.Add($"{file.RelativePath}:{index + 1}: {line.Trim()}");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Crude but adequate comment detection.
    /// </summary>
    /// <remarks>
    /// This does not parse C#; it skips lines that START with a comment marker. A violation hidden
    /// after code on the same line as a trailing comment would still be caught, because the pattern
    /// matches anywhere in the line. The failure mode is a false NEGATIVE on a line beginning with a
    /// comment, which is acceptable — the alternative is a Roslyn analyser, and that is a
    /// disproportionate amount of machinery for this.
    /// </remarks>
    private static bool IsCommentOrString(string line)
    {
        var trimmed = line.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("///", StringComparison.Ordinal)
            || trimmed.StartsWith('*')
            || trimmed.StartsWith("/*", StringComparison.Ordinal);
    }

    private static string Describe(IEnumerable<string> violations) =>
        string.Join(Environment.NewLine, violations.Select(violation => "  " + violation));

    [GeneratedRegex(@"\b(DateTime|DateTimeOffset)\s*\.\s*(UtcNow|Now|Today)\b")]
    private static partial Regex AmbientClockPattern();

    [GeneratedRegex(@"\.(Result|Wait)\s*(\(\s*\))?(?=\s*[;.,)])")]
    private static partial Regex BlockingCallPattern();

    [GeneratedRegex(@"\basync\s+void\b")]
    private static partial Regex AsyncVoidPattern();

    [GeneratedRegex(@"\b(TODO|FIXME|HACK)\b(?!\s*\(\s*TASK-\d{4}\s*\))", RegexOptions.IgnoreCase)]
    private static partial Regex UntrackedTodoPattern();
}
