using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Application.Pupils;

/// <summary>Pure resolution logic behind <c>ListPupils</c>/<c>GetPupil</c>/<c>UpdatePupilBiographical</c>'s scope handling.</summary>
public sealed class PupilAccessGuardTests
{
    [Fact]
    public void Resolve_WithNoMatchingGrant_ReturnsForbidden()
    {
        var grants = new[] { new PrivilegeGrant(Privileges.Admin.View, ScopeType.SchoolWide, new HashSet<Guid>(), null) };

        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View, targetSessionId: null).ShouldBe(PupilAccessScope.Forbidden);
    }

    [Fact]
    public void Resolve_WithASchoolWideGrant_ReturnsSchoolWide()
    {
        var grants = new[] { new PrivilegeGrant(Privileges.Pupil.View, ScopeType.SchoolWide, new HashSet<Guid>(), null) };

        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View, targetSessionId: null).ShouldBe(PupilAccessScope.SchoolWide);
    }

    [Fact]
    public void Resolve_WithOnlyArmScopedGrants_ReturnsArmRestricted()
    {
        var grants = new[]
        {
            new PrivilegeGrant(Privileges.Pupil.View, ScopeType.ArmList, new HashSet<Guid> { Guid.CreateVersion7() }, null),
        };

        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View, targetSessionId: null).ShouldBe(PupilAccessScope.ArmRestricted);
    }

    [Fact]
    public void Resolve_WithOneArmScopedAndOneSchoolWideGrant_ReturnsSchoolWide()
    {
        var grants = new[]
        {
            new PrivilegeGrant(Privileges.Pupil.View, ScopeType.ArmList, new HashSet<Guid> { Guid.CreateVersion7() }, null),
            new PrivilegeGrant(Privileges.Pupil.View, ScopeType.SchoolWide, new HashSet<Guid>(), null),
        };

        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View, targetSessionId: null).ShouldBe(PupilAccessScope.SchoolWide);
    }

    [Fact]
    public void Resolve_CountsOnlyGrantsInTheTargetsSession()
    {
        // TASK-0060: last session's school-wide grant does not reach this session's pupils; this session's arm list does.
        var lastSession = Guid.CreateVersion7();
        var thisSession = Guid.CreateVersion7();
        var arm = Guid.CreateVersion7();
        var grants = new[]
        {
            new PrivilegeGrant(Privileges.Pupil.View, ScopeType.SchoolWide, new HashSet<Guid>(), lastSession),
            new PrivilegeGrant(Privileges.Pupil.View, ScopeType.ArmList, new HashSet<Guid> { arm }, thisSession),
        };

        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View, thisSession).ShouldBe(PupilAccessScope.ArmRestricted);
        PupilAccessGuard.ResolveArmIds(grants, Privileges.Pupil.View, thisSession).ShouldBe([arm]);
        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View, lastSession).ShouldBe(PupilAccessScope.SchoolWide);
        PupilAccessGuard.ResolveArmIds(grants, Privileges.Pupil.View, lastSession).ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_WithNoGrantsAtAll_ReturnsForbidden() =>
        PupilAccessGuard.Resolve([], Privileges.Pupil.Update, targetSessionId: null).ShouldBe(PupilAccessScope.Forbidden);
}
