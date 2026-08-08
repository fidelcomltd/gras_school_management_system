using Microsoft.Extensions.Time.Testing;
using SchoolManagement.Application.Reference.Ping;

namespace SchoolManagement.UnitTests.Application;

/// <summary>
/// REFERENCE TEST — the template for a handler unit test. Copy this shape.
/// </summary>
/// <remarks>
/// Note what it does NOT do: no <c>WebApplicationFactory</c>, no database, no DI container, no mocking
/// framework for the clock. A handler is a plain class, so a unit test constructs it directly. If a
/// handler is hard to test this way, it has too many dependencies.
/// </remarks>
public sealed class PingQueryHandlerTests
{
    /// <summary>
    /// A fixed instant. Tests must never depend on the real clock: <c>FakeTimeProvider</c> is what makes
    /// this assertion exact instead of approximate, and what stops the suite behaving differently at
    /// midnight or on a leap day.
    /// </summary>
    private static readonly DateTimeOffset FixedNow =
        new(2026, 8, 3, 9, 30, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(FixedNow);

    // Returns the concrete type rather than IRequestHandler<,>: a unit test constructs the handler
    // directly, so there is nothing to gain from the interface and CA1859 flags the indirection.
    private PingQueryHandler CreateHandler() => new(_timeProvider);

    [Fact]
    public async Task HandleAsync_ReturnsSuccess()
    {
        var result = await CreateHandler().HandleAsync(new PingQuery("Ada"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_EchoesTheSuppliedName()
    {
        var result = await CreateHandler().HandleAsync(new PingQuery("Ada"), TestContext.Current.CancellationToken);

        result.Value.Message.ShouldBe("Hello, Ada.");
    }

    [Fact]
    public async Task HandleAsync_UsesTheInjectedClock()
    {
        var result = await CreateHandler().HandleAsync(new PingQuery("Ada"), TestContext.Current.CancellationToken);

        // Exact equality is only possible because the clock is injected. With DateTimeOffset.UtcNow this
        // assertion would have to be a tolerance window, and would still be flaky.
        result.Value.ServerTimeUtc.ShouldBe(FixedNow);
    }

    [Fact]
    public async Task HandleAsync_ReportsTimeInUtc()
    {
        var result = await CreateHandler().HandleAsync(new PingQuery("Ada"), TestContext.Current.CancellationToken);

        result.Value.ServerTimeUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, TestContext.Current.CancellationToken));
    }
}
