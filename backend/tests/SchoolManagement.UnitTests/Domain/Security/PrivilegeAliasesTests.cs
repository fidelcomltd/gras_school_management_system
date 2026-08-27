using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Domain.Security;

/// <summary>
/// Spec <c>02-data-model.md</c> §5.5 item 1: <c>guardian.*</c> is aliased to <c>contact.*</c>
/// rather than broken.
/// </summary>
public sealed class PrivilegeAliasesTests
{
    [Fact]
    public void GuardianView_ResolvesToContactView()
    {
        PrivilegeAliases.Resolve("guardian.view").ShouldBe(Privileges.Contact.View);
    }

    [Fact]
    public void GuardianCreate_ResolvesToContactCreate()
    {
        PrivilegeAliases.Resolve("guardian.create").ShouldBe(Privileges.Contact.Create);
    }

    [Fact]
    public void GuardianUpdate_ResolvesToContactUpdate()
    {
        PrivilegeAliases.Resolve("guardian.update").ShouldBe(Privileges.Contact.Update);
    }

    [Fact]
    public void ACanonicalCode_ResolvesToItself()
    {
        PrivilegeAliases.Resolve(Privileges.Contact.View).ShouldBe(Privileges.Contact.View);
    }

    [Fact]
    public void AnUnrelatedCode_IsReturnedUnchanged()
    {
        PrivilegeAliases.Resolve("result.view").ShouldBe("result.view");
    }
}
