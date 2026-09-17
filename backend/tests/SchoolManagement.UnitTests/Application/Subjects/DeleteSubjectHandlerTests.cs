using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="DeleteSubjectHandler"/> — spec 6.6.8: "Subject deleted | Permitted only where it
/// has never been mapped and never scored. Otherwise deactivate."
/// </summary>
public sealed class DeleteSubjectHandlerTests
{
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly ISubjectMappingRepository _mappings = Substitute.For<ISubjectMappingRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    private DeleteSubjectHandler CreateHandler() => new(_subjects, _mappings, _currentUser, _auditSink);

    [Fact]
    public async Task HandleAsync_WhenTheSubjectDoesNotExist_ReturnsNotFound()
    {
        var id = Guid.CreateVersion7();
        _subjects.FindTrackedByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Subject?)null);

        var result = await CreateHandler().HandleAsync(new DeleteSubjectCommand(id), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.not_found");
    }

    [Fact]
    public async Task HandleAsync_WhenTheSubjectHasEverBeenMapped_IsRejected()
    {
        var subject = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null).Value;
        _subjects.FindTrackedByIdAsync(subject.Id, Arg.Any<CancellationToken>()).Returns(subject);
        _mappings.AnyEverForSubjectAsync(subject.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().HandleAsync(new DeleteSubjectCommand(subject.Id), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.referenced");
        result.Error.Description.ShouldContain("Mathematics");
        await _subjects.DidNotReceive().RemoveAsync(Arg.Any<Subject>(), Arg.Any<CancellationToken>());
    }

    // Positive control: a subject never mapped is deletable. Without this, a bug that always
    // rejected deletion would still pass the test above.
    [Fact]
    public async Task HandleAsync_WhenTheSubjectHasNeverBeenMapped_Succeeds()
    {
        var subject = Subject.Create(Guid.CreateVersion7(), "Unused Subject", null, null).Value;
        _subjects.FindTrackedByIdAsync(subject.Id, Arg.Any<CancellationToken>()).Returns(subject);
        _mappings.AnyEverForSubjectAsync(subject.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().HandleAsync(new DeleteSubjectCommand(subject.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _subjects.Received(1).RemoveAsync(subject, Arg.Any<CancellationToken>());
    }
}
