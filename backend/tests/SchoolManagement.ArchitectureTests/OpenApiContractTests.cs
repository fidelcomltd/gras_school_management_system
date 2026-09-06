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
    public void ProblemSchemas_DeclareErrorCodeAndTraceId()
    {
        // TASK-0012 / AUDIT.md B1: the API attaches errorCode and traceId to every problem response
        // (ResultExtensions.cs, GlobalExceptionHandler.cs, Program.cs's CustomizeProblemDetails), and the
        // schema's own description tells a client to branch on errorCode — so both members must be
        // declared, not left as undeclared extension data forbidden by additionalProperties: false.
        // traceId is attached centrally to EVERY problem response, including framework-produced ones, so
        // it is required. errorCode is attached only by this API's own result mapping and exception
        // handler, so a framework-produced problem response (a model-binding 400, an auth 401) can lack
        // it — it must NOT be required.
        var schemas = Document.GetProperty("components").GetProperty("schemas");
        var failures = new List<string>();

        foreach (var schemaName in new[] { "ProblemDetails", "HttpValidationProblemDetails" })
        {
            var schema = schemas.GetProperty(schemaName);

            if (!schema.TryGetProperty("properties", out var properties) ||
                !properties.TryGetProperty("errorCode", out var errorCode) ||
                !HasNonEmpty(errorCode, "description"))
            {
                failures.Add($"{schemaName}: missing a declared, described 'errorCode' property");
            }

            if (!properties.TryGetProperty("traceId", out var traceId) || !HasNonEmpty(traceId, "description"))
            {
                failures.Add($"{schemaName}: missing a declared, described 'traceId' property");
            }

            var required = schema.TryGetProperty("required", out var requiredElement)
                ? requiredElement.EnumerateArray().Select(entry => entry.GetString()).ToArray()
                : [];

            if (!required.Contains("traceId"))
            {
                failures.Add($"{schemaName}: 'traceId' must be required — it is attached centrally to every response");
            }

            if (required.Contains("errorCode"))
            {
                failures.Add(
                    $"{schemaName}: 'errorCode' must NOT be required — framework-produced problem " +
                    "responses (model binding, auth middleware) never carry it");
            }
        }

        failures.ShouldBeEmpty(
            $"Problem-detail schemas must declare both extension members correctly.{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures.Select(failure => "  - " + failure)));
    }

    [Fact]
    public void ProblemDetailsSchema_DeclaresLockedUntilButHttpValidationProblemDetailsDoesNot()
    {
        // TASK-0027: ResultExtensions.cs attaches a `lockedUntil` extension member to the 423 sign-in
        // body (approved contract delta §2), but the generator's additionalProperties: false made the
        // committed document say that response could not carry it — invisible to §4.4's drift check
        // because both the regenerated and committed documents were equally wrong. Pinning the DECLARED
        // shape here, the same way ProblemSchemas_DeclareErrorCodeAndTraceId pins errorCode/traceId, so
        // it cannot silently regress to prose-only again.
        var schemas = Document.GetProperty("components").GetProperty("schemas");

        var problemDetails = schemas.GetProperty("ProblemDetails");
        problemDetails.TryGetProperty("properties", out var problemProperties).ShouldBeTrue();
        problemProperties.TryGetProperty("lockedUntil", out var lockedUntil).ShouldBeTrue(
            "ProblemDetails must declare 'lockedUntil' — the field POST /auth/sign-in's 423 response " +
            "actually sends (spec 6.1.11).");
        HasNonEmpty(lockedUntil, "description").ShouldBeTrue("'lockedUntil' must carry a description.");
        lockedUntil.GetProperty("format").GetString().ShouldBe("date-time");

        var problemRequired = problemDetails.TryGetProperty("required", out var requiredElement)
            ? requiredElement.EnumerateArray().Select(entry => entry.GetString()).ToArray()
            : [];

        problemRequired.ShouldNotContain(
            "lockedUntil",
            "'lockedUntil' must NOT be required — it is present only on the one 423 outcome, not on " +
            "every problem response.");

        // A validation failure can never also be an account lockout, so the OTHER problem schema gets
        // no such property at all — this is not a shape every problem response carries.
        var validationProblemDetails = schemas.GetProperty("HttpValidationProblemDetails");

        if (validationProblemDetails.TryGetProperty("properties", out var validationProperties))
        {
            validationProperties.TryGetProperty("lockedUntil", out _).ShouldBeFalse(
                "HttpValidationProblemDetails should not declare 'lockedUntil' — a validation failure " +
                "is never also a lockout.");
        }
    }

    [Fact]
    public void EverySchemaExample_ValidatesAgainstItsOwnSchema()
    {
        // An example that a client cannot actually receive is worse than no example: it teaches a wrong
        // shape. This is a structural check, not a full JSON Schema validator — it asserts exactly what a
        // "closed" object schema promises: every required property is present, and (when
        // additionalProperties is false) every member of the example is one the schema actually declares.
        // This is what EveryOperation_DocumentsItsErrorResponses did not catch for TASK-0012 / B1: both
        // problem schemas' examples carried errorCode and traceId while additionalProperties: false and
        // no matching declared property made that example invalid against its own schema.
        var failures = new List<string>();

        foreach (var schema in Document.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("example", out var example) ||
                example.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            ValidateObjectExample(schema.Name, schema.Value, example, failures);
        }

        failures.ShouldBeEmpty(
            $"Every schema's example must validate against its own schema.{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures.Select(failure => "  - " + failure)));
    }

    private static void ValidateObjectExample(
        string schemaName,
        JsonElement schema,
        JsonElement example,
        List<string> failures)
    {
        var properties = schema.TryGetProperty("properties", out var propertiesElement)
            ? propertiesElement
            : default;

        var allowsAdditionalProperties =
            !schema.TryGetProperty("additionalProperties", out var additionalProperties) ||
            additionalProperties.ValueKind != JsonValueKind.False;

        if (!allowsAdditionalProperties)
        {
            foreach (var member in example.EnumerateObject())
            {
                if (properties.ValueKind != JsonValueKind.Object || !properties.TryGetProperty(member.Name, out _))
                {
                    failures.Add(
                        $"{schemaName}: example carries '{member.Name}', which the schema does not " +
                        "declare and additionalProperties: false forbids");
                }
            }
        }

        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var requiredProperty in required.EnumerateArray())
            {
                var name = requiredProperty.GetString();

                if (name is not null && !example.TryGetProperty(name, out _))
                {
                    failures.Add($"{schemaName}: example is missing required property '{name}'");
                }
            }
        }
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
