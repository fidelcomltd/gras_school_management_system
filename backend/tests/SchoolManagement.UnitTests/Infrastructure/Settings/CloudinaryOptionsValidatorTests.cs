using SchoolManagement.Infrastructure.Settings;

namespace SchoolManagement.UnitTests.Infrastructure.Settings;

/// <summary>
/// Tests <see cref="CloudinaryOptionsValidator"/> (TASK-0005b stage D). The case that matters most
/// is the HALF-configured one: it reads as "Cloudinary is set up" while the DI registration falls
/// back to the in-memory store, so the school's logo would survive right up until the next restart.
/// </summary>
public sealed class CloudinaryOptionsValidatorTests
{
    private static readonly CloudinaryOptionsValidator Validator = new();

    private static CloudinaryOptions Configured() => new()
    {
        CloudName = "cloud",
        ApiKey = "key",
        ApiSecret = "secret",
        FolderPrefix = "gras/prod",
    };

    [Fact]
    public void Validate_AcceptsAFullSetOfCredentials()
    {
        Validator.Validate(null, Configured()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AcceptsNoCredentialsWhenTheInMemoryStoreIsAllowed()
    {
        var options = new CloudinaryOptions { AllowInMemoryStore = true };

        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsNoCredentialsInADeployedEnvironment()
    {
        var result = Validator.Validate(null, new CloudinaryOptions());

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Cloudinary__CloudName");
    }

    [Fact]
    public void Validate_RejectsHalfConfiguredCredentials()
    {
        var options = new CloudinaryOptions { AllowInMemoryStore = true, CloudName = "cloud" };

        var result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("partially configured");
    }

    [Theory]
    [InlineData("/gras")]
    [InlineData("gras/")]
    [InlineData("")]
    public void Validate_RejectsAMalformedFolderPrefix(string prefix)
    {
        var options = Configured();
        options.FolderPrefix = prefix;

        Validator.Validate(null, options).Failed.ShouldBeTrue();
    }
}
