using System.Text.Json;

namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// Loads the build-generated OpenAPI document once per test run.
/// </summary>
/// <remarks>
/// <para>
/// Reads <c>backend/artifacts/openapi/SchoolManagement.Api.json</c> — the same git-ignored artefact
/// <c>backend/scripts/generate-openapi.ps1</c> writes with no database connection, and the same file
/// <c>backend/scripts/ci.ps1</c>'s drift gate already regenerates and hashes against the committed
/// contract. This is deliberately the ONLY place that reads it: a second, independent way of obtaining
/// the document (starting the application, calling the endpoint, re-implementing generation) would be a
/// new drift surface between "what the tests see" and "what ci.ps1 checks".
/// </para>
/// <para>
/// <c>ci.ps1</c> generates this file before running tests, specifically so <see cref="OpenApiContractTests"/>
/// has it available. Running the architecture tests directly (for example <c>dotnet test</c> against just
/// this project) requires generating it first — see the exception message below.
/// </para>
/// <para>
/// MISSING FAILS LOUDLY; STALE MUST TOO. <see cref="Load"/> also refuses an artefact that is OLDER
/// than the newest <c>.cs</c> file anywhere under <c>backend/src</c> — not just
/// <c>src/SchoolManagement.Api</c>. The document is shaped by types outside that project too: for
/// example <c>ReferenceEndpoints.cs</c> returns <c>PingResponse</c>, <c>PagedResult&lt;SampleRecordDto&gt;</c>
/// and <c>CreateSampleRecordResponse</c>, all declared in <c>SchoolManagement.Application</c>. An
/// edit to one of those changes the generated document without touching the Api project at all, so
/// an Api-only freshness bar would stay green over a now-stale artefact — the exact "stale passes
/// silently" case this check exists to close, just on a path it did not cover. Inside <c>ci.ps1</c>
/// this never fires — the artefact is always regenerated immediately beforehand, so it is always
/// newer than every source file. Outside it, a bare <c>dotnet test</c> would otherwise silently
/// assert all eight document properties against whatever JSON happens to be sitting on disk, which
/// may predate a source edit made since it was last generated — the same "reported success over a
/// document it did not write" defect family as TASK-0010.
/// </para>
/// <para>
/// TRADE-OFF, DELIBERATE: scanning all four projects instead of one makes a spurious failure
/// slightly more likely — for example a fresh checkout that writes a <c>.cs</c> file a moment after
/// the committed JSON, purely from filesystem timestamp jitter, with no real staleness. That is
/// accepted: the failure is loud and names the offending file, which is recoverable in one command,
/// whereas the failure mode it replaces — a stale artefact silently reported as fresh — is not
/// noticed until the wrong contract ships. It also cannot happen inside <c>ci.ps1</c> at all, since
/// generation always happens immediately before this runs.
/// </para>
/// </remarks>
internal static class OpenApiDocument
{
    private static readonly Lazy<JsonDocument> LazyDocument = new(Load);

    /// <summary>The root element of the generated OpenAPI document.</summary>
    public static JsonElement Root => LazyDocument.Value.RootElement;

    private static JsonDocument Load()
    {
        var path = Path.Combine(
            SourceTree.BackendRoot.FullName,
            "artifacts",
            "openapi",
            "SchoolManagement.Api.json");

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"No generated OpenAPI document at '{path}'. Generate it with " +
                "'backend/scripts/generate-openapi.ps1' — no database or -Promote needed, and " +
                "'backend/scripts/ci.ps1' does this automatically before running tests.");
        }

        var artifactWrittenAtUtc = File.GetLastWriteTimeUtc(path);
        var newestSource = FindNewestBackendSourceFile();

        if (newestSource is { } source && source.WrittenAtUtc > artifactWrittenAtUtc)
        {
            throw new InvalidOperationException(
                $"The generated OpenAPI document at '{path}' was last written " +
                $"{artifactWrittenAtUtc:O}, which is OLDER than '{source.RelativePath}' " +
                $"({source.WrittenAtUtc:O}) — the artefact predates the current source and does not " +
                "reflect it. Regenerate it with 'backend/scripts/generate-openapi.ps1' — no database " +
                "or -Promote needed, and 'backend/scripts/ci.ps1' does this automatically before " +
                "running tests.");
        }

        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream);
    }

    /// <summary>
    /// Finds the most recently written <c>.cs</c> file anywhere under <c>backend/src</c> (all four
    /// projects), ignoring build output. The document is shaped by types in Domain, Application and
    /// Infrastructure as well as Api — see the class remarks — so the freshness bar has to cover all
    /// of them, not just the project <c>generate-openapi.ps1</c> starts.
    /// </summary>
    private static (string RelativePath, DateTime WrittenAtUtc)? FindNewestBackendSourceFile()
    {
        var sourceDirectory = new DirectoryInfo(Path.Combine(SourceTree.BackendRoot.FullName, "src"));

        if (!sourceDirectory.Exists)
        {
            return null;
        }

        return sourceDirectory
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Select(file => (
                RelativePath: Path
                    .GetRelativePath(SourceTree.BackendRoot.FullName, file.FullName)
                    .Replace('\\', '/'),
                WrittenAtUtc: file.LastWriteTimeUtc))
            .OrderByDescending(entry => entry.WrittenAtUtc)
            .Cast<(string RelativePath, DateTime WrittenAtUtc)?>()
            .FirstOrDefault();
    }

    private static bool IsBuildOutput(FileInfo file)
    {
        var path = file.FullName.Replace('\\', '/');

        return path.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }
}
