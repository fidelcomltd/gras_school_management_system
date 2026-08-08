using SchoolManagement.Api.Configuration;

namespace SchoolManagement.UnitTests.Api;

/// <summary>
/// Tests the CORS allow-list validation. Security-relevant: a wildcard origin here would expose every
/// endpoint to every website, so the rejection is worth an explicit test rather than trust.
/// </summary>
public sealed class CorsOptionsValidatorTests
{
    private readonly CorsOptionsValidator _validator = new();

    private static CorsOptions CreateOptions(bool allowCredentials, params string[] origins)
    {
        var options = new CorsOptions { AllowCredentials = allowCredentials };

        foreach (var origin in origins)
        {
            options.AllowedOrigins.Add(origin);
        }

        return options;
    }

    [Fact]
    public void Validate_AcceptsExplicitHttpsOrigins()
    {
        var options = CreateOptions(true, "https://app.example.com", "https://admin.example.com:8443");

        _validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AcceptsLocalhostForDevelopment()
    {
        var options = CreateOptions(true, "http://localhost:5173");

        _validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    public void Validate_RejectsWildcardOrigins(string origin)
    {
        var result = _validator.Validate(null, CreateOptions(true, origin));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Wildcard");
    }

    [Fact]
    public void Validate_RejectsAnOriginWithAPath()
    {
        // An "origin" with a path is a misunderstanding of the spec, and the browser will never match
        // it — producing a CORS failure that looks like a server bug.
        var result = _validator.Validate(null, CreateOptions(true, "https://app.example.com/api"));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("must not include a path");
    }

    [Theory]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("not a url")]
    public void Validate_RejectsANonHttpAbsoluteUrl(string origin)
    {
        _validator.Validate(null, CreateOptions(true, origin)).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsCredentialsWithNoOrigins()
    {
        // Catches the half-configured state: credentials enabled but no allow-list, which cannot work
        // and would otherwise only be discovered from the browser console.
        var result = _validator.Validate(null, CreateOptions(allowCredentials: true));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("explicit origin list");
    }

    [Fact]
    public void Validate_AllowsNoOriginsWhenCredentialsAreDisabled()
    {
        // The correct configuration for a same-origin-only deployment.
        _validator.Validate(null, CreateOptions(allowCredentials: false)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsAnOutOfRangePreflightMaxAge()
    {
        var options = CreateOptions(false);
        options.PreflightMaxAgeSeconds = -1;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }
}
