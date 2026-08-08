using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Builds an <see cref="ApplicationDbContext"/> for the <c>dotnet ef</c> tooling.
/// </summary>
/// <remarks>
/// <para>
/// Without this, <c>dotnet ef migrations add</c> has to boot the Api host to find a context — which
/// means it needs the full production configuration (secret store, identity provider, every
/// <c>ValidateOnStart</c> check) just to generate a C# file. This factory decouples the design-time
/// tooling from the runtime composition root.
/// </para>
/// <para>
/// NO CONNECTION STRING IS EMBEDDED HERE, not even a harmless-looking local default. A committed
/// default is the thing somebody eventually edits to point at a real database, and it is how
/// credentials reach source control. It is also how a migration gets generated against, or applied
/// to, the wrong server. The environment variable must be set explicitly:
/// </para>
/// <code>
/// # PowerShell
/// $env:SCHOOLMANAGEMENT_DESIGNTIME_CONNECTION = "Host=localhost;Port=5432;Database=schoolmanagement;Username=...;Password=..."
/// dotnet ef migrations add &lt;Name&gt; --project src/SchoolManagement.Infrastructure --startup-project src/SchoolManagement.Api
/// </code>
/// <para>
/// The migration's generated SQL does not depend on the database's CONTENTS, only on the provider, so
/// pointing this at a scratch database is fine and is the recommended practice.
/// </para>
/// </remarks>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <summary>Environment variable holding the design-time connection string.</summary>
    private const string ConnectionEnvironmentVariable = "SCHOOLMANAGEMENT_DESIGNTIME_CONNECTION";

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The environment variable is not set. Deliberate: a clear failure with instructions beats a
    /// silent fallback to somebody else's database.
    /// </exception>
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable '{ConnectionEnvironmentVariable}' is not set. The EF Core " +
                "design-time tooling needs a connection string, and none is committed to this " +
                "repository on purpose. Set it for your shell session and re-run the command — see " +
                "README.md, \"Adding a migration\".");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(ApplicationDbContextDefaults.MigrationsHistoryTable))
            // MUST match the runtime configuration. If the design-time model used different naming,
            // every generated migration would try to rename every table and column in the database.
            .UseSnakeCaseNamingConvention();

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// Settings that MUST be identical between the design-time factory and the runtime registration.
/// </summary>
/// <remarks>
/// Shared constants rather than two string literals: if the migrations history table name differed
/// between design time and runtime, EF Core would believe the database had no migrations applied and
/// try to create every table again.
/// </remarks>
internal static class ApplicationDbContextDefaults
{
    /// <summary>The migrations history table, named to match the snake_case convention.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";
}
