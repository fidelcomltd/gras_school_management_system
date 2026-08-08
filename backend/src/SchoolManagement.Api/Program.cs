using System.Threading.RateLimiting;
using Asp.Versioning;
using Scalar.AspNetCore;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Endpoints;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Observability;
using SchoolManagement.Api.OpenApi;
using SchoolManagement.Api.Security;
using SchoolManagement.Application;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Behaviors;
using SchoolManagement.Infrastructure;

// ═════════════════════════════════════════════════════════════════════════════════════════════
//  COMPOSITION ROOT
//
//  This file wires things together and contains NO logic. If you are tempted to put a rule, a
//  calculation or a data access call here, it belongs in a handler.
//
//  Ordering in the second half (the middleware pipeline) is SECURITY-CRITICAL and is annotated
//  inline. Read those comments before moving a line.
// ═════════════════════════════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

var isDevelopment = builder.Environment.IsDevelopment();

// ── Configuration sources ────────────────────────────────────────────────────────────────────
// Precedence, lowest to highest: appsettings.json → appsettings.{Environment}.json →
// user-secrets (Development only) → environment variables. Later sources win, so a deployed
// environment overrides a committed file and nothing secret needs to be committed at all.
// AddUserSecrets is Development-only: outside development the store does not exist, and reaching
// for it would mask a missing environment variable.
if (isDevelopment)
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Configuration.AddEnvironmentVariables();

// Is this process running only to emit the OpenAPI document? Generation STARTS the host to read its
// route metadata, so every ValidateOnStart check would otherwise demand a configured database and a
// chosen identity provider just to produce a JSON file. In that mode the eager startup checks are not
// registered — nothing else changes. See HostMode for why this is a flag and not a dummy connection
// string, and scripts/generate-openapi.ps1 for the one command that sets it.
var validateOnStart = !HostMode.IsContractGeneration(builder.Configuration);

// To add a cloud secret store, register its configuration provider HERE — it layers on top and
// every consumer keeps using IOptions/ISecretProvider unchanged. See docs/adr/0009-secret-store.md.

// ── Observability first ──────────────────────────────────────────────────────────────────────
// Registered before anything else so failures during the remaining startup are logged properly
// rather than through the bootstrap logger.
builder.AddObservability(validateOnStart);

// ── Options ──────────────────────────────────────────────────────────────────────────────────
builder.Services
    .AddValidatedOptions<CorsOptions, CorsOptionsValidator>(
        builder.Configuration, CorsOptions.SectionName, validateOnStart)
    .AddValidatedOptions<RateLimitingOptions, RateLimitingOptionsValidator>(
        builder.Configuration, RateLimitingOptions.SectionName, validateOnStart)
    .AddValidatedOptions<RequestLimitsOptions, RequestLimitsOptionsValidator>(
        builder.Configuration, RequestLimitsOptions.SectionName, validateOnStart);

var pipelineOptions = builder.Services
    .AddOptions<PipelineOptions>()
    .Bind(builder.Configuration.GetSection(PipelineOptions.SectionName));

if (validateOnStart)
{
    pipelineOptions.ValidateOnStart();
}

var requestLimits = builder.Configuration
    .GetSection(RequestLimitsOptions.SectionName)
    .Get<RequestLimitsOptions>() ?? new RequestLimitsOptions();

