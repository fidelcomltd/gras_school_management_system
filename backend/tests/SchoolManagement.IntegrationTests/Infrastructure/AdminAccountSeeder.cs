using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Seeds an <see cref="AdminAccount"/> directly through the DbContext — standing in for the
/// off-the-wire bootstrap seam (spec 6.1.6), which the auth integration tests must not call over
/// HTTP because no such endpoint exists.
/// </summary>
internal static class AdminAccountSeeder
{
    /// <summary>The plaintext password every seeded account is created with.</summary>
    public const string Password = "Correct-Horse-Battery-9";

    /// <summary>Seeds a super-admin account and returns its id and email.</summary>
    /// <param name="fixture">The shared fixture.</param>
    /// <param name="mustChangePassword">
    /// Whether the seeded account should still require a password change. <c>CreateBootstrapSuperAdmin</c>
    /// always starts <c>true</c>; passing <c>false</c> here immediately clears it via
    /// <c>ChangePassword</c> (re-hashing the SAME password), matching an account that has already
    /// completed its forced first change.
    /// </param>
    /// <param name="email">The account's email, or a fresh unique one when omitted.</param>
    /// <param name="seedPassword">The plaintext to hash, when a test needs a specific value (e.g. a
    /// distinctive marker string for a log-redaction assertion) rather than the shared <see cref="Password"/>.</param>
    public static async Task<(Guid AccountId, string Email)> SeedAsync(
        ApiTestFixture fixture,
        bool mustChangePassword = false,
        string? email = null,
        string? seedPassword = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var resolvedEmail = email ?? $"admin-{Guid.NewGuid():N}@example.com";
        var passwordHash = hasher.Hash(seedPassword ?? Password);

        var creation = AdminAccount.CreateBootstrapSuperAdmin(
            Guid.CreateVersion7(),
            resolvedEmail,
            "Seeded Admin",
            passwordHash);

        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        var account = creation.Value;

        if (!mustChangePassword)
        {
            account.ChangePassword(passwordHash);
        }

        context.Add(account);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (account.Id, account.Email);
    }
}
