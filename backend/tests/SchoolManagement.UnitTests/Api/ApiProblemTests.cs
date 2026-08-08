using Microsoft.AspNetCore.Http;
using SchoolManagement.Api.Http;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.UnitTests.Api;

/// <summary>
/// Tests the single error-category to status-code mapping.
/// </summary>
public sealed class ApiProblemTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 422)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unauthenticated, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.Failure, 500)]
    public void ToStatusCode_MapsEachCategory(ErrorType errorType, int expectedStatusCode)
    {
        ApiProblem.ToStatusCode(errorType).ShouldBe(expectedStatusCode);
    }

    [Fact]
    public void EveryErrorTypeIsExplicitlyMapped()
    {
        // THE POINT OF THIS FILE. Adding a member to ErrorType without extending the mapping would
        // silently fall through to 500, so a genuine 404 would be reported to clients as a server
        // error. This test enumerates the enum, so it fails the moment a member is added.
        var unmapped = Enum.GetValues<ErrorType>()
            .Where(errorType => ApiProblem.ToStatusCode(errorType) == StatusCodes.Status500InternalServerError)
            .Where(errorType => errorType != ErrorType.Failure)
            .ToArray();

        unmapped.ShouldBeEmpty(
            "Every ErrorType must map to a deliberate status code. Unmapped members fall through to " +
            $"500. Add these to ApiProblem.ToStatusCode: {string.Join(", ", unmapped)}");
    }

    [Fact]
    public void EveryErrorTypeHasATitle()
    {
        var untitled = Enum.GetValues<ErrorType>()
            .Where(errorType => string.IsNullOrWhiteSpace(ApiProblem.ToTitle(errorType)))
            .ToArray();

        untitled.ShouldBeEmpty($"Missing ProblemDetails titles for: {string.Join(", ", untitled)}");
    }

    [Fact]
    public void ToTypeUrn_ProducesAStableUriForAnErrorCode()
    {
        var urn = ApiProblem.ToTypeUrn("sample_record.label_taken");

        urn.ShouldBe("urn:schoolmanagement:error:sample_record.label_taken");

        // It must be a valid URI reference, since RFC 9457 requires that of the `type` member.
        Uri.TryCreate(urn, UriKind.Absolute, out _).ShouldBeTrue();
    }
}
