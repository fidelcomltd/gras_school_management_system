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

        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View).ShouldBe(PupilAccessScope.Forbidden);
    }

    [Fact]
    public void Resolve_WithASchoolWideGrant_ReturnsSchoolWide()
    {
        var grants = new[] { new PrivilegeGrant(Privileges.Pupil.View, ScopeType.SchoolWide, new HashSet<Guid>(), null) };

        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View).ShouldBe(PupilAccessScope.SchoolWide);
    }

    [Fact]
    public void Resolve_WithOnlyArmScopedGrants_ReturnsArmRestricted()
    {
        var grants = new[]
        {
            new PrivilegeGrant(Privileges.Pupil.View, ScopeType.ArmList, new HashSet<Guid> { Guid.CreateVersion7() }, null),
        };

        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View).ShouldBe(PupilAccessScope.ArmRestricted);
    }

    [Fact]
    public void Resolve_WithOneArmScopedAndOneSchoolWideGrant_ReturnsSchoolWide()
    {
        var grants = new[]
        {
            new PrivilegeGrant(Privileges.Pupil.View, ScopeType.ArmList, new HashSet<Guid> { Guid.CreateVersion7() }, null),
            new PrivilegeGrant(Privileges.Pupil.View, ScopeType.SchoolWide, new HashSet<Guid>(), null),
        };

        PupilAccessGuard.Resolve(grants, Privileges.Pupil.View).ShouldBe(PupilAccessScope.SchoolWide);
    }

    [Fact]
    public void Resolve_WithNoGrantsAtAll_ReturnsForbidden() =>
        PupilAccessGuard.Resolve([], Privileges.Pupil.Update).ShouldBe(PupilAccessScope.Forbidden);
}
