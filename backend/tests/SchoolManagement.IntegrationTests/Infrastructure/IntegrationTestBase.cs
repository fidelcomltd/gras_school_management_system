using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests. Handles the skip-when-no-database decision and per-test isolation.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public abstract class IntegrationTestBase(ApiTestFixture fixture) : IAsyncLifetime
{
    /// <summary>JSON options matching the API's wire format, so deserialisation in tests behaves like a client's.</summary>
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The shared application fixture.</summary>
    protected ApiTestFixture Fixture { get; } = fixture;

    /// <summary>An HTTP client for the running application. Created only when a database is available.</summary>
    protected HttpClient Client { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (!Fixture.IsDatabaseAvailable)
        {
            // Nothing to set up; each test's RequireDatabase() call reports the skip.
            return;
        }

        await Fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        Client = Fixture.CreateClient();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Client?.Dispose();

        // xunit's IAsyncLifetime derives from IAsyncDisposable, so this is the dispose pattern and
        // CA1816 applies: suppressing finalization here means a derived test class that introduces a
        // finalizer does not have to re-implement disposal to get it suppressed.
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Skips the current test, with an actionable reason, when no PostgreSQL is available.
    /// </summary>
    /// <remarks>
    /// Called explicitly at the top of every test rather than relying on a skip thrown from
    /// <see cref="InitializeAsync"/>. Being explicit makes the dependency visible in the test body and
    /// keeps the skip reporting behaviour independent of test-framework lifecycle details.
    /// <para>
    /// It SKIPS — it never passes and never falls back to a weaker database. See
    /// <see cref="DatabaseAvailability"/> for why that distinction matters.
    /// </para>
    /// </remarks>
    protected void RequireDatabase()
    {
        if (!Fixture.IsDatabaseAvailable)
        {
            Assert.Skip(Fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    /// <summary>Reads a JSON response body into <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The expected shape. A reference type — every contract DTO is a record class.</typeparam>
    /// <param name="response">The response to read.</param>
    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(response);

        var value = await response.Content.ReadFromJsonAsync<T>(
            JsonOptions,
            TestContext.Current.CancellationToken);

        value.ShouldNotBeNull(
            $"Expected a {typeof(T).Name} body but the response was empty. Status: {response.StatusCode}.");

        return value;
    }

    /// <summary>Reads a response body as a JSON document, for asserting on ProblemDetails extensions.</summary>
    /// <param name="response">The response to read.</param>
    protected static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(json);
    }
}
