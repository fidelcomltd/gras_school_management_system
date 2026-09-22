using Microsoft.Extensions.Options;

namespace SchoolManagement.Api.Configuration;

/// <summary>
/// Where the Data Protection key ring is stored, bound from the <c>DataProtection</c> section.
/// </summary>
/// <remarks>
/// <para>
/// The key ring signs the CSRF tokens <c>CsrfTokenService</c> issues, and nothing else in this
/// application. Session tokens are database-backed, so losing the ring signs nobody out; it only
/// invalidates outstanding CSRF tokens, which clients replace by re-fetching <c>GET /auth/csrf</c>.
/// </para>
/// <para>
/// ON THE VPS this points at a directory owned by the service account with no access for anyone
/// else (the provisioning script creates it mode 0700). The keys are stored as plain XML: on Linux
/// there is no OS key store to wrap them with, and <c>ProtectKeysWith*</c> would need a
/// certificate whose own private key has exactly the same storage problem. Directory permissions
/// are the protection, which is why the deploy sets them and this type records that it does.
/// </para>
/// </remarks>
public sealed class KeyRingOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Absolute path to the directory holding the key ring — for example
    /// <c>/var/lib/gras/dataprotection-keys</c>. Unset means the framework's default location,
    /// which a restart may not preserve.
    /// </summary>
    public string? KeyRingPath { get; set; }
}

/// <summary>Validates <see cref="KeyRingOptions"/> at startup.</summary>
/// <remarks>
/// A RELATIVE path is rejected rather than resolved. It would resolve against the process working
/// directory, which differs between the systemd unit, a manual <c>dotnet run</c> and the EF
/// tooling — so the same configuration would silently address three different key rings.
/// </remarks>
internal sealed class KeyRingOptionsValidator : IValidateOptions<KeyRingOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, KeyRingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.KeyRingPath))
        {
            return ValidateOptionsResult.Success;
        }

        // IsPathRooted, and DELIBERATELY EVALUATED AGAINST THE RUNNING PLATFORM. This value names a
        // directory on THIS host, so "absolute" means absolute here, and the answer differs by
        // platform in both directions: '/var/lib/gras/dataprotection-keys' (the production value) is
        // rooted on Windows too, while 'C:\ProgramData\gras\keys' is NOT rooted on Linux — there it
        // is an ordinary relative name containing a colon and backslashes, and accepting it would
        // create a directory literally called 'C:\ProgramData\gras\keys' inside whatever the working
        // directory happened to be. Rejecting it is therefore right, not a gap.
        //
        // IsPathFullyQualified was tried first and is wrong for the opposite reason: it calls the
        // production Linux path "not fully qualified" on a Windows dev machine, so the real
        // configuration failed validation everywhere it was not already running.
        //
        // Either way the failure mode being ruled out is the same one: a relative path silently
        // resolving against three different working directories (the service, a local run, the EF
        // tooling). See KeyRingOptionsValidatorTests for why its assertions are platform-aware.
        return Path.IsPathRooted(options.KeyRingPath.Trim())
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{SectionNameOf(nameof(KeyRingOptions.KeyRingPath))} must be an absolute path, " +
                $"found '{options.KeyRingPath}'. A relative path resolves against the process working " +
                "directory, which differs between the service, a local run and the EF tooling.");
    }

    private static string SectionNameOf(string property) =>
        $"{KeyRingOptions.SectionName}:{property}";
}

/// <summary>
/// Logs, at startup, that the key ring will not survive a restart when no path is configured.
/// </summary>
/// <remarks>
/// A hosted service rather than a check in <c>Program.cs</c> for the reason
/// <see cref="Security.StartupEnvironmentGuard"/> gives in full: a guard that runs during service
/// registration also runs during build-time OpenAPI generation and the EF design-time tooling.
/// Warns rather than throws — see <see cref="KeyRingOptions"/> for why the failure mode is a
/// nuisance and not a breach.
/// </remarks>
internal sealed class DataProtectionKeyRingGuard(
    IHostEnvironment environment,
    IOptions<KeyRingOptions> options,
    ILogger<DataProtectionKeyRingGuard> logger)
    : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.Value.KeyRingPath))
        {
            ApiLog.DataProtectionKeyRingNotPersisted(logger, environment.EnvironmentName);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
