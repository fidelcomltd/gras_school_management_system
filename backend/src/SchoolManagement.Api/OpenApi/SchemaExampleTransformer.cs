using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SchoolManagement.Api.OpenApi;

/// <summary>
/// Attaches examples to schemas, structurally.
/// </summary>
/// <remarks>
/// <para>
/// Examples end up in the document as real <c>example</c> members — machine-readable, so a client
/// generator, a mock server or a docs UI can use them. Prose inside a <c>&lt;summary&gt;</c> cannot do
/// any of that.
/// </para>
/// <para>
/// The transformer is called once per schema. There are two cases, and the ORDER of the checks matters
/// because a property's schema also carries the property's own type information:
/// </para>
/// <list type="number">
/// <item><b>A property schema</b> (<c>JsonPropertyInfo</c> is set). The example is read out of the
/// PARENT type's registered example, so an object's example and its fields' examples are guaranteed
/// consistent.</item>
/// <item><b>A type schema</b> (no <c>JsonPropertyInfo</c>). The registered whole-object example is
/// attached.</item>
/// </list>
/// <para>
/// Finally, any <c>date-time</c> field still without an example gets the canonical timestamp. Date
/// formats are the single most commonly misread part of an API contract, so none is left to guesswork
/// even if somebody forgets to register a DTO.
/// </para>
/// </remarks>
internal sealed class SchemaExampleTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (context.JsonPropertyInfo is { } property)
        {
            var parentExample = OpenApiExamples.TryGetExample(property.DeclaringType);

            if (parentExample is JsonObject parentObject &&
                parentObject.TryGetPropertyValue(property.Name, out var propertyExample) &&
                propertyExample is not null)
            {
                // DeepClone because a JsonNode may only be attached to one parent, and this node is
                // still owned by the parsed parent example.
                schema.Example = propertyExample.DeepClone();
            }
        }
        else
        {
            if (OpenApiExamples.TryGetExample(context.JsonTypeInfo.Type) is { } typeExample)
            {
                schema.Example = typeExample;
            }

            // Only when the schema has none already: an XML doc comment always wins, so this fills the
            // gap for framework types we cannot annotate rather than overriding our own documentation.
            if (string.IsNullOrWhiteSpace(schema.Description) &&
                OpenApiExamples.TryGetDescription(context.JsonTypeInfo.Type) is { } description)
            {
                schema.Description = description;
            }
        }

        // Backstop for dates, applied whether or not the type was registered.
        if (schema.Example is null &&
            string.Equals(schema.Format, "date-time", StringComparison.Ordinal))
        {
            schema.Example = JsonValue.Create(OpenApiExamples.CanonicalTimestamp);
        }

        return Task.CompletedTask;
    }
}
