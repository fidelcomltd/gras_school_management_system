using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="SchoolImage.Create"/> (TASK-0005b stage B1; spec 9.6).</summary>
public sealed class SchoolImageTests
{
    [Fact]
    public void Create_SetsEveryFieldExactlyAsGiven()
    {
        var id = Guid.CreateVersion7();
        var uploadGroupId = Guid.CreateVersion7();

        var image = SchoolImage.Create(
            id,
            SchoolImageKind.Logo,
            SchoolImageSizeVariant.Size200,
            uploadGroupId,
            "asset-123",
            widthPixels: 200,
            heightPixels: 160,
            contentType: "image/png");

        image.Id.ShouldBe(id);
        image.Kind.ShouldBe(SchoolImageKind.Logo);
        image.SizeVariant.ShouldBe(SchoolImageSizeVariant.Size200);
        image.UploadGroupId.ShouldBe(uploadGroupId);
        image.AssetId.ShouldBe("asset-123");
        image.WidthPixels.ShouldBe(200);
        image.HeightPixels.ShouldBe(160);
        image.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public void Create_ForASignature_UsesTheOriginalSizeVariant()
    {
        // A signature only ever produces one rendition (ISchoolImageProcessor.ProcessSignature) —
        // this proves the entity itself places no restriction on Kind/SizeVariant combinations
        // beyond what the caller passes, matching "the domain trusts its inputs" elsewhere.
        var image = SchoolImage.Create(
            Guid.CreateVersion7(),
            SchoolImageKind.Signature,
            SchoolImageSizeVariant.Original,
            Guid.CreateVersion7(),
            "asset-456",
            widthPixels: 600,
            heightPixels: 200,
            contentType: "image/jpeg");

        image.Kind.ShouldBe(SchoolImageKind.Signature);
        image.SizeVariant.ShouldBe(SchoolImageSizeVariant.Original);
    }

    [Fact]
    public void Create_StartsWithNoModificationStamp()
    {
        // Rows are immutable and never updated (see the entity's remarks) — ModifiedAtUtc/ModifiedBy
        // are only ever populated by AuditingInterceptor on an EntityState.Modified save, which
        // production code never causes for this entity.
        var image = SchoolImage.Create(
            Guid.CreateVersion7(),
            SchoolImageKind.Logo,
            SchoolImageSizeVariant.Original,
            Guid.CreateVersion7(),
            "asset-789",
            widthPixels: 320,
            heightPixels: 320,
            contentType: "image/png");

        image.ModifiedAtUtc.ShouldBeNull();
        image.ModifiedBy.ShouldBeNull();
    }
}
