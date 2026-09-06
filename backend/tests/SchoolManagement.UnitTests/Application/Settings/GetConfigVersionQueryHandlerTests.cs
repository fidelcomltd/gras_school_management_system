using System.Text.Json;
using NSubstitute;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="GetConfigVersionQueryHandler"/>.</summary>
public sealed class GetConfigVersionQueryHandlerTests
{
    private readonly IConfigVersionRepository _repository = Substitute.For<IConfigVersionRepository>();

    private GetConfigVersionQueryHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task HandleAsync_WhenFound_ReturnsTheDetail()
    {
        var id = Guid.CreateVersion7();
        var detail = new ConfigVersionDetailDto(
            id.ToString(),
            3,
            "Identity",
            "admin-1",
            null,
            DateTimeOffset.UtcNow,
            JsonSerializer.Deserialize<JsonElement>("{}"));
        _repository.FindReadOnlyByIdAsync(id, Arg.Any<CancellationToken>()).Returns(detail);

        var result = await CreateHandler().HandleAsync(new GetConfigVersionQuery(id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.VersionNumber.ShouldBe(3);
    }

    [Fact]
    public async Task HandleAsync_WhenNotFound_Returns404()
    {
        var id = Guid.CreateVersion7();
        _repository.FindReadOnlyByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ConfigVersionDetailDto?)null);

        var result = await CreateHandler().HandleAsync(new GetConfigVersionQuery(id), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("config_version.not_found");
    }
}
