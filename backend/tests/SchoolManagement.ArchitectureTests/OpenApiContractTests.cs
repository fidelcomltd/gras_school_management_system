using System.Text.Json;

namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// Enforces the OpenAPI documentation rules mechanically, against the real generated document.
/// </summary>
/// <remarks>
/// <para>
/// "Document your endpoints" as a written rule decays. These tests make it a build gate: an endpoint
/// without a summary, or a DTO without an example, fails here. The frontend generates a typed client
/// from this document, so a gap in it becomes a gap in every consumer.
/// </para>
/// <para>
/// MOVED HERE FROM <c>SchoolManagement.IntegrationTests</c> (TASK-0009). Every one of these tests asserts
/// a property of the generated DOCUMENT — none touches the database or a request handler. They used to
/// reach the document by starting the application under <c>WebApplicationFactory</c> and fetching
/// <c>/openapi/v1.json</c> over HTTP, which coupled them to database availability for no reason: on
/// 2026-08-26 that coupling let <c>EverySchema_HasAnExample</c> skip silently on a machine with no
/// reachable PostgreSQL, and a missing <c>SecureArmResponse</c> example reached the COMMITTED contract
/// undetected as a result. Reading the build-generated document directly — see
/// <see cref="OpenApiDocument"/> — removes the database from the picture entirely, so these run on every
/// machine, every time.
/// </para>
/// </remarks>
public sealed class OpenApiContractTests
{
    private static JsonElement Document => OpenApiDocument.Root;

    [Fact]
    public void Document_DescribesTheReferenceEndpoints()
    {
        var paths = Document.GetProperty("paths");

        paths.TryGetProperty("/api/v1/reference/ping", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/v1/reference/records", out _).ShouldBeTrue();
    }

    [Fact]
    public void Document_UsesConcreteVersionSegmentsNotRouteTemplates()
    {
        // URL-segment versioning routes on "/api/v{version:apiVersion}". Left unprocessed, the document
        // would contain a literal "{version}" placeholder and a generated client would either take a
        // pointless version argument or call an unescaped URL. VersionedPathDocumentTransformer fixes it;
        // this test proves the fix is still in place.
        foreach (var path in Document.GetProperty("paths").EnumerateObject())
        {
            path.Name.ShouldNotContain(
                "{version}",
                Case.Sensitive,
                $"Path '{path.Name}' still contains the version route template.");
        }
    }

    [Fact]
    public void EveryOperation_HasASummaryDescriptionAndOperationId()
    {
        var failures = new List<string>();

        foreach (var path in Document.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var location = $"{operation.Name.ToUpperInvariant()} {path.Name}";

                RequireNonEmpty(operation.Value, "summary", location, failures);
                RequireNonEmpty(operation.Value, "description", location, failures);
                RequireNonEmpty(operation.Value, "operationId", location, failures);

                if (!operation.Value.TryGetProperty("tags", out var tags) || tags.GetArrayLength() == 0)
                {
                    failures.Add($"{location}: no tags");
                }
            }
        }

        failures.ShouldBeEmpty(
            "Every operation needs a summary, description, operationId and tag — the generated client " +
            $"and the docs UI are built from them.{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures.Select(failure => "  - " + failure)));
    }

    [Fact]
    public void EveryOperation_DocumentsItsErrorResponses()
    {
        // An operation that only documents 200 tells a client author nothing about what can go wrong, so
        // they write no error handling. Every operation must declare at least one non-2xx response.
        var failures = new List<string>();

        foreach (var path in Document.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var codes = operation.Value.GetProperty("responses")
                    .EnumerateObject()
                    .Select(response => response.Name)
                    .ToArray();

                if (!codes.Any(code => code.Length == 3 && code[0] is '4' or '5'))
                {
                    failures.Add($"{operation.Name.ToUpperInvariant()} {path.Name}: only {string.Join(", ", codes)}");
                }
            }
        }

        failures.ShouldBeEmpty(
            $"Every operation must document at least one error response.{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures.Select(failure => "  - " + failure)));
    }

    [Fact]
    public void EverySchema_HasADescription()
    {
        var failures = new List<string>();

        foreach (var schema in Document.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            // Descriptions come from XML doc comments, which is why CS1591 is an error in Api and
            // Application: a missing /// is a missing description here.
            if (!HasNonEmpty(schema.Value, "description"))
            {
                failures.Add(schema.Name);
            }
        }

        failures.ShouldBeEmpty(
            "Every contract schema needs a description, supplied by its XML doc comment. " +
            $"Missing: {string.Join(", ", failures)}");
    }

    [Fact]
    public void EverySchema_HasAnExample()
    {
        // Descriptions say what a field means; examples say what a VALUE looks like, which is what a
        // client author actually needs for dates, IDs and formats. Register new DTOs in OpenApiExamples.
        var failures = new List<string>();

        foreach (var schema in Document.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("example", out _))
            {
                failures.Add(schema.Name);
            }
        }

        failures.ShouldBeEmpty(
            "Every contract schema needs a worked example. Add an entry to " +
            $"SchoolManagement.Api.OpenApi.OpenApiExamples for: {string.Join(", ", failures)}");
    }

    [Fact]
    public void EveryDateTimeProperty_HasAnExample()
    {
        // Date formats are the single most misread part of any contract. SchemaExampleTransformer has a
        // backstop for this, so a failure here means the backstop was removed.
        var failures = new List<string>();

        foreach (var schema in Document.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            foreach (var property in properties.EnumerateObject())
            {
                var isDateTime = property.Value.TryGetProperty("format", out var format) &&
                                 format.GetString() == "date-time";

                if (isDateTime && !property.Value.TryGetProperty("example", out _))
                {
                    failures.Add($"{schema.Name}.{property.Name}");
                }
            }
        }

        failures.ShouldBeEmpty(
            $"Every date-time property must carry an example. Missing: {string.Join(", ", failures)}");
    }

    [Fact]
    public void Document_HasApiLevelDocumentation()
    {
        var info = Document.GetProperty("info");

        info.GetProperty("title").GetString().ShouldBe("School Management API");
        info.GetProperty("version").GetString().ShouldBe("1.0");

        var description = info.GetProperty("description").GetString();
        description.ShouldNotBeNullOrWhiteSpace();

        // The cross-cutting conventions a consumer must know about, documented in the contract itself
        // rather than in a README they will not read.
        description.ShouldContain("9457");
        description.ShouldContain("422");
    }

    private static bool HasNonEmpty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        !string.IsNullOrWhiteSpace(value.GetString());

    private static void RequireNonEmpty(
        JsonElement element,
        string propertyName,
        string location,
        List<string> failures)
    {
        if (!HasNonEmpty(element, propertyName))
        {
            failures.Add($"{location}: missing {propertyName}");
        }
    }
}
