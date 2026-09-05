using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Auth.Bootstrap;

namespace SchoolManagement.Api.Bootstrap;

/// <summary>
/// The console command spec 6.1.6 calls for: "Installation runs a seed command that creates one
/// <c>admin_account</c>." OFF THE WIRE by construction — this never starts Kestrel, never maps a
/// route, and <see cref="BootstrapAdminAccountCommand"/> has no <c>MapPost</c> anywhere, so
/// <c>PrivilegeDeclarationGuard</c> never even sees it.
/// </summary>
/// <remarks>
/// <para>
/// USAGE: <c>dotnet run --project src/SchoolManagement.Api -- bootstrap-admin --email=admin@example.com
/// --staff-name="Chisom Maxwell"</c>.
/// </para>
/// <para>
/// DELIBERATELY KNOWS NOTHING ABOUT INFRASTRUCTURE OR DI COMPOSITION — only Program.cs (the exempt
/// composition root, see <c>DependencyDirectionTests.Api_DoesNotDependOnInfrastructureOutsideComposition</c>)
/// may reference Infrastructure from this project. Program.cs builds the plain
/// <c>ServiceProvider</c> this command runs under (no Kestrel, no middleware pipeline, no
/// authentication scheme — nothing HTTP-shaped) and hands this type only an already-resolved
/// <see cref="ISender"/>.
/// </para>
/// <para>
/// Reuses the ordinary mediator pipeline (validation, logging, the transaction) rather than writing
/// bespoke one-off persistence code, so the same tested handler backs both this command and any future
/// automated provisioning tool. <c>BootstrapAdminAccountCommandHandler</c>'s own refuse-if-any-
/// account-exists check is spec 6.1.6's "refuses to run a second time" rule.
/// </para>
/// </remarks>
public static class BootstrapAdminAccountCli
{
    /// <summary>The subcommand name recognised as <c>args[0]</c>.</summary>
    public const string CommandName = "bootstrap-admin";

    /// <summary>Runs the bootstrap command. Returns the process exit code.</summary>
    /// <param name="args">Full process arguments, including the leading <see cref="CommandName"/>.</param>
    /// <param name="sender">Resolved from the composition root's own <c>ServiceProvider</c>.</param>
    public static async Task<int> RunAsync(string[] args, ISender sender)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(sender);

        var options = ParseArguments(args);

        if (options is null)
        {
            await Console.Error.WriteLineAsync(
                "Usage: dotnet run -- bootstrap-admin --email=<email> --staff-name=\"<Two Words>\"");
            return 1;
        }

        var result = await sender.SendAsync(
            new BootstrapAdminAccountCommand(options.Value.Email, options.Value.StaffName),
            CancellationToken.None);

        if (result.IsFailure)
        {
            await Console.Error.WriteLineAsync($"Bootstrap failed: {result.Error.Description}");
            return 1;
        }

        // Second-pass review LOW 9, ACCEPTED AND RECORDED, not fixed here: under `kubectl run`,
        // `docker run`, or a CI job, stdout is routinely captured by the platform's own log
        // collector — meaning this plaintext password can end up sitting in a log store, which is
        // exactly what CLAUDE.md §6 ("no password in a log line") forbids in the HTTP surface. There
        // is no HTTP surface here to apply that rule to; printing the password IS the delivery
        // mechanism spec 6.1.6 asks for ("displayed once on screen with a copy button" is the UI
        // equivalent this console command has no UI to offer). Inventing an alternative delivery
        // channel (email, a one-time secret store, etc.) is a product decision, not something to
        // improvise in this card — left as accepted, tracked drift.
        await Console.Out.WriteLineAsync("Administrator account created.");
        await Console.Out.WriteLineAsync($"  Email:              {result.Value.Email}");
        await Console.Out.WriteLineAsync($"  Temporary password: {result.Value.TemporaryPassword}");
        await Console.Out.WriteLineAsync(
            "This password is shown ONCE and is not stored anywhere in plaintext. Write it down now.");

        return 0;
    }

    private static (string Email, string StaffName)? ParseArguments(string[] args)
    {
        string? email = null;
        string? staffName = null;

        // args[0] is the "bootstrap-admin" verb itself.
        foreach (var argument in args.Skip(1))
        {
            var separatorIndex = argument.IndexOf('=', StringComparison.Ordinal);

            if (!argument.StartsWith("--", StringComparison.Ordinal) || separatorIndex < 0)
            {
                continue;
            }

            var key = argument[2..separatorIndex];
            var value = argument[(separatorIndex + 1)..];

            switch (key)
            {
                case "email":
                    email = value;
                    break;
                case "staff-name":
                    staffName = value;
                    break;
            }
        }

        return email is null || staffName is null ? null : (email, staffName);
    }
}
