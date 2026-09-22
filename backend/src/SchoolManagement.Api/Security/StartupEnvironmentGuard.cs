using Microsoft.Extensions.Options;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Settings;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Refuses to start the host when a Development-only setting is active in a deployed environment.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS IS A HOSTED SERVICE AND NOT A CHECK IN <c>Program.cs</c>: build-time OpenAPI generation
/// (<c>Microsoft.Extensions.ApiDescription.Server</c>) runs the entry point to CONSTRUCT the host, then
/// aborts before starting it. A guard that throws during service registration therefore breaks
/// <c>dotnet build</c> — the document generator cannot get far enough to read the endpoints, and the
/// contract can never be regenerated. The same applies to any tool that inspects the host, including
/// the EF Core design-time tooling.
/// </para>
/// <para>
/// Running the checks in <see cref="StartAsync"/> puts them exactly where they belong: they fire when
/// the service actually starts serving traffic, and are invisible to build-time tooling. This is the
/// same mechanism <c>ValidateOnStart()</c> uses, which is why a missing connection string does not
/// break the build either.
/// </para>
/// <para>
/// Throwing rather than logging is deliberate. Both conditions below are "this deployment is unsafe or
/// useless"; a warning in a startup log is read by nobody, whereas a failed start halts a rollout.
/// </para>
/// </remarks>
internal sealed class StartupEnvironmentGuard(
    IHostEnvironment environment,
    IOptions<ApiAuthenticationOptions> authenticationOptions,
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<CloudinaryOptions> cloudinaryOptions)
    : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment())
        {
            return Task.CompletedTask;
        }

        var failures = new List<string>();

        if (string.Equals(
                authenticationOptions.Value.Mode,
                AuthenticationModes.Placeholder,
                StringComparison.Ordinal))
        {
            failures.Add(
                $"'{ApiAuthenticationOptions.SectionName}:{nameof(ApiAuthenticationOptions.Mode)}' is " +
                "'Placeholder', which authenticates NOBODY: every protected endpoint would return 401. " +
                "Choose and implement a real mechanism — that decision needs human sign-off per root " +
                "CLAUDE.md §5 and is tracked in docs/ASSUMPTIONS.md.");
        }

        if (cloudinaryOptions.Value.AllowInMemoryStore)
        {
            // TASK-0005b stage D. This flag makes the in-memory image store win over configured
            // credentials — deliberately, because the integration fixture sets it and the host it
            // builds runs as Development, where Program.cs loads user-secrets (a developer with
            // real credentials there would otherwise have the whole suite uploading to the school's
            // live media library).
            //
            // The cost of that precedence is THIS failure mode: a deployed host carrying the flag
            // over from appsettings.Development.template.json — where it ships as true — would boot
            // perfectly, accept a logo upload, and lose it on the next restart, with no error
            // anywhere. CloudinaryOptionsValidator cannot catch it, because with real credentials
            // also present the configuration is entirely valid; it is only wrong FOR THIS
            // ENVIRONMENT, which is exactly what this guard is for.
            failures.Add(
                $"'{CloudinaryOptions.SectionName}:{nameof(CloudinaryOptions.AllowInMemoryStore)}' is " +
                "enabled, which stores uploaded logos and signatures in process memory: they are lost " +
                "on every restart, and this flag takes precedence even when real Cloudinary " +
                "credentials are configured. Remove it from this environment's configuration.");
        }

        if (databaseOptions.Value.EnableSensitiveDataLogging)
        {
            failures.Add(
                $"'{DatabaseOptions.SectionName}:" +
                $"{nameof(DatabaseOptions.EnableSensitiveDataLogging)}' is enabled. It writes query " +
                "parameter values — which are user data — into logs. Remove it from this " +
                "environment's configuration.");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Refusing to start in environment '{environment.EnvironmentName}':" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures.Select(failure => "  - " + failure)));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
