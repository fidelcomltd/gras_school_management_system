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

        var descriptor = services.Last(service => service.ServiceType == typeof(ISchoolImageStore));

        return descriptor.ImplementationType!;
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
    public void NoCredentialsAndNoFlag_FallsBackToTheFake_AndTheValidatorRefusesTheBoot()
    {
        // Registration cannot throw (it also runs under build-time OpenAPI generation), so the
        // refusal is the validator's job — see CloudinaryOptionsValidatorTests. What matters here
        // is that nothing tries to reach Cloudinary without credentials.
        ResolveStoreImplementation().Name.ShouldBe("InMemorySchoolImageStore");
    }
}
