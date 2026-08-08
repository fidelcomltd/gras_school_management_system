using System.Text.Json.Nodes;
using SchoolManagement.Api.Endpoints;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Reference.Ping;
using SchoolManagement.Application.Reference.SampleRecords;

namespace SchoolManagement.Api.OpenApi;

/// <summary>
/// Worked examples for every contract type, declared once each.
/// </summary>
/// <remarks>
/// <para>
/// WHY EXAMPLES ARE NOT OPTIONAL. A description says what a field means; an example says what a VALUE
/// looks like, and that is what a consumer actually needs for anything with a format:
/// <c>"2026-08-03T09:30:00+00:00"</c> versus <c>"03/08/2026"</c>, an opaque ID versus a bare integer.
/// Without them, every client author guesses, and half of them guess wrong.
/// </para>
/// <para>
/// ONE DECLARATION PER TYPE. Only whole-object examples are written here.
/// <see cref="SchemaExampleTransformer"/> derives each PROPERTY's example by reading the matching field
/// out of its parent's example, so a field's example and the object's example cannot contradict each
/// other — there is nothing to keep in sync.
/// </para>
/// <para>
/// ADDING A CONTRACT TYPE: add an entry below. <c>OpenApiContractTests</c> asserts every schema in the
/// generated document has an example, so a new DTO without one fails the build rather than shipping
/// undocumented.
/// </para>
/// </remarks>
internal static class OpenApiExamples
{
    /// <summary>Canonical timestamp used by every example, and as the fallback for any date-time field.</summary>
    /// <remarks>
    /// A fixed value, never "now": an example that changes on every regeneration would make the
    /// committed contract differ on every build and turn the CI drift check into noise.
    /// </remarks>
    public const string CanonicalTimestamp = "2026-08-03T09:30:00+00:00";

    /// <summary>An example identifier, shaped like the version 7 GUIDs this service generates.</summary>
    private const string ExampleId = "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40";

    /// <summary>
    /// Whole-object example JSON, keyed by contract type. Property names are camelCase, matching the
    /// wire format.
    /// </summary>
    public static IReadOnlyDictionary<Type, string> ByType { get; } = new Dictionary<Type, string>
    {
        [typeof(PingResponse)] = $$"""
            {
              "message": "Hello, Ada.",
              "serverTimeUtc": "{{CanonicalTimestamp}}",
              "apiVersion": "1.0"
            }
            """,

        [typeof(CreateSampleRecordCommand)] = """
            {
              "label": "Term 1 timetable draft",
              "note": "Carried over from the previous academic year."
            }
            """,

        [typeof(CreateSampleRecordResponse)] = $$"""
            {
              "id": "{{ExampleId}}"
            }
            """,

        [typeof(SampleRecordDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "label": "Term 1 timetable draft",
              "note": "Carried over from the previous academic year.",
              "createdAtUtc": "{{CanonicalTimestamp}}",
              "modifiedAtUtc": null
            }
            """,

        // Shows a middle page, so hasNextPage and hasPreviousPage are both meaningful rather than
        // both false as they would be on a single-page example.
        [typeof(PagedResult<SampleRecordDto>)] = $$"""
            {
              "items": [
                {
                  "id": "{{ExampleId}}",
                  "label": "Term 1 timetable draft",
                  "note": "Carried over from the previous academic year.",
                  "createdAtUtc": "{{CanonicalTimestamp}}",
                  "modifiedAtUtc": null
                }
              ],
              "page": 2,
              "pageSize": 20,
              "totalCount": 137,
              "totalPages": 7,
              "hasNextPage": true,
              "hasPreviousPage": true
            }
            """,

        [typeof(WhoAmIResponse)] = """
            {
              "userId": "subject-identifier-from-your-identity-provider",
              "isAuthenticated": true
            }
            """,

        // The error contract matters more to a client author than any success shape: it is what they
        // have to handle and cannot easily provoke on demand. Both framework types are given examples
        // showing the extension members this API adds — errorCode and traceId — which a consumer would
        // otherwise not know exist, because they are additionalProperties in the schema.
        [typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)] = """
            {
              "type": "urn:schoolmanagement:error:sample_record.label_taken",
              "title": "Conflict with current state",
              "status": 409,
              "detail": "A record with that label already exists.",
              "instance": "/api/v1/reference/records",
              "errorCode": "sample_record.label_taken",
              "traceId": "0af7651916cd43dd8448eb211c80319c"
            }
            """,

        [typeof(Microsoft.AspNetCore.Http.HttpValidationProblemDetails)] = """
            {
              "type": "urn:schoolmanagement:error:request.validation_failed",
              "title": "Validation failed",
              "status": 422,
              "detail": "One or more validation errors occurred.",
              "instance": "/api/v1/reference/records",
              "errorCode": "request.validation_failed",
              "traceId": "0af7651916cd43dd8448eb211c80319c",
              "errors": {
                "Label": [
                  "Label is required."
                ],
                "PageSize": [
                  "PageSize must be at most 100."
                ]
              }
            }
            """,
    };

    /// <summary>
    /// Descriptions for contract types that cannot carry an XML doc comment.
    /// </summary>
    /// <remarks>
    /// Almost every schema's description comes from its <c>///</c> comment automatically. These two are
    /// FRAMEWORK types — we do not own the source, so there is nowhere to put a comment, and they would
    /// otherwise be the only undescribed schemas in the document. They also happen to be the most
    /// important ones for a client author: the error shape is what they must handle and cannot easily
    /// provoke on demand.
    /// </remarks>
    public static IReadOnlyDictionary<Type, string> DescriptionsByType { get; } = new Dictionary<Type, string>
    {
        [typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)] =
            "An RFC 9457 problem response. Returned for every error. Branch on the `errorCode` " +
            "extension member — it is stable — and never on `detail`, which is human-readable prose that " +
            "may be reworded. `traceId` identifies this specific occurrence in the server logs; quote it " +
            "when reporting a problem. `type` is a stable URN of the form " +
            "`urn:schoolmanagement:error:<code>`.",

        [typeof(Microsoft.AspNetCore.Http.HttpValidationProblemDetails)] =
            "An RFC 9457 problem response for a validation failure, returned with status 422. Extends " +
            "the standard problem shape with `errors`: an object keyed by request property name, whose " +
            "values are the messages for that property, suitable for attaching to form fields. A 400 " +
            "(rather than 422) means the request itself could not be parsed.",
    };

    /// <summary>
    /// Parses the example for <paramref name="type"/>, or returns <c>null</c> if none is registered.
    /// </summary>
    /// <param name="type">The contract type.</param>
    /// <returns>A fresh node each call, so callers may attach it to a schema.</returns>
    public static JsonNode? TryGetExample(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return ByType.TryGetValue(type, out var json) ? JsonNode.Parse(json) : null;
    }

    /// <summary>
    /// Returns the registered description for <paramref name="type"/>, or <c>null</c>.
    /// </summary>
    /// <param name="type">The contract type.</param>
    public static string? TryGetDescription(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return DescriptionsByType.GetValueOrDefault(type);
    }
}
