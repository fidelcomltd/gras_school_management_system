using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="DeleteSubjectExceptionHandler"/> — TASK-0070 delta amendment 2: this route is
/// gated by <c>subject.map.arm</c> at the endpoint (proven at the pipeline/authorization layer, not
/// here), and the handler itself carries no closed-session check — spec 6.6.8's closed-session
/// rejection is stated for creation only, and the handler's own remarks record that asymmetry
/// deliberately rather than resolving it silently.
/// </summary>
public sealed class DeleteSubjectExceptionHandlerTests
{
    private readonly ISubjectMappingExceptionRepository _exceptions = Substitute.For<ISubjectMappingExceptionRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    private DeleteSubjectExceptionHandler CreateHandler() => new(_exceptions, _currentUser, _auditSink);

    [Fact]
    public async Task HandleAsync_WhenTheExceptionDoesNotExist_ReturnsNotFound()
    {
        var id = Guid.CreateVersion7();
        _exceptions.FindTrackedByIdAsync(id, Arg.Any<CancellationToken>()).Returns((SubjectMappingException?)null);

        var result = await CreateHandler().HandleAsync(new DeleteSubjectExceptionCommand(id), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.not_found");
    }

    [Fact]
    public async Task HandleAsync_WhenTheExceptionExists_RemovesItAndAuditsUnderMapArm()
    {
        var exception = SubjectMappingException.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            SubjectExceptionMode.Include, "A reason").Value;
        _exceptions.FindTrackedByIdAsync(exception.Id, Arg.Any<CancellationToken>()).Returns(exception);
        _currentUser.UserId.Returns("admin-1");

        var result = await CreateHandler().HandleAsync(new DeleteSubjectExceptionCommand(exception.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _exceptions.Received(1).RemoveAsync(exception, Arg.Any<CancellationToken>());
        await _auditSink.Received(1).RecordAsync(
            Privileges.Subject.MapArm,
            "subject_mapping_exception",
            exception.Id.ToString(),
            Arg.Any<IReadOnlyDictionary<string, object?>?>(),
            "admin-1",
            Arg.Any<CancellationToken>());
    }
}
