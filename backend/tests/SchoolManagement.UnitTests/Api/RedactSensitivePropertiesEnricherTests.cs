using SchoolManagement.Api.Observability;
using Serilog.Events;

namespace SchoolManagement.UnitTests.Api;

/// <summary>
/// Tests the log redaction backstop.
/// </summary>
/// <remarks>
/// Worth testing carefully: this is the last thing standing between a careless log statement and a
/// credential in a log store with long retention and wide access.
/// </remarks>
public sealed class RedactSensitivePropertiesEnricherTests
{
    private readonly RedactSensitivePropertiesEnricher _enricher = new();

    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("UserPassword")]
    [InlineData("access_token")]
    [InlineData("ApiKey")]
    [InlineData("Authorization")]
    [InlineData("ConnectionString")]
    [InlineData("CreditCardNumber")]
    [InlineData("Ssn")]
    [InlineData("PrivateKey")]
    public void Enrich_RedactsSensitivePropertyNames(string propertyName)
    {
        var logEvent = CreateLogEvent(propertyName, "the-actual-secret-value");

        _enricher.Enrich(logEvent, new PropertyFactory());

        var value = logEvent.Properties[propertyName].ToString();

        value.ShouldContain(RedactSensitivePropertiesEnricher.RedactedPlaceholder);
        value.ShouldNotContain("the-actual-secret-value");
    }

    [Theory]
    [InlineData("RequestName")]
    [InlineData("ElapsedMilliseconds")]
    [InlineData("StatusCode")]
    [InlineData("TraceId")]
    public void Enrich_LeavesOrdinaryPropertiesAlone(string propertyName)
    {
        // Over-redaction is its own failure: logs that hide the diagnostic fields are useless, and
        // people respond by turning the enricher off.
        var logEvent = CreateLogEvent(propertyName, "ordinary-value");

        _enricher.Enrich(logEvent, new PropertyFactory());

        logEvent.Properties[propertyName].ToString().ShouldContain("ordinary-value");
    }

    [Fact]
    public void Enrich_IsCaseInsensitive()
    {
        var logEvent = CreateLogEvent("SECRET_VALUE", "hunter2");

        _enricher.Enrich(logEvent, new PropertyFactory());

        logEvent.Properties["SECRET_VALUE"].ToString()
            .ShouldContain(RedactSensitivePropertiesEnricher.RedactedPlaceholder);
    }

    private static LogEvent CreateLogEvent(string propertyName, string value) => new(
        DateTimeOffset.UnixEpoch,
        LogEventLevel.Information,
        exception: null,
        new MessageTemplate([]),
        [new LogEventProperty(propertyName, new ScalarValue(value))]);

    /// <summary>Minimal property factory; the enricher only needs it to build replacements.</summary>
    private sealed class PropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(
            string name,
            object? value,
            bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
