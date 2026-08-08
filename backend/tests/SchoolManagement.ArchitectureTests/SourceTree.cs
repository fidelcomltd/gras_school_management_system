namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// Locates the repository's source files so conventions can be checked against SOURCE TEXT.
/// </summary>
/// <remarks>
/// Some rules cannot be expressed against compiled metadata. <c>DateTime.UtcNow</c> is a property
/// access that leaves no trace in the type graph NetArchTest inspects, and <c>.Result</c> on a task is
/// indistinguishable from any other property read. A source scan is a blunt instrument, but it catches
/// exactly the mistakes that reviewers miss, and it runs in milliseconds.
/// </remarks>
internal static class SourceTree
{
    // Lazy rather than static field initialisers. Static initialisers run in DECLARATION order, so
    // when ProductionFiles was declared above BackendRoot it ran first and read BackendRoot as null —
    // a NullReferenceException wrapped in TypeInitializationException, which is a genuinely confusing
    // failure to diagnose. Lazy removes the ordering dependency, so reordering these members can never
    // reintroduce the bug.
    private static readonly Lazy<DirectoryInfo> LazyBackendRoot = new(FindBackendRoot);
    private static readonly Lazy<IReadOnlyList<SourceFile>> LazyProductionFiles = new(LoadProductionFiles);

    /// <summary>
    /// Every <c>.cs</c> file under <c>src/</c>, excluding generated output.
    /// </summary>
    public static IReadOnlyList<SourceFile> ProductionFiles => LazyProductionFiles.Value;

    /// <summary>The backend root: the directory containing the solution file.</summary>
    /// <exception cref="InvalidOperationException">
    /// The solution could not be found. Thrown rather than silently scanning nothing — a source-scanning
    /// test that finds no files would otherwise report success.
    /// </exception>
    public static DirectoryInfo BackendRoot => LazyBackendRoot.Value;

    private static DirectoryInfo FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.EnumerateFiles("SchoolManagement.slnx").Any() ||
                directory.EnumerateFiles("SchoolManagement.sln").Any())
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the backend root by walking up from '{AppContext.BaseDirectory}'. " +
            "The source-convention tests need the repository on disk; they cannot run from a packaged " +
            "assembly alone.");
    }

    // Returns the concrete List (CA1859); the method group still converts to
    // Func<IReadOnlyList<SourceFile>> for the Lazy above, because Func is covariant in its result.
    private static List<SourceFile> LoadProductionFiles()
    {
        var sourceDirectory = new DirectoryInfo(Path.Combine(BackendRoot.FullName, "src"));

        if (!sourceDirectory.Exists)
        {
            throw new InvalidOperationException(
                $"Expected a source directory at '{sourceDirectory.FullName}'.");
        }

        var files = sourceDirectory
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file))
            .Select(file => new SourceFile(
                Path.GetRelativePath(BackendRoot.FullName, file.FullName).Replace('\\', '/'),
                File.ReadAllLines(file.FullName)))
            .ToList();

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"No source files found under '{sourceDirectory.FullName}'.");
        }

        return files;
    }

    private static bool IsGenerated(FileInfo file)
    {
        var path = file.FullName.Replace('\\', '/');

        return path.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase)
            || file.Name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || file.Name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>A source file's repo-relative path and its lines.</summary>
/// <param name="RelativePath">Path relative to the backend root, with forward slashes.</param>
/// <param name="Lines">The file's lines, in order.</param>
internal sealed record SourceFile(string RelativePath, string[] Lines);
