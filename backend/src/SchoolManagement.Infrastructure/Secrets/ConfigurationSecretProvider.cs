using Microsoft.Extensions.Configuration;
using SchoolManagement.Application.Abstractions.Secrets;

namespace SchoolManagement.Infrastructure.Secrets;

/// <summary>
/// <see cref="ISecretProvider"/> over <see cref="IConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the ONLY shipped implementation, and it is a deliberate choice rather than an omission.
/// <c>IConfiguration</c> is already a layered chain — <c>appsettings.json</c>, then the environment
/// file, then user-secrets locally, then environment variables, then any provider added at the
/// composition root. Adding a cloud secret store therefore means registering ITS configuration
/// provider in <c>Program.cs</c>; this class and every caller stay unchanged.
/// </para>
/// <para>
/// The deployment target is not yet decided, so committing to an SDK now would mean adding a large
/// cloud dependency with a real chance of being the wrong cloud. To add one when that is settled:
/// </para>
/// <code>
/// // Azure:  builder.Configuration.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
/// // AWS:    builder.Configuration.AddSecretsManager();
/// // Vault:  builder.Configuration.Add(new VaultConfigurationSource(...));
/// </code>
/// <para>
/// One caveat to know: configuration providers load at startup, so a secret ROTATED in the store is
/// not seen until the next reload or restart. If a rotation window matters, either enable the
/// provider's reload interval or replace this implementation with one that calls the store directly —
/// which is exactly what the seam is for.
/// </para>
/// </remarks>
internal sealed class ConfigurationSecretProvider(IConfiguration configuration) : ISecretProvider
{
    /// <inheritdoc />
    public ValueTask<string?> TryGetSecretAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        var value = configuration[name];

        // Treat whitespace as absent. An empty environment variable is a common deployment mistake,
        // and reporting "configured" for it just moves the failure somewhere less obvious.
        return ValueTask.FromResult(string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <inheritdoc />
    public async ValueTask<string> GetRequiredSecretAsync(string name, CancellationToken cancellationToken)
    {
        var value = await TryGetSecretAsync(name, cancellationToken).ConfigureAwait(false);

        // The KEY is named to make this actionable; the VALUE is never echoed, because exception
        // messages reach logs and error trackers.
        return value ?? throw new InvalidOperationException(
            $"Required secret '{name}' is not configured. Set it via user-secrets for local " +
            "development, or via the environment/secret store in a deployed environment. See " +
            "README.md, \"Configuration and secrets\".");
    }
}
