using NSubstitute;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>Tests <see cref="GetRemarkTemplatesHandler"/>: per-kind privilege, any-grant included.</summary>
public sealed class GetRemarkTemplatesHandlerTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid ArmId = Guid.CreateVersion7();

    private readonly IRemarkTemplateRepository _templates = Substitute.For<IRemarkTemplateRepository>();
    private readonly IEffectivePrivilegeProvider _grantsProvider = Substitute.For<IEffectivePrivilegeProvider>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    public GetRemarkTemplatesHandlerTests()
    {
        _currentUser.UserId.Returns(UserId.ToString());
        _templates.ListByKindReadOnlyAsync(Arg.Any<RemarkKind>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private GetRemarkTemplatesHandler CreateHandler() => new(_templates, _grantsProvider, _currentUser);

    [Fact]
    public async Task HandleAsync_WhenNotAuthenticated_ReturnsUnauthenticated()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await CreateHandler().HandleAsync(new GetRemarkTemplatesQuery(RemarkKind.ClassTeacher), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("authentication.required");
    }

    [Fact]
    public async Task HandleAsync_WithNoGrantAtAll_Returns403()
    {
        _grantsProvider.GetGrantsAsync(UserId.ToString(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateHandler().HandleAsync(new GetRemarkTemplatesQuery(RemarkKind.ClassTeacher), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.view_forbidden");
    }

    [Fact]
    public async Task HandleAsync_WithAnArmScopedGrantForTheRequestedKind_Succeeds()
    {
        // Ruling T: "the class-teacher list needs result.remark.classteacher at ANY scope, and
        // arm-scoped counts" — the whole point of ScopeResolution.AnyGrant.
        _grantsProvider.GetGrantsAsync(UserId.ToString(), Arg.Any<CancellationToken>())
            .Returns([new PrivilegeGrant(Privileges.Results.RemarkClassTeacher, ScopeType.ArmList, new HashSet<Guid> { ArmId }, SessionId: null)]);

        var result = await CreateHandler().HandleAsync(new GetRemarkTemplatesQuery(RemarkKind.ClassTeacher), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_HoldingOnlyTheOtherKindsPrivilege_Returns403()
    {
        _grantsProvider.GetGrantsAsync(UserId.ToString(), Arg.Any<CancellationToken>())
            .Returns([new PrivilegeGrant(Privileges.Results.RemarkHeadTeacher, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null)]);

        var result = await CreateHandler().HandleAsync(new GetRemarkTemplatesQuery(RemarkKind.ClassTeacher), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.view_forbidden");
    }
}
