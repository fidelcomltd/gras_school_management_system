using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Domain.Security;

/// <summary>Spec 6.1.4's field rules and invariants for <see cref="Role"/>.</summary>
public sealed class RoleTests
{
    private static readonly Guid FixedId = Guid.Parse("0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40");

    [Fact]
    public void Create_WithValidFields_Succeeds()
    {
        var result = Role.Create(FixedId, "Class Teacher", "Enters marks.", [Privileges.Pupil.View]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Class Teacher");
        result.Value.NameKey.ShouldBe("class teacher");
        result.Value.Description.ShouldBe("Enters marks.");
        result.Value.IsSystem.ShouldBeFalse();
        result.Value.Status.ShouldBe(RoleStatus.Active);
        result.Value.Privileges.ShouldBe([Privileges.Pupil.View]);
    }

    [Fact]
    public void Create_WithAnEmptyId_Fails()
    {
        var result = Role.Create(Guid.Empty, "Class Teacher", null, [Privileges.Pupil.View]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.id_required");
    }

    [Fact]
    public void Create_WithAnEmptyName_FailsValidation()
    {
        var result = Role.Create(FixedId, "   ", null, [Privileges.Pupil.View]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.name_required");
    }

    [Fact]
    public void Create_WithANameLongerThanSixtyCharacters_FailsValidation()
    {
        var name = new string('a', Role.NameMaxLength + 1);

        var result = Role.Create(FixedId, name, null, [Privileges.Pupil.View]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.name_too_long");
    }

    [Theory]
    [InlineData("Super Admin")]
    [InlineData("super admin")]
    [InlineData("SUPER ADMIN")]
    [InlineData("  Super Admin  ")]
    public void Create_WithTheReservedNameCaseInsensitive_Rejects(string name)
    {
        var result = Role.Create(FixedId, name, null, [Privileges.Pupil.View]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.name_reserved");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithADescriptionLongerThanThreeHundredCharacters_FailsValidation()
    {
        var description = new string('a', Role.DescriptionMaxLength + 1);

        var result = Role.Create(FixedId, "Class Teacher", description, [Privileges.Pupil.View]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.description_too_long");
    }

    [Fact]
    public void Create_WithANullDescription_Succeeds()
    {
        var result = Role.Create(FixedId, "Class Teacher", null, [Privileges.Pupil.View]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBeNull();
    }

    [Fact]
    public void Create_WithAWhitespaceOnlyDescription_NormalizesToNull()
    {
        var result = Role.Create(FixedId, "Class Teacher", "   ", [Privileges.Pupil.View]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBeNull();
    }

    [Fact]
    public void Create_WithNoPrivileges_FailsValidation()
    {
        var result = Role.Create(FixedId, "Class Teacher", null, []);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.privileges_empty");
    }

    [Fact]
    public void Create_WithAnUnknownPrivilegeCode_FailsNamingTheOffender()
    {
        var result = Role.Create(FixedId, "Class Teacher", null, ["not.a.real.code"]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.unknown_privilege");
        result.Error.Description.ShouldBe("'not.a.real.code' is not a recognised privilege code.");
    }

    [Fact]
    public void Create_WithSeveralUnknownPrivilegeCodes_NamesAllOfThemSortedAndDeduplicated()
    {
        var result = Role.Create(FixedId, "Class Teacher", null, ["zzz.fake", "aaa.fake", "zzz.fake"]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.unknown_privilege");
        result.Error.Description.ShouldBe("'aaa.fake, zzz.fake' are not recognised privilege codes.");
    }

    [Fact]
    public void Create_ResolvesAGuardianAliasToItsCanonicalCode()
    {
        var result = Role.Create(FixedId, "Class Teacher", null, ["guardian.view"]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Privileges.ShouldBe([Privileges.Contact.View]);
    }

    [Fact]
    public void Create_DeduplicatesAndSortsPrivilegesDeterministically()
    {
        var result = Role.Create(
            FixedId,
            "Class Teacher",
            null,
            [Privileges.Results.ScoreEnter, Privileges.Admin.Create, Privileges.Results.ScoreEnter]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Privileges.ShouldBe([Privileges.Admin.Create, Privileges.Results.ScoreEnter]);
    }

    [Fact]
    public void CreateSystemRole_WithTheReservedName_Succeeds()
    {
        var result = Role.CreateSystemRole(FixedId, "Super Admin", "Everything.", [Privileges.Admin.View]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsSystem.ShouldBeTrue();
        result.Value.Name.ShouldBe("Super Admin");
    }

    [Fact]
    public void CreateSystemRole_WithAnyOtherName_Fails()
    {
        var result = Role.CreateSystemRole(FixedId, "Not Super Admin", null, [Privileges.Admin.View]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.system_role_name_invalid");
    }

    [Fact]
    public void Rename_WithValidFields_UpdatesNameNameKeyAndDescription()
    {
        var role = Role.Create(FixedId, "Class Teacher", "Old.", [Privileges.Pupil.View]).Value;

        var result = role.Rename("Senior Class Teacher", "New.");

        result.IsSuccess.ShouldBeTrue();
        role.Name.ShouldBe("Senior Class Teacher");
        role.NameKey.ShouldBe("senior class teacher");
        role.Description.ShouldBe("New.");
    }

    [Fact]
    public void Rename_ToTheReservedName_Fails()
    {
        var role = Role.Create(FixedId, "Class Teacher", null, [Privileges.Pupil.View]).Value;

        var result = role.Rename("Super Admin", null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.name_reserved");
        // The rejected rename must not have partially applied.
        role.Name.ShouldBe("Class Teacher");
    }

    [Fact]
    public void SetPrivileges_WithAValidNonEmptySet_ReplacesTheWholeSet()
    {
        var role = Role.Create(FixedId, "Class Teacher", null, [Privileges.Pupil.View]).Value;

        var result = role.SetPrivileges([Privileges.Results.ScoreEnter]);

        result.IsSuccess.ShouldBeTrue();
        role.Privileges.ShouldBe([Privileges.Results.ScoreEnter]);
    }

    [Fact]
    public void SetPrivileges_WithAnEmptySet_FailsAndLeavesThePreviousSetIntact()
    {
        var role = Role.Create(FixedId, "Class Teacher", null, [Privileges.Pupil.View]).Value;

        var result = role.SetPrivileges([]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.privileges_empty");
        role.Privileges.ShouldBe([Privileges.Pupil.View]);
    }

    [Fact]
    public void ChangeStatus_MovesBetweenActiveAndArchived()
    {
        var role = Role.Create(FixedId, "Class Teacher", null, [Privileges.Pupil.View]).Value;

        role.ChangeStatus(RoleStatus.Archived);

        role.Status.ShouldBe(RoleStatus.Archived);
    }
}
