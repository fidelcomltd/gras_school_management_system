using Microsoft.Extensions.Configuration;
using SchoolManagement.Api.Endpoints;

namespace SchoolManagement.UnitTests.Api.Endpoints;

/// <summary>
/// /health/live is anonymous, so the deployed commit it reports must be a git SHA and nothing else.
/// </summary>
public sealed class DeployedCommitTests
{
    private const string Sha = "3036cf0b1d2e4f5a6b7c8d9e0f1a2b3c4d5e6f70";

    [Theory]
    [InlineData(Sha, Sha)]
    [InlineData(" 3036CF0B1D2E4F5A6B7C8D9E0F1A2B3C4D5E6F70 ", Sha)]
    public void AFullSha_IsReportedLowerCase(string configured, string expected) =>
        HealthEndpoints.DeployedCommit(Configuration(configured)).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3036cf0")] // short: would never match the full SHA it is compared with
    [InlineData("some other value entirely, e.g. a secret put in the wrong variable")]
    [InlineData("3036cf0b1d2e4f5a6b7c8d9e0f1a2b3c4d5e6f70zz")]
    public void AnythingElse_IsReportedAsUnknown(string? configured) =>
        HealthEndpoints.DeployedCommit(Configuration(configured)).ShouldBe(HealthEndpoints.UnknownCommit);

    private static IConfiguration Configuration(string? commit) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [HealthEndpoints.DeployedCommitKey] = commit })
            .Build();
}
