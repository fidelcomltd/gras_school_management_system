using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SchoolManagement.Api.Security;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Settings;

namespace SchoolManagement.UnitTests.Api;

/// <summary>
/// Tests the startup refusals for a deployed environment.
/// </summary>
/// <remarks>
/// Added for the <c>Cloudinary:AllowInMemoryStore</c> case, which is the one refusal here that
/// prevents SILENT DATA LOSS rather than an obvious outage: that flag makes the in-memory image
/// store win even when real credentials are configured (deliberately — see the guard's own
/// remarks), it ships as <c>true</c> in the Development template, and a host that carried it over
/// would boot green, accept the school's logo, and lose it on the next restart with nothing in the
/// log. The other two refusals were previously covered only by integration tests.
/// </remarks>
public sealed class StartupEnvironmentGuardTests
{
    private static StartupEnvironmentGuard CreateGuard(
        string environmentName,
        bool allowInMemoryStore = false,
        bool sensitiveDataLogging = false,
        string mode = AuthenticationModes.CookieSession) =>
        new(
            new StubHostEnvironment(environmentName),
            Options.Create(new ApiAuthenticationOptions { Mode = mode }),
            Options.Create(new DatabaseOptions { EnableSensitiveDataLogging = sensitiveDataLogging }),
            Options.Create(new CloudinaryOptions { AllowInMemoryStore = allowInMemoryStore }));

    [Fact]
    public async Task StartAsync_RefusesTheInMemoryImageStoreInADeployedEnvironment()
    {
        var guard = CreateGuard(Environments.Production, allowInMemoryStore: true);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => guard.StartAsync(CancellationToken.None));

        exception.Message.ShouldContain(nameof(CloudinaryOptions.AllowInMemoryStore));
        exception.Message.ShouldContain("lost on every restart");
    }

    [Fact]
    public async Task StartAsync_AllowsTheInMemoryImageStoreInDevelopment()
    {
        // Development is where the flag belongs: it is how a local run and the integration fixture
        // keep uploads off the network.
        var guard = CreateGuard(Environments.Development, allowInMemoryStore: true);

        await guard.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_AcceptsAProperlyConfiguredDeployedEnvironment()
    {
        var guard = CreateGuard(Environments.Production);

        await guard.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ReportsEveryProblemAtOnce()
    {
        // One boot, one list: an operator fixing a misconfigured host should not have to discover
        // the next problem by restarting.
        var guard = CreateGuard(
            Environments.Production,
            allowInMemoryStore: true,
            sensitiveDataLogging: true,
            mode: AuthenticationModes.Placeholder);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => guard.StartAsync(CancellationToken.None));

        exception.Message.ShouldContain(nameof(CloudinaryOptions.AllowInMemoryStore));
        exception.Message.ShouldContain(nameof(DatabaseOptions.EnableSensitiveDataLogging));
        exception.Message.ShouldContain("Placeholder");
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "SchoolManagement.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
