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

        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream);
    }
}
