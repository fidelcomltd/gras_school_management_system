using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;
using SchoolImageSizeVariant = SchoolManagement.Application.Abstractions.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// <see cref="UploadSchoolLogoCommandHandler"/>, <see cref="UploadSchoolSignatureCommandHandler"/>
/// and the shared <see cref="SchoolImageUploadSupport"/> (TASK-0005b stage B2). The processor and
/// the store are both fakes here — this suite is about the upload/repoint/audit SEQUENCE, not image
/// processing (covered by <c>SkiaSchoolImageProcessorTests</c>) or storage (covered by
/// <c>InMemorySchoolImageStoreTests</c>).
/// </summary>
public sealed class UploadSchoolImageHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly ISchoolImageProcessor _processor = Substitute.For<ISchoolImageProcessor>();
    private readonly ISchoolImageStore _store = Substitute.For<ISchoolImageStore>();
    private readonly ISchoolImageRepository _schoolImages = Substitute.For<ISchoolImageRepository>();
    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly IAdminAccountRepository _accounts = Substitute.For<IAdminAccountRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    private UploadSchoolLogoCommandHandler CreateLogoHandler() => new(
        _processor, _store, _schoolImages, _schoolProfileRepository, _accounts, _currentUser, _auditSink, _timeProvider);

    private UploadSchoolSignatureCommandHandler CreateSignatureHandler() => new(
        _processor, _store, _schoolImages, _schoolProfileRepository, _accounts, _currentUser, _auditSink, _timeProvider);

    // --- Happy path --------------------------------------------------------------------------------

    [Fact]
    public async Task Logo_HappyPath_StoresEveryRenditionUnderOneGroupIdAndRepointsTheProfile()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _processor.ProcessLogo(Arg.Any<byte[]>()).Returns(Result.Success(ThreeRenditions()));
        _store.PutAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => $"asset-{Guid.NewGuid()}");
        SetActor("admin-1", staffName: null);

        IReadOnlyList<SchoolImage>? storedImages = null;
        _schoolImages
            .AddRangeAsync(Arg.Do<IReadOnlyList<SchoolImage>>(images => storedImages = images), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await CreateLogoHandler().HandleAsync(
            new UploadSchoolLogoCommand(new byte[] { 1, 2, 3 }), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        // Every rendition was pushed through the store.
        await _store.Received(3).PutAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // All three rows share exactly one upload group id.
        storedImages.ShouldNotBeNull();
        storedImages!.Count.ShouldBe(3);
        var groupIds = storedImages.Select(image => image.UploadGroupId).Distinct().ToList();
        groupIds.Count.ShouldBe(1);

        // The profile now points at that same group.
        profile.CurrentLogoGroupId.ShouldBe(groupIds[0]);
    }

    [Fact]
    public async Task Signature_HappyPath_RepointsTheSignatureNotTheLogo()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        var existingLogoGroupId = Guid.CreateVersion7();
        profile.SetCurrentLogo(existingLogoGroupId);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _processor.ProcessSignature(Arg.Any<byte[]>()).Returns(Result.Success(OneRendition()));
        _store.PutAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("asset-1");
        SetActor("admin-1", staffName: null);

        var result = await CreateSignatureHandler().HandleAsync(
            new UploadSchoolSignatureCommand(new byte[] { 9, 9, 9 }), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        profile.CurrentSignatureGroupId.ShouldNotBeNull();
        profile.CurrentLogoGroupId.ShouldBe(existingLogoGroupId); // Untouched by a signature upload.
    }

    [Fact]
    public async Task HappyPath_NeverDeletesTheReplacedAsset()
    {
        // There is no delete method on ISchoolImageStore (assets are immutable, orchestrator design)
        // — the only way this test CAN be about "the old asset is never deleted" is to prove the
        // upload sequence writes only new rows and never removes or updates an existing one.
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        var previousGroupId = Guid.CreateVersion7();
        profile.SetCurrentLogo(previousGroupId);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _processor.ProcessLogo(Arg.Any<byte[]>()).Returns(Result.Success(ThreeRenditions()));
        _store.PutAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("asset-new");
        SetActor("admin-1", staffName: null);

        await CreateLogoHandler().HandleAsync(
            new UploadSchoolLogoCommand(new byte[] { 1 }), TestContext.Current.CancellationToken);

        // The repository is only ever asked to ADD rows — it has no update/delete method to call in
        // the first place, and this asserts the handler never tries to reach for one via any other
        // path (for example by re-fetching and re-saving the previous group).
        await _schoolImages.Received(1).AddRangeAsync(Arg.Any<IReadOnlyList<SchoolImage>>(), Arg.Any<CancellationToken>());
        profile.CurrentLogoGroupId.ShouldNotBe(previousGroupId);
    }

    // --- Processor failure ---------------------------------------------------------------------

    [Fact]
    public async Task Logo_WhenTheProcessorFails_PassesTheFailureThroughAndTouchesNothingElse()
    {
        var processorError = Error.Validation(SchoolImageErrorCodes.TooSmall, "Logo must be at least 300 by 300 pixels.");
        _processor.ProcessLogo(Arg.Any<byte[]>()).Returns(Result.Failure<ProcessedSchoolImage>(processorError));

        var result = await CreateLogoHandler().HandleAsync(
            new UploadSchoolLogoCommand(new byte[] { 1 }), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SchoolImageErrorCodes.TooSmall);

        // Nothing downstream of the processor is ever touched on a failure.
        var cancellationToken = TestContext.Current.CancellationToken;
        await _schoolProfileRepository.DidNotReceiveWithAnyArgs().GetTrackedSingletonAsync(cancellationToken);
        await _store.DidNotReceiveWithAnyArgs().PutAsync(default, default!, cancellationToken);
        await _schoolImages.DidNotReceiveWithAnyArgs().AddRangeAsync(default!, cancellationToken);
        await _auditSink.DidNotReceiveWithAnyArgs().RecordAsync(
            default!, default, default, default, default, cancellationToken, default, default);
    }

    [Fact]
    public async Task Signature_WhenTheProcessorFails_PassesTheFailureThrough()
    {
        var processorError = Error.Validation(SchoolImageErrorCodes.UnsupportedType, "Not a PNG or JPEG.");
        _processor.ProcessSignature(Arg.Any<byte[]>()).Returns(Result.Failure<ProcessedSchoolImage>(processorError));

        var result = await CreateSignatureHandler().HandleAsync(
            new UploadSchoolSignatureCommand(new byte[] { 1 }), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SchoolImageErrorCodes.UnsupportedType);
    }

    // --- Audit ---------------------------------------------------------------------------------

    [Fact]
    public async Task HappyPath_AuditsWithTheGroupIdAndNeverAnyBytes()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _processor.ProcessLogo(Arg.Any<byte[]>()).Returns(Result.Success(ThreeRenditions()));
        _store.PutAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("asset-1");
        SetActor("admin-1", staffName: null);

        IReadOnlyDictionary<string, object?>? capturedMetadata = null;
        await _auditSink.RecordAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Do<IReadOnlyDictionary<string, object?>?>(metadata => capturedMetadata = metadata),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyDictionary<string, object?>?>());

        await CreateLogoHandler().HandleAsync(
            new UploadSchoolLogoCommand(new byte[] { 1 }), TestContext.Current.CancellationToken);

        await _auditSink.Received(1).RecordAsync(
            "settings.identity.logo_uploaded",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyDictionary<string, object?>?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyDictionary<string, object?>?>());

        capturedMetadata.ShouldNotBeNull();
        capturedMetadata!.Count.ShouldBe(1);
        capturedMetadata.ShouldContainKey("uploadGroupId");
        capturedMetadata["uploadGroupId"].ShouldBeOfType<Guid>();

        // The only values in the metadata dictionary are the group id — no byte array, no base64
        // string, nothing that could be the uploaded image's content.
        capturedMetadata.Values.ShouldAllBe(value => value is Guid);
    }

    [Fact]
    public async Task SecondUpload_RecordsThePreviousGroupIdAsTheBeforeValue()
    {
        var previousGroupId = Guid.CreateVersion7();
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        profile.SetCurrentLogo(previousGroupId);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _processor.ProcessLogo(Arg.Any<byte[]>()).Returns(Result.Success(ThreeRenditions()));
        _store.PutAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("asset-1");
        SetActor("admin-1", staffName: null);

        await CreateLogoHandler().HandleAsync(
            new UploadSchoolLogoCommand(new byte[] { 1 }), TestContext.Current.CancellationToken);

        await _auditSink.Received(1).RecordAsync(
            "settings.identity.logo_uploaded",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<IReadOnlyDictionary<string, object?>?>(metadata =>
                metadata != null && (Guid)metadata["uploadGroupId"]! != previousGroupId),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>(),
            Arg.Is<IReadOnlyDictionary<string, object?>?>(before =>
                before != null && (Guid)before["uploadGroupId"]! == previousGroupId));
    }

    [Fact]
    public async Task FirstUpload_RecordsNoBeforeValue()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7()); // CurrentLogoGroupId is null.
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _processor.ProcessLogo(Arg.Any<byte[]>()).Returns(Result.Success(ThreeRenditions()));
        _store.PutAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("asset-1");
        SetActor("admin-1", staffName: null);

        await CreateLogoHandler().HandleAsync(
            new UploadSchoolLogoCommand(new byte[] { 1 }), TestContext.Current.CancellationToken);

        await _auditSink.Received(1).RecordAsync(
            "settings.identity.logo_uploaded",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyDictionary<string, object?>?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>(),
            beforeMetadata: null);
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private void SetActor(string adminId, string? staffName)
    {
        _currentUser.UserId.Returns(adminId);

        if (staffName is not null && Guid.TryParse(adminId, out var parsedId))
        {
            var account = AdminAccount.Create(parsedId, "actor@example.com", staffName, "08012345678", "hash").Value;
            _accounts.FindReadOnlyByIdAsync(parsedId, Arg.Any<CancellationToken>()).Returns(account);
        }
    }

    private static ProcessedSchoolImage ThreeRenditions() => new(
    [
        new SchoolImageRendition(SchoolImageSizeVariant.Original, new byte[] { 1, 2, 3 }, 320, 320, "image/png"),
        new SchoolImageRendition(SchoolImageSizeVariant.Size200, new byte[] { 4, 5, 6 }, 200, 200, "image/png"),
        new SchoolImageRendition(SchoolImageSizeVariant.Size64, new byte[] { 7, 8, 9 }, 64, 64, "image/png"),
    ]);

    private static ProcessedSchoolImage OneRendition() => new(
    [
        new SchoolImageRendition(SchoolImageSizeVariant.Original, new byte[] { 1, 2, 3 }, 600, 200, "image/png"),
    ]);
}
