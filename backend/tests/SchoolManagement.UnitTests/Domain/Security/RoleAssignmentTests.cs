using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Domain.Security;

/// <summary>Spec 6.1.5's field rules and invariants for <see cref="RoleAssignment"/>.</summary>
public sealed class RoleAssignmentTests
{
    private static readonly Guid AccountId = Guid.CreateVersion7();
    private static readonly Guid RoleId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid GrantedBy = Guid.CreateVersion7();
    private static readonly Guid ArmId = Guid.CreateVersion7();

    [Fact]
    public void Create_SchoolWide_Succeeds()
    {
        var result = RoleAssignment.Create(
            Guid.CreateVersion7(), AccountId, RoleId, SessionId, ScopeType.SchoolWide, [], GrantedBy);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ScopeType.ShouldBe(ScopeType.SchoolWide);
        result.Value.ArmIds.ShouldBeEmpty();
        result.Value.Status.ShouldBe(RoleAssignmentStatus.Active);
    }

    [Fact]
    public void Create_ArmList_WithArms_Succeeds()
    {
        var result = RoleAssignment.Create(
            Guid.CreateVersion7(), AccountId, RoleId, SessionId, ScopeType.ArmList, [ArmId], GrantedBy);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ArmIds.ShouldBe([ArmId]);
    }

    [Fact]
    public void Create_ArmList_WithNoArms_Fails()
    {
        var result = RoleAssignment.Create(
            Guid.CreateVersion7(), AccountId, RoleId, SessionId, ScopeType.ArmList, [], GrantedBy);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role_assignment.arm_ids_required");
    }

    [Fact]
    public void Create_SchoolWide_WithArms_Fails()
    {
        var result = RoleAssignment.Create(
            Guid.CreateVersion7(), AccountId, RoleId, SessionId, ScopeType.SchoolWide, [ArmId], GrantedBy);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role_assignment.arm_ids_not_allowed");
    }

    [Fact]
    public void Create_WithoutASession_Fails()
    {
        var result = RoleAssignment.Create(
            Guid.CreateVersion7(), AccountId, RoleId, sessionId: null, ScopeType.SchoolWide, [], GrantedBy);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role_assignment.session_id_required");
    }

    [Fact]
    public void Create_SuperAdminAssignment_WithASession_Fails()
    {
        var result = RoleAssignment.Create(
            Guid.CreateVersion7(),
            AccountId,
            RoleId,
            SessionId,
            ScopeType.SchoolWide,
            [],
            GrantedBy,
            isSuperAdminAssignment: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role_assignment.super_admin_session_must_be_null");
    }

    [Fact]
    public void Create_SuperAdminAssignment_WithoutASession_Succeeds()
    {
        var result = RoleAssignment.Create(
            Guid.CreateVersion7(),
            AccountId,
            RoleId,
            sessionId: null,
            ScopeType.SchoolWide,
            [],
            GrantedBy,
            isSuperAdminAssignment: true);

        result.IsSuccess.ShouldBeTrue();
        result.Value.SessionId.ShouldBeNull();
    }

    [Fact]
    public void Create_DuplicateArmIds_AreDeduplicated()
    {
        var result = RoleAssignment.Create(
            Guid.CreateVersion7(), AccountId, RoleId, SessionId, ScopeType.ArmList, [ArmId, ArmId], GrantedBy);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ArmIds.ShouldBe([ArmId]);
    }

    [Fact]
    public void Create_WithAnEmptyId_Fails()
    {
        var result = RoleAssignment.Create(
            Guid.Empty, AccountId, RoleId, SessionId, ScopeType.SchoolWide, [], GrantedBy);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role_assignment.id_required");
    }

    [Fact]
    public void Revoke_MovesToRevoked()
    {
        var assignment = RoleAssignment.Create(
            Guid.CreateVersion7(), AccountId, RoleId, SessionId, ScopeType.SchoolWide, [], GrantedBy).Value;

        assignment.Revoke();

        assignment.Status.ShouldBe(RoleAssignmentStatus.Revoked);
    }
}