// ── Kestrel limits ───────────────────────────────────────────────────────────────────────────
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxRequestBodySize = requestLimits.MaxRequestBodyBytes;

    // Do not advertise the server product/version to anyone probing.
    kestrel.AddServerHeader = false;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(json =>
{
    // Reject unknown JSON members instead of ignoring them. A client that misspells a field would
    // otherwise get a 201 and silently lose data — the request "succeeded" but did not do what it
    // asked. Better to fail loudly with a 400.
    json.SerializerOptions.UnmappedMemberHandling =
        System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;

    // Bounds nesting depth. A deeply nested payload is cheap to send and expensive to parse, and
    // without a limit it is a denial-of-service primitive.
    json.SerializerOptions.MaxDepth = requestLimits.MaxJsonDepth;

    // Enums cross the wire as NAMES, not integers (root CLAUDE.md §8). An integer enum is unreadable
    // in a log or a contract, and — worse — reordering the C# enum silently changes the meaning of
    // every stored and in-flight value. Registered here so it applies to every future DTO by default.
    json.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// ── Application layers ───────────────────────────────────────────────────────────────────────
// TimeProvider.System is the single source of "now". Handlers take TimeProvider, never
// DateTimeOffset.UtcNow, so tests can control the clock. NoAmbientDateTimeTests enforces it.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, validateOnStart);

// ── HTTP surface ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddApiAuthentication(builder.Configuration, validateOnStart);
builder.Services.AddApiVersioningScheme();
builder.Services.AddApiOpenApi();
builder.Services.AddEndpointModules();

builder.Services.AddProblemDetails(options =>
{
    // The single place traceId is attached, so EVERY problem response carries it — including ones
    // the framework produces (a 400 from model binding, a 401 from auth middleware) that never pass
    // through our own result mapping.
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            System.Diagnostics.Activity.Current?.TraceId.ToString()
            ?? context.HttpContext.TraceIdentifier;

        // `instance` identifies the specific occurrence. The path is safe; the query string is not
        // included because it routinely carries identifiers and search terms.
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path.Value;
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddCors(corsOptions =>
{
    var configured = builder.Configuration
        .GetSection(CorsOptions.SectionName)
        .Get<CorsOptions>() ?? new CorsOptions();

    corsOptions.AddPolicy(CorsOptions.PolicyName, policy =>
    {
        // WithOrigins only — there is no AllowAnyOrigin path in this codebase, in any environment.
        policy
            .WithOrigins([.. configured.AllowedOrigins])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)
            .SetPreflightMaxAge(TimeSpan.FromSeconds(configured.PreflightMaxAgeSeconds));

        if (configured.AllowCredentials)
        {
            policy.AllowCredentials();
        }
    });
});

builder.Services.AddRateLimiter(rateLimiter =>
{
    var configured = builder.Configuration
        .GetSection(RateLimitingOptions.SectionName)
        .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

    // 429, not the default 503. 503 says "the server is broken"; 429 says "you are going too fast",
    // which is what actually happened and what a well-behaved client knows how to back off from.
    rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    rateLimiter.OnRejected = static (context, cancellationToken) =>
    {
        // Retry-After turns a rejection into something a client can act on rather than guess at.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };

    rateLimiter.AddPolicy(RateLimitingOptions.DefaultPolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Partitioned by authenticated user where possible, falling back to remote IP. Without a
            // partition key every client would share one global budget, so one noisy caller would
            // rate-limit everybody else.
            partitionKey: ResolveRateLimitPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = configured.PermitLimit,
                Window = TimeSpan.FromSeconds(configured.WindowSeconds),
                QueueLimit = configured.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }));

    rateLimiter.AddPolicy(RateLimitingOptions.SensitivePolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolveRateLimitPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = configured.SensitivePermitLimit,
                Window = TimeSpan.FromSeconds(configured.SensitiveWindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }));
});

var app = builder.Build();

// ═════════════════════════════════════════════════════════════════════════════════════════════
//  MIDDLEWARE PIPELINE — ORDER IS SECURITY-CRITICAL
// ═════════════════════════════════════════════════════════════════════════════════════════════

// FIRST. Everything below may throw, and an unhandled exception must become a ProblemDetails
// response rather than a stack trace or a blank 500.
app.UseExceptionHandler();

// Before anything that logs or responds, so the correlation ID appears on error responses too.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!isDevelopment)
{
    // Development is served over plain HTTP; sending HSTS from localhost pins the browser to HTTPS
    // for the whole domain and is painful to undo.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseApiRequestLogging();

// CORS BEFORE authentication and rate limiting. A browser preflight (OPTIONS) carries no
// credentials, so if it reached the auth middleware it would be rejected with a 401 that has no
// CORS headers — and the browser would report a confusing CORS error for what is really an
// ordering bug.
app.UseCors(CorsOptions.PolicyName);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────────────────────────────────────
app.MapHealthEndpoints();

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// URL-segment versioning. The generated contract substitutes the concrete segment (/api/v1/...)
// via VersionedPathDocumentTransformer, so the committed document has real paths a client
// generator can use.
var versionedApi = app
    .MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet)
    .RequireRateLimiting(RateLimitingOptions.DefaultPolicyName);

versionedApi.MapEndpointModules(app.Services, app.Logger);

if (isDevelopment)
{
    // Development only. The document and the interactive UI describe every endpoint and every
    // error shape, which is useful to a developer and a free reconnaissance map to anyone else.
    // The committed contract at ../contracts/openapi.json is how non-developers consume the API.
    // .AllowAnonymous() is REQUIRED, not decorative. The authorisation fallback policy applies to every
    // endpoint that declares no authorisation metadata — including these, which the framework maps for
    // us. Without it both return 401 to every caller and the docs are unusable, which is exactly what
    // the integration tests caught.
    app.MapOpenApi().AllowAnonymous();

    app.MapScalarApiReference(scalar => scalar
            .WithTitle("School Management API")
            .WithOpenApiRoutePattern($"/openapi/{OpenApiSetup.DocumentName}.json"))
        .AllowAnonymous();
}

await app.RunAsync().ConfigureAwait(false);

// ── Local helpers ────────────────────────────────────────────────────────────────────────────

// Chooses the rate-limit partition: the authenticated user if there is one, else the remote IP.
//
// Falls back to a single shared bucket when the remote IP is unknown (which happens behind a
// misconfigured proxy). That is deliberately the CONSERVATIVE choice: sharing a bucket may throttle
// legitimate callers, whereas exempting unknown clients would make the limiter trivial to bypass.
static string ResolveRateLimitPartitionKey(HttpContext httpContext)
{
    if (httpContext.User.Identity?.IsAuthenticated == true)
    {
        var userId = httpContext.User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }
    }

    return $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}

/// <summary>
/// Entry point marker.
/// </summary>
/// <remarks>
/// Declared so <c>WebApplicationFactory&lt;Program&gt;</c> in the integration tests has a type to
/// reference, and so <c>AddUserSecrets&lt;Program&gt;</c> can locate the assembly. A top-level
/// program's generated class is internal, which is why InternalsVisibleTo is set in the csproj.
/// </remarks>
public partial class Program;
