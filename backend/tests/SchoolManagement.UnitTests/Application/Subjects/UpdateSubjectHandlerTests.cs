using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="UpdateSubjectHandler"/>: the code-collision edge case (spec 6.6.8: "Two subjects
/// with the same code | Rejected: The code MTH is already used by Mathematics"), and the
/// data-dependent <c>subject.deactivate</c> privilege check on a status change.
/// </summary>
public sealed class UpdateSubjectHandlerTests
{
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly IEffectivePrivilegeProvider _privileges = Substitute.For<IEffectivePrivilegeProvider>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    public UpdateSubjectHandlerTests() => _currentUser.UserId.Returns("admin-1");

    private UpdateSubjectHandler CreateHandler() => new(_subjects, _privileges, _currentUser, _auditSink);

    private void Grant(params string[] privileges) =>
        _privileges.GetGrantsAsync("admin-1", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<PrivilegeGrant>)privileges
                .Select(privilege => new PrivilegeGrant(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), null))
                .ToArray());

    // Spec 6.6.8: "Two subjects with the same code | Rejected: The code MTH is already used by Mathematics."
    [Fact]
    public async Task HandleAsync_ChangingCodeToOneAlreadyUsedByAnotherSubject_IsRejected()
    {
        var mathematics = Subject.Create(Guid.CreateVersion7(), "Mathematics", "MTH", null).Value;
        var english = Subject.Create(Guid.CreateVersion7(), "English Language", null, null).Value;
        _subjects.FindTrackedByIdAsync(english.Id, Arg.Any<CancellationToken>()).Returns(english);
        _subjects.FindByCodeKeyAsync("mth", english.Id, Arg.Any<CancellationToken>()).Returns(mathematics);

        var result = await CreateHandler().HandleAsync(
            new UpdateSubjectCommand(english.Id, null, "MTH", null, null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.code_duplicate");
        result.Error.Description.ShouldContain("MTH");
        result.Error.Description.ShouldContain("Mathematics");
    }

    [Fact]
    public async Task HandleAsync_ChangingStatus_WithoutTheDeactivatePrivilege_IsForbidden()
    {
        var subject = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null).Value;
        _subjects.FindTrackedByIdAsync(subject.Id, Arg.Any<CancellationToken>()).Returns(subject);
        Grant(Privileges.Subject.Update); // Update only — NOT Deactivate.

        var result = await CreateHandler().HandleAsync(
            new UpdateSubjectCommand(subject.Id, null, null, null, SubjectStatus.Inactive), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.deactivate_denied");
        subject.Status.ShouldBe(SubjectStatus.Active);
    }

    [Fact]
    public async Task HandleAsync_ChangingStatus_WithTheDeactivatePrivilege_Succeeds()
    {
        var subject = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null).Value;
        _subjects.FindTrackedByIdAsync(subject.Id, Arg.Any<CancellationToken>()).Returns(subject);
        Grant(Privileges.Subject.Deactivate);

        var result = await CreateHandler().HandleAsync(
            new UpdateSubjectCommand(subject.Id, null, null, null, SubjectStatus.Inactive), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        subject.Status.ShouldBe(SubjectStatus.Inactive);
    }

    // Editing name/code/description WITHOUT touching status never consults the deactivate privilege
    // at all — proven by succeeding with no grants configured.
    [Fact]
    public async Task HandleAsync_EditingFieldsWithoutChangingStatus_NeverRequiresTheDeactivatePrivilege()
    {
        var subject = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null).Value;
        _subjects.FindTrackedByIdAsync(subject.Id, Arg.Any<CancellationToken>()).Returns(subject);

        var result = await CreateHandler().HandleAsync(
            new UpdateSubjectCommand(subject.Id, "Further Mathematics", null, null, null), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        subject.Name.ShouldBe("Further Mathematics");
    }

    [Fact]
    public async Task HandleAsync_WhenTheSubjectDoesNotExist_ReturnsNotFound()
    {
        var id = Guid.CreateVersion7();
        _subjects.FindTrackedByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Subject?)null);

        var result = await CreateHandler().HandleAsync(
            new UpdateSubjectCommand(id, "Name", null, null, null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.not_found");
    }
}
