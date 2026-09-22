using SchoolManagement.Application.Abstractions.Settings;

namespace SchoolManagement.Api.Bootstrap;

/// <summary>
/// Proves that this host's configured file store really works, end to end, against the real
/// Cloudinary account (TASK-0005b stage D's "proven by the human once, with real credentials").
/// </summary>
/// <remarks>
/// <para>
/// USAGE: <c>dotnet SchoolManagement.Api.dll cloudinary-smoke-test</c> — on the VPS, as the service
/// account, once <c>/etc/gras/api.env</c> has the credentials:
/// <c>sudo -u gras env $(grep ^Cloudinary /etc/gras/api.env | xargs) dotnet
/// /opt/gras/api/SchoolManagement.Api.dll cloudinary-smoke-test</c>.
/// </para>
/// <para>
/// WHY A COMMAND RATHER THAN CLICKING THROUGH THE UI. The documented alternative is: sign in,
/// upload a logo, restart the service, reload the page. That needs a database, an admin account, a
/// frontend and a browser to answer one question — "can this process store a file and read it
/// back?" — and it answers it late, after the school is already depending on the box. This runs
/// before any of that exists, needs nothing but the credentials, and exercises the SAME store the
/// endpoints use, resolved from the same container.
/// </para>
/// <para>
/// Like <see cref="BootstrapAdminAccountCli"/>, it is dispatched before any web-hosting concern is
/// wired, so it can never become reachable over HTTP.
/// </para>
/// <para>
/// WHAT IT LEAVES BEHIND: one asset, a 73-byte 2×2 PNG, under the configured folder prefix. Assets
/// in this system are immutable and never deleted by design (a publication snapshot may reference
/// one forever), so this command does not delete it either — it prints the id so a human can
/// remove it from the Cloudinary dashboard if they want to.
/// </para>
/// </remarks>
internal static class CloudinarySmokeTestCli
{
    /// <summary>The subcommand name recognised as <c>args[0]</c>.</summary>
    public const string CommandName = "cloudinary-smoke-test";

    /// <summary>A valid 2×2 opaque PNG. Small enough to be free, real enough that Cloudinary accepts it as an image.</summary>
    private const string TestImageBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR42mNQz74BRAwQCgAk4gWpfbOgsAAAAABJRU5ErkJggg==";

    /// <summary>Uploads one asset, reads it back and compares the bytes.</summary>
    /// <param name="store">The configured store, resolved from the application's own container.</param>
    /// <param name="storeDescription">What the container actually chose, for the operator to see.</param>
    /// <returns>0 on success; 1 on any failure, with the reason on stderr.</returns>
    public static async Task<int> RunAsync(ISchoolImageStore store, string storeDescription)
    {
        ArgumentNullException.ThrowIfNull(store);

        // REFUSES TO PASS ON THE FAKE. A green run against the in-memory store would be worse than
        // no run at all: it is exactly the reassurance this command exists to withhold.
        if (storeDescription.Contains("InMemory", StringComparison.Ordinal))
        {
            await Console.Error.WriteLineAsync(
                $"The configured store is '{storeDescription}', not Cloudinary, so this proves nothing.")
                .ConfigureAwait(false);
            await Console.Error.WriteLineAsync(
                "Set Cloudinary__CloudName, Cloudinary__ApiKey and Cloudinary__ApiSecret, and remove " +
                "Cloudinary:AllowInMemoryStore (it deliberately wins over configured credentials).")
                .ConfigureAwait(false);

            return 1;
        }

        var original = Convert.FromBase64String(TestImageBase64);

        Console.WriteLine($"Store:    {storeDescription}");
        Console.WriteLine($"Uploading {original.Length} bytes as image/png ...");

        string assetId;

        try
        {
            assetId = await store.PutAsync(original, "image/png", CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types.
        // Justified: this is a diagnostic command whose ENTIRE PURPOSE is to turn any failure into
        // a readable sentence for an operator standing at a terminal. Narrowing the catch would
        // mean enumerating everything a third-party SDK, the TLS stack and the network can throw,
        // and being wrong about that list turns this tool's one job — explaining the problem — into
        // an unhandled stack trace. The failure is reported and the process exits non-zero, so
        // nothing is swallowed.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // The common causes are all configuration: a wrong secret, a wrong cloud name, or no
            // outbound network from this host.
            await Console.Error.WriteLineAsync($"UPLOAD FAILED: {exception.Message}").ConfigureAwait(false);
            return 1;
        }

        Console.WriteLine($"Uploaded: {assetId}");
        Console.WriteLine("Reading it back through a signed URL ...");

        byte[] roundTripped;

        try
        {
            using var stream = await store.OpenAsync(assetId, CancellationToken.None).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            roundTripped = buffer.ToArray();
        }
#pragma warning disable CA1031 // Do not catch general exception types — see the justification above.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            await Console.Error.WriteLineAsync($"READ-BACK FAILED: {exception.Message}").ConfigureAwait(false);
            await Console.Error.WriteLineAsync(
                "The upload worked, so the credentials are right. A failure HERE is the signed-URL " +
                "delivery path for a type=authenticated asset.")
                .ConfigureAwait(false);

            return 1;
        }

        // Byte equality, not just a length or a 200: Cloudinary can transform an image on delivery,
        // and a store that silently re-encodes the head teacher's signature is not a store this
        // application can use.
        if (!roundTripped.AsSpan().SequenceEqual(original))
        {
            await Console.Error.WriteLineAsync(
                $"BYTES DIFFER: stored {original.Length}, read back {roundTripped.Length}. The asset " +
                "came back transformed rather than as uploaded.")
                .ConfigureAwait(false);

            return 1;
        }

        Console.WriteLine($"OK: {roundTripped.Length} bytes returned, byte-for-byte identical.");
        Console.WriteLine();
        Console.WriteLine("The store works. Delete the test asset from the Cloudinary dashboard if you want:");
        Console.WriteLine($"  {assetId}");

        return 0;
    }
}
