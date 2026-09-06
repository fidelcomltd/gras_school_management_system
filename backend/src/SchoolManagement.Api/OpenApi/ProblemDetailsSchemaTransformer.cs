using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SchoolManagement.Api.OpenApi;

/// <summary>
/// Declares the <c>errorCode</c>, <c>traceId</c> and (on <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>
/// only) <c>lockedUntil</c> extension members on the problem-detail schemas.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS. <c>Program.cs</c> sets <c>UnmappedMemberHandling.Disallow</c> globally, which is
/// right for request bodies: an unrecognised field in a request is a client bug and should be reported,
/// not silently dropped. The generator applies the same <c>additionalProperties: false</c> to every
/// schema it derives from a C# type, including <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> and
/// <see cref="Microsoft.AspNetCore.Http.HttpValidationProblemDetails"/> — but those two are RFC 9457
/// problem objects, where <c>Extensions</c> members ARE the mechanism, and this API attaches
/// <c>errorCode</c> (<c>Http/ResultExtensions.cs</c>, <c>Http/GlobalExceptionHandler.cs</c>),
/// <c>traceId</c> (<c>Program.cs</c>'s <c>CustomizeProblemDetails</c> hook) and — on a locked-account
/// response only — <c>lockedUntil</c> (<c>Http/ResultExtensions.cs</c>'s <c>LockedError</c> branch) to
/// responses of these shapes without any of the three ever appearing as a declared C# property to
/// generate a schema from.
/// </para>
/// <para>
/// Both schemas stay CLOSED (<c>additionalProperties: false</c>) rather than opened up — see TASK-0012.
/// Declaring the members here, instead, keeps the closed-type discipline while making what every client
/// is told to depend on actually part of the contract. TASK-0027 found that <c>lockedUntil</c> had been
/// sent on <c>POST /auth/sign-in</c>'s <c>423</c> since TASK-0003 without ever being declared this way —
/// the committed document said the response could not carry it while the running API did anyway, and
/// §4.4's drift check cannot see that class of gap (see `.agent/drift/2026-Q3.md`).
/// </para>
/// <para>
/// <c>traceId</c> is marked <c>required</c>: it is attached by the single central
/// <c>CustomizeProblemDetails</c> hook, so it is on every problem response including framework-produced
/// ones. <c>errorCode</c> and <c>lockedUntil</c> are declared but NOT required: <c>errorCode</c> is
/// attached only by this API's own result mapping and exception handler, so a problem response the
/// framework produces directly — a model-binding 400, a 401 from authentication middleware — carries
/// no <c>errorCode</c>; <c>lockedUntil</c> is attached only when the error is a <c>LockedError</c> (spec
/// 6.1.11's <c>423</c>), which is one specific outcome of one specific operation, not a shape every
/// problem response carries. Marking either required would document something the API does not always
/// send.
/// </para>
/// <para>
/// Runs after <see cref="SchemaExampleTransformer"/> in registration order, but does not depend on that
/// ordering: it only adds entries to <see cref="Microsoft.OpenApi.OpenApiSchema.Properties"/> and does
/// not read or overwrite anything the example transformer sets.
/// </para>
/// </remarks>
internal sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>The C# types this transformer augments, and each one's extension-member descriptions.</summary>
    /// <remarks>
    /// <c>LockedUntil</c> is <see langword="null"/> for <see cref="Microsoft.AspNetCore.Http.HttpValidationProblemDetails"/>:
    /// a validation failure can never also be a lockout, so that schema gets no such property.
    /// </remarks>
    private static readonly Dictionary<Type, (string ErrorCode, string TraceId, string? LockedUntil)> Descriptions =
        new Dictionary<Type, (string ErrorCode, string TraceId, string? LockedUntil)>
        {
            [typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)] = (
                ErrorCode:
                    "Stable, machine-readable error code. Clients branch on this, never on `detail`. " +
                    "Absent when this response was produced directly by the framework (for example a " +
                    "model-binding failure or an authentication challenge) rather than by this API's own " +
                    "result mapping.",
                TraceId:
                    "Correlation id for this specific response occurrence. Present on every error " +
                    "response; quote it when reporting a problem.",
                LockedUntil:
                    "UTC time the account's lockout ends (spec 6.1.11: five failed attempts locks it for " +
                    "fifteen minutes). Present only on the `423 Locked` response `POST /auth/sign-in` " +
                    "returns when the SUBMITTED password is correct but the account is currently locked " +
                    "— never on any other problem response."),

            [typeof(Microsoft.AspNetCore.Http.HttpValidationProblemDetails)] = (
                ErrorCode:
                    "Stable, machine-readable error code. Clients branch on this, never on `detail`. " +
                    "Absent when this response was produced directly by the framework rather than by " +
                    "this API's own result mapping.",
                TraceId:
                    "Correlation id for this specific response occurrence. Present on every error " +
                    "response; quote it when reporting a problem.",
                LockedUntil: null),
        };

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        // Only the whole-object schema, never a property schema (which reuses the same CLR type
        // information but represents a single field, e.g. nothing here is itself typed ProblemDetails).
        if (context.JsonPropertyInfo is not null)
        {
            return Task.CompletedTask;
        }

        if (!Descriptions.TryGetValue(context.JsonTypeInfo.Type, out var descriptions))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        schema.Properties["errorCode"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = descriptions.ErrorCode,
        };

        schema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = descriptions.TraceId,
        };

        if (descriptions.LockedUntil is { } lockedUntilDescription)
        {
            schema.Properties["lockedUntil"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "date-time",
                Description = lockedUntilDescription,
                Example = JsonValue.Create(OpenApiExamples.CanonicalTimestamp),
            };
        }

        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        schema.Required.Add("traceId");

        return Task.CompletedTask;
    }
}
