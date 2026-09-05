using Microsoft.Extensions.Options;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Security;

namespace SchoolManagement.UnitTests.Api;

/// <summary>
/// Tests the CORS allow-list validation. Security-relevant: a wildcard origin here would expose every
/// endpoint to every website, so the rejection is worth an explicit test rather than trust. Also
/// tests the cross-check against <see cref="ApiAuthenticationOptions"/> added by second-pass review
/// HIGH 3: CORS credentials mode and the cookie session scheme must agree, mechanically, not just by
/// a reviewer reading both files.
/// </summary>
public sealed class CorsOptionsValidatorTests
{
    private static readonly CorsOptionsValidator CookieSessionValidator = CreateValidator(AuthenticationModes.CookieSession);
    private static readonly CorsOptionsValidator PlaceholderValidator = CreateValidator(AuthenticationModes.Placeholder);

    private static CorsOptionsValidator CreateValidator(string mode) =>
        new(Options.Create(new ApiAuthenticationOptions { Mode = mode }));

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

        CookieSessionValidator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AcceptsLocalhostForDevelopment()
    {
        var options = CreateOptions(true, "http://localhost:5173");

        CookieSessionValidator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    public void Validate_RejectsWildcardOrigins(string origin)
    {
        var result = CookieSessionValidator.Validate(null, CreateOptions(true, origin));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Wildcard");
    }

    [Fact]
    public void Validate_RejectsAnOriginWithAPath()
    {
        // An "origin" with a path is a misunderstanding of the spec, and the browser will never match
        // it — producing a CORS failure that looks like a server bug.
        var result = CookieSessionValidator.Validate(null, CreateOptions(true, "https://app.example.com/api"));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("must not include a path");
    }

    [Theory]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("not a url")]
    public void Validate_RejectsANonHttpAbsoluteUrl(string origin)
    {
        CookieSessionValidator.Validate(null, CreateOptions(true, origin)).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsCredentialsWithNoOrigins()
    {
        // Catches the half-configured state: credentials enabled but no allow-list, which cannot work
        // and would otherwise only be discovered from the browser console.
        var result = CookieSessionValidator.Validate(null, CreateOptions(allowCredentials: true));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("explicit origin list");
    }

    [Fact]
    public void Validate_AllowsNoOriginsWhenCredentialsAreDisabled_AndTheSchemeIsNotCookieSession()
    {
        // The correct configuration for a same-origin-only deployment that is not (yet) using the
        // cookie session scheme.
        PlaceholderValidator.Validate(null, CreateOptions(allowCredentials: false)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsAnOutOfRangePreflightMaxAge()
    {
        var options = CreateOptions(false);
        options.PreflightMaxAgeSeconds = -1;

        PlaceholderValidator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsCredentialsDisabled_WhileTheCookieSessionSchemeIsActive()
    {
        // Second-pass review HIGH 3: the shipped appsettings.json once contradicted CLAUDE.md §5 this
        // exact way (AllowCredentials: false while Authentication:Mode was CookieSession). No origins
        // configured at all — this must fail regardless, since the incoherence is about credentials
        // mode versus auth scheme, not about the origin list being present.
        var result = CookieSessionValidator.Validate(null, CreateOptions(allowCredentials: false));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CookieSession");
    }

    [Fact]
    public void Validate_AcceptsCredentialsEnabled_WhileTheCookieSessionSchemeIsActive_WithAnOrigin()
    {
        var result = CookieSessionValidator.Validate(null, CreateOptions(true, "https://app.example.com"));

        result.Succeeded.ShouldBeTrue();
    }
}
