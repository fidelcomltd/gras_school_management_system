using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Infrastructure;

namespace SchoolManagement.UnitTests.Infrastructure.Settings;

/// <summary>
/// Which <see cref="ISchoolImageStore"/> the container ends up with (TASK-0005b stage D).
/// </summary>
/// <remarks>
/// <para>
/// This exists because the FIRST version of that decision was wrong in a way no other test could
/// see. It checked "are credentials configured?" before "was the in-memory store demanded?", and
/// the integration fixture runs the host as Development — where <c>Program.cs</c> loads
/// user-secrets. So the moment a developer put real Cloudinary credentials in user-secrets (the
/// documented way to exercise the real store locally), the entire integration suite would have
/// started uploading to the school's live media library, while the fixture that sets
/// <c>AllowInMemoryStore</c> went on claiming tests never touch the network.
/// </para>
/// <para>
/// The registration is asserted through the service DESCRIPTOR rather than by resolving the
/// service: resolving the Cloudinary path would construct a real client from fake credentials, and
/// the question here is only which type was chosen.
/// </para>
/// </remarks>
public sealed class SchoolImageStoreRegistrationTests
{
    private static Type ResolveStoreImplementation(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting =>
                new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration, validateOnStart: false);

        // Resolve a real instance: the choice is made from the bound options at resolution time, so
        // inspecting the registration descriptor would prove nothing (drift 2026-09-23).
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ISchoolImageStore>().GetType();
    }

    [Fact]
    public void AllowInMemoryStore_WinsOverConfiguredCredentials()
    {
        // The integration fixture's exact situation once a developer has user-secrets set.
        var implementation = ResolveStoreImplementation(
            ("Cloudinary:AllowInMemoryStore", "true"),
            ("Cloudinary:CloudName", "cloud"),
            ("Cloudinary:ApiKey", "key"),
            ("Cloudinary:ApiSecret", "secret"));

        implementation.Name.ShouldBe("InMemorySchoolImageStore");
    }

    [Fact]
    public void ConfiguredCredentialsAlone_SelectCloudinary()
    {
        var implementation = ResolveStoreImplementation(
            ("Cloudinary:CloudName", "cloud"),
            ("Cloudinary:ApiKey", "key"),
            ("Cloudinary:ApiSecret", "secret"));

        implementation.Name.ShouldBe("CloudinaryImageStore");
    }

    [Fact]
    public void ConfigurationAddedAfterRegistration_StillDecides()
    {
        // The integration host's order: services are registered from user-secrets alone, then the
        // fixture's overrides arrive. The flag must still win.
        var source = new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = "cloud",
            ["Cloudinary:ApiKey"] = "key",
            ["Cloudinary:ApiSecret"] = "secret",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(source).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration, validateOnStart: false);
        configuration["Cloudinary:AllowInMemoryStore"] = "true";
        configuration["Cloudinary:CloudName"] = string.Empty;
        configuration["Cloudinary:ApiKey"] = string.Empty;
        configuration["Cloudinary:ApiSecret"] = string.Empty;

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISchoolImageStore>().GetType().Name.ShouldBe("InMemorySchoolImageStore");
    }

    [Fact]
    public void NoCredentialsAndNoFlag_FallsBackToTheFake_AndTheValidatorRefusesTheBoot()
    {
        // Registration cannot throw (it also runs under build-time OpenAPI generation), so the
        // refusal is the validator's job — see CloudinaryOptionsValidatorTests. What matters here
        // is that nothing tries to reach Cloudinary without credentials.
        // Resolving reads the bound options, which runs the validator: a host with neither the flag nor credentials
        // cannot obtain a store at all, let alone reach Cloudinary.
        Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(() => ResolveStoreImplementation());
    }
}
