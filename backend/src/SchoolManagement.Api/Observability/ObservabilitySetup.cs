using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Endpoints;
using Serilog;
using Serilog.Events;

namespace SchoolManagement.Api.Observability;

/// <summary>
/// Structured logging, traces and metrics.
/// </summary>
public static class ObservabilitySetup
{
    /// <summary>
    /// Adds Serilog and OpenTelemetry.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="validateOnStart">
    /// Whether to validate telemetry configuration at host start. See <c>HostMode</c>.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder AddObservability(
        this WebApplicationBuilder builder,
        bool validateOnStart = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddValidatedOptions<ObservabilityOptions, ObservabilityOptionsValidator>(
            builder.Configuration,
            ObservabilityOptions.SectionName,
            validateOnStart);

        // Read directly rather than through IOptions: logging must be configured before the service
        // provider exists, so there is nothing to resolve from yet.
        var options = builder.Configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        ConfigureSerilog(builder, options);
        ConfigureOpenTelemetry(builder, options);

        return builder;
    }

    private static void ConfigureSerilog(WebApplicationBuilder builder, ObservabilityOptions options)
    {
        builder.Services.AddSerilog((services, configuration) =>
        {
            configuration
                // Levels and overrides come from configuration so verbosity is tunable per environment
                // without a code change.
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.With<TraceContextEnricher>()
                // MUST be last: enrichers run in order, so redaction has to see the properties every
                // earlier enricher has already added.
                .Enrich.With<RedactSensitivePropertiesEnricher>()
                .WriteTo.Console();

            if (options.IsOtlpExportEnabled)
            {
                configuration.WriteTo.OpenTelemetry(sink =>
                {
                    sink.Endpoint = options.OtlpEndpoint;
                    sink.ResourceAttributes = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["service.name"] = options.ServiceName,
                    };
                });
            }
        });
    }

    private static void ConfigureOpenTelemetry(WebApplicationBuilder builder, ObservabilityOptions options)
    {
        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: options.ServiceName,
                serviceVersion: typeof(ObservabilitySetup).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio))
                    .AddAspNetCoreInstrumentation(instrumentation =>
                    {
                        instrumentation.RecordException = true;

                        // Health probes fire every few seconds forever. Tracing them buries real traffic
                        // in noise and costs money at every managed backend.
                        instrumentation.Filter = static httpContext =>
                            !IsHealthProbe(httpContext.Request.Path);
                    })
                    .AddHttpClientInstrumentation()
                    // Npgsql emits its own ActivitySource, so database spans come from the stable
                    // provider package rather than the pre-release EF Core instrumentation.
                    .AddNpgsql();

                if (options.IsOtlpExportEnabled)
                {
                    tracing.AddOtlpExporter(exporter =>
                        exporter.Endpoint = new Uri(options.OtlpEndpoint!));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (options.IsOtlpExportEnabled)
                {
                    metrics.AddOtlpExporter(exporter =>
                        exporter.Endpoint = new Uri(options.OtlpEndpoint!));
                }
            });
    }

    /// <summary>
    /// Adds Serilog's HTTP request logging, configured to keep health probes out of the log.
    /// </summary>
    /// <param name="app">The application.</param>
    public static void UseApiRequestLogging(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSerilogRequestLogging(logging =>
        {
            logging.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";

            // Successful health probes drop to Verbose so they are effectively silent, while a FAILING
            // probe still surfaces — which is the only time anyone wants to see one.
            logging.GetLevel = static (httpContext, _, exception) =>
            {
                if (exception is not null || httpContext.Response.StatusCode >= 500)
                {
                    return LogEventLevel.Error;
                }

                if (IsHealthProbe(httpContext.Request.Path))
                {
                    return LogEventLevel.Verbose;
                }

                return httpContext.Response.StatusCode >= 400
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
            };

            logging.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
            {
                // Deliberately NOT logged: the query string and request body. Both routinely contain
                // identifiers, search terms and personal data. The path alone is enough to identify the
                // endpoint.
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            };
        });
    }

    private static bool IsHealthProbe(PathString path) =>
        path.StartsWithSegments(HealthEndpoints.LivePath, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(HealthEndpoints.ReadyPath, StringComparison.OrdinalIgnoreCase);
}
