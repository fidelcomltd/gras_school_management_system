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
/// THE DELIBERATE DESIGN DECISION HERE: when no database is reachable these tests SKIP with an
/// explanatory message. They do not silently pass, and they never fall back to the in-memory provider.
/// A skipped test is visibly absent from the run; a test that quietly substitutes a weaker database
/// reports success while having verified almost nothing, which is the worse failure by far.
/// </para>
/// <para>
/// Two ways to make them run, in priority order:
/// </para>
/// <list type="number">
/// <item>Set <c>POSTGRES_TEST_CONNECTION</c> to point at any PostgreSQL instance. Used as-is. This is
/// what CI does with a service container.</item>
/// <item>Install a container runtime (Docker Desktop or Podman). Testcontainers then starts and
/// disposes a throwaway PostgreSQL automatically, with no configuration.</item>
/// </list>
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

    /// <summary>An externally supplied connection string, or <c>null</c> if none is configured.</summary>
    public static string? ExternalConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable) is { } value &&
        !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    /// <summary>
    /// Whether a container runtime appears to be available.
    /// </summary>
    /// <remarks>
    /// Probed by looking for the runtime's socket/pipe rather than by attempting to start a container,
    /// because a failed container start takes tens of seconds and produces a wall of unrelated
    /// Testcontainers logging before the test suite can report anything useful.
    /// </remarks>
    public static bool IsContainerRuntimeAvailable => OperatingSystem.IsWindows()
        ? Directory.Exists(@"\\.\pipe\") && File.Exists(@"\\.\pipe\docker_engine")
        : File.Exists("/var/run/docker.sock") ||
          File.Exists($"/run/user/{Environment.GetEnvironmentVariable("UID")}/podman/podman.sock");

    /// <summary>The reason the tests cannot run, or <c>null</c> when a database is obtainable.</summary>
    public static string? UnavailableReason =>
        ExternalConnectionString is not null || IsContainerRuntimeAvailable
            ? null
            : "NO POSTGRESQL AVAILABLE — these integration tests were SKIPPED, not passed. " +
              "They require a real database because the EF Core in-memory provider does not enforce " +
              "constraints or translate SQL, so it would pass queries that cannot actually execute. " +
              $"Fix either way: (1) set {ConnectionEnvironmentVariable} to a PostgreSQL connection " +
              "string, or (2) install a container runtime (Docker Desktop / Podman) and Testcontainers " +
              $"will start {PostgresImage} automatically. See backend/README.md, 'Running the tests'.";
}
