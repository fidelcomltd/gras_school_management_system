namespace SchoolManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Decides how the integration tests obtain a PostgreSQL database, and reports LOUDLY when they cannot.
/// </summary>
/// <remarks>
/// <para>
/// The rule these tests exist to serve is "integration tests run against a real PostgreSQL, never the
/// in-memory provider" — because the in-memory provider does not enforce constraints, does not
/// translate SQL, and will happily pass a query that cannot execute against a real database.
/// </para>
/// <para>
/// TASK-0065: when no database is reachable, <see cref="ApiTestFixture.InitializeAsync"/> THROWS rather
/// than skipping. A skipped run is invisible — a set <c>POSTGRES_TEST_CONNECTION</c> always took
/// priority over the container path, so nothing ever forced the Testcontainers fallback to prove
/// itself (drift 2026-08-27, "Docker.DotNet.Enhanced is unverified in practice"). A thrown exception
/// fails every test in the collection loudly, with an actionable message, and — same as before — never
/// falls back to the in-memory provider.
/// </para>
/// <para>
/// Two ways to make them run, in priority order:
/// </para>
/// <list type="number">
/// <item>Set <c>POSTGRES_TEST_CONNECTION</c> to point at any PostgreSQL instance. Used as-is. This is
/// what CI does with a service container.</item>
/// <item>Make a container runtime's Docker API reachable — a local socket/pipe, or a daemon reachable
/// over TCP (for example one running inside WSL2, with no runtime visible from Windows itself).
/// Testcontainers then starts and disposes a throwaway PostgreSQL automatically, with no
/// configuration.</item>
/// </list>
/// <para>
/// TASK-0078 (2026-09-17, human directive): which of the two this class actually sees is now decided
/// one layer up, by <c>backend/scripts/ci.ps1</c> and <c>lib/postgres-test-connection.ps1</c>, not by
/// this class. A gate run defaults to leaving <c>POSTGRES_TEST_CONNECTION</c> unset — i.e. path (2)
/// above, the local container — and reads the hosted database
/// (<c>~/.gras/pg-test.txt</c>) into path (1) only when the run is invoked with <c>-UseHostedDb</c>.
/// The file no longer "wins" over the container by default; see <c>.agent/rules/gates.md</c> §7. This
/// class's own priority order above is unchanged — it still just asks "is
/// <c>POSTGRES_TEST_CONNECTION</c> set" — the change is entirely in who sets it and when.
/// </para>
/// </remarks>
internal static class DatabaseAvailability
{
    /// <summary>Environment variable holding a connection string to an existing test database.</summary>
    public const string ConnectionEnvironmentVariable = "POSTGRES_TEST_CONNECTION";

    /// <summary>The PostgreSQL image used when Testcontainers provides the database.</summary>
    /// <remarks>
    /// Pinned to a specific minor version, not <c>latest</c>. An implicitly-floating database image means
    /// the suite can start failing because someone published a new image, and the failure appears
    /// unrelated to any change in this repository.
    /// </remarks>
    public const string PostgresImage = "postgres:17.6-alpine";

    /// <summary>
    /// The WSL2 NAT-mode localhost-forwarding address the Windows host can reach even when
    /// <c>$env:DOCKER_HOST</c> is unset, or set to an address that does not actually work. TASK-0065
    /// found the reference machine's <c>$env:DOCKER_HOST</c> set to the IPv4 literal
    /// <c>tcp://127.0.0.1:2375</c>, which does NOT answer — only the hostname form below does.
    /// </summary>
    private const string LocalhostFallbackEndpoint = "tcp://localhost:2375";

    /// <summary>How long a single endpoint probe may take before it counts as "does not answer".</summary>
    /// <remarks>
    /// Deliberately short, and never retried: this must stay far below the tens of seconds a failed
    /// container START costs (see <see cref="ResolveDockerEndpointAsync"/>'s remarks) — a single HTTP
    /// GET against <c>/_ping</c>, nothing more.
    /// </remarks>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// Caches <see cref="ResolveDockerEndpointAsync"/>'s result for the lifetime of the process. The
    /// probes it runs are real network calls; every caller (the availability check AND the container
    /// build itself) must see the SAME resolved endpoint from a SINGLE set of probes, not repeat them.
    /// </summary>
    private static readonly Lazy<Task<string?>> LazyResolvedDockerEndpoint = new(ResolveDockerEndpointAsync);

