using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="ConfigVersion.Create"/> (spec 6.2.9).</summary>
public sealed class ConfigVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_CarriesEveryFieldSupplied()
    {
        var id = Guid.CreateVersion7();

        var version = ConfigVersion.Create(id, "{\"schoolProfile\":{}}", ConfigVersionGroup.Identity, "admin-1", "a reason", Now);

        version.Id.ShouldBe(id);
        version.SnapshotJson.ShouldBe("{\"schoolProfile\":{}}");
        version.ChangedGroup.ShouldBe(ConfigVersionGroup.Identity);
        version.ActorAdminId.ShouldBe("admin-1");
        version.Reason.ShouldBe("a reason");
        version.CreatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Create_ANotYetInsertedVersion_HasNoVersionNumberYet()
    {
        // VersionNumber is database-generated (a real Postgres identity column, per the entity's
        // remarks) — an entity built by Create but never added to a context genuinely has none yet.
        var version = ConfigVersion.Create(
            Guid.CreateVersion7(), "{}", ConfigVersionGroup.Identity, actorAdminId: null, reason: null, Now);

        version.VersionNumber.ShouldBe(0);
    }

    [Fact]
    public void Create_AllowsANullActorAndNullReason_ForASystemActionWithNoReasonRequirement()
    {
        var version = ConfigVersion.Create(
            Guid.CreateVersion7(), "{}", ConfigVersionGroup.Identity, actorAdminId: null, reason: null, Now);

        version.ActorAdminId.ShouldBeNull();
        version.Reason.ShouldBeNull();
    }
}