    /// <summary>An externally supplied connection string, or <c>null</c> if none is configured.</summary>
    public static string? ExternalConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable) is { } value &&
        !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    /// <summary>
    /// The Docker endpoint to hand Testcontainers explicitly via
    /// <c>ContainerBuilder.WithDockerEndpoint</c>, or <c>null</c> if none answers. Never trust ambient
    /// <c>$env:DOCKER_HOST</c> implicitly — TASK-0065 found it set to an address that does not work.
    /// </summary>
    public static Task<string?> ResolvedDockerEndpointAsync => LazyResolvedDockerEndpoint.Value;

    /// <summary>The reason the tests cannot run, or <c>null</c> when a database is obtainable.</summary>
    public static async Task<string?> UnavailableReasonAsync()
    {
        if (ExternalConnectionString is not null)
        {
            return null;
        }

        if (await ResolvedDockerEndpointAsync.ConfigureAwait(false) is not null)
        {
            return null;
        }

        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        var dockerHostTried = string.IsNullOrWhiteSpace(dockerHost) ? "unset" : $"'{dockerHost}' (did not answer)";

        return "NO POSTGRESQL AVAILABLE for the integration tests — this FAILS the run, it does not " +
               "skip it. They require a real database because the EF Core in-memory provider does not " +
               "enforce constraints or translate SQL, so it would pass queries that cannot actually " +
               "execute. Both fallbacks were tried and neither produced a database: " +
               $"(1) {ConnectionEnvironmentVariable} is not set; " +
               $"(2) no Docker endpoint answered a /_ping — tried $DOCKER_HOST ({dockerHostTried}), " +
               $"{LocalhostFallbackEndpoint}, and the local named pipe/socket. " +
               $"Fix either way: set {ConnectionEnvironmentVariable} to a PostgreSQL connection string, " +
               $"or make a container runtime's Docker API reachable and Testcontainers will start " +
               $"{PostgresImage} automatically. See backend/README.md, 'Running the tests'.";
    }

    /// <summary>
    /// Resolves the Docker endpoint to use, first candidate that actually answers wins:
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item><c>$env:DOCKER_HOST</c>, if set — but only once a probe against it succeeds. Its mere
    /// presence is never trusted on its own.</item>
    /// <item><see cref="LocalhostFallbackEndpoint"/> — the WSL2 NAT-mode path that answers when the
    /// literal IPv4 form in (1) does not.</item>
    /// <item>The platform's local named pipe (Windows) or Unix socket (Linux/podman), if present.</item>
    /// </list>
    /// <para>
    /// Probed by a short-timeout <c>/_ping</c>, never by starting a container: a failed container start
    /// costs tens of seconds and buries the real message under a wall of unrelated Testcontainers
    /// logging before the suite can report anything useful.
    /// </para>
    /// </remarks>
    private static async Task<string?> ResolveDockerEndpointAsync()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(dockerHost) &&
            await TcpEndpointAnswersAsync(dockerHost).ConfigureAwait(false))
        {
            return dockerHost;
        }

        if (await TcpEndpointAnswersAsync(LocalhostFallbackEndpoint).ConfigureAwait(false))
        {
            return LocalhostFallbackEndpoint;
        }

        return LocalNamedEndpoint();
    }

    /// <summary>
    /// True only if <paramref name="dockerEndpoint"/> parses as a <c>tcp://</c> endpoint AND a
    /// short-timeout GET against its <c>/_ping</c> succeeds. Any other scheme, and any failure —
    /// refused, timed out, DNS failure — answers <c>false</c>: a probe failing is the expected case on
    /// a machine with no reachable daemon at that address, not an exceptional one.
    /// </summary>
    private static async Task<bool> TcpEndpointAnswersAsync(string dockerEndpoint)
    {
        if (!Uri.TryCreate(dockerEndpoint, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "tcp", StringComparison.OrdinalIgnoreCase))
        {
            // npipe:// and unix:// are handled by LocalNamedEndpoint's plain existence check below —
            // pinging a named pipe/socket over HTTP needs a different transport than this TCP probe.
            return false;
        }

        try
        {
            using var client = new HttpClient { Timeout = ProbeTimeout };
            using var response = await client.GetAsync($"http://{uri.Host}:{uri.Port}/_ping")
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// The platform's local named pipe (Windows) or Unix socket (Linux/podman), by existence check
    /// only — no network round trip needed to stat a local file.
    /// </summary>
    private static string? LocalNamedEndpoint() => OperatingSystem.IsWindows()
        ? (Directory.Exists(@"\\.\pipe\") && File.Exists(@"\\.\pipe\docker_engine")
            ? "npipe://./pipe/docker_engine"
            : null)
        : UnixSocketEndpoint();

    private static string? UnixSocketEndpoint()
    {
        if (File.Exists("/var/run/docker.sock"))
        {
            return "unix:///var/run/docker.sock";
        }

        var podmanSocket = $"/run/user/{Environment.GetEnvironmentVariable("UID")}/podman/podman.sock";
        return File.Exists(podmanSocket) ? $"unix://{podmanSocket}" : null;
    }
}
