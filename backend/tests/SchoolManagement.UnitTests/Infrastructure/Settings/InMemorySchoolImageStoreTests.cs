using SchoolManagement.Infrastructure.Settings;

namespace SchoolManagement.UnitTests.Infrastructure.Settings;

/// <summary>Tests <see cref="InMemorySchoolImageStore"/> (TASK-0005b stage B1).</summary>
public sealed class InMemorySchoolImageStoreTests
{
    private readonly InMemorySchoolImageStore _store = new();

    [Fact]
    public async Task PutAsync_ThenOpenAsync_RoundTripsTheExactBytes()
    {
        byte[] original = [1, 2, 3, 4, 5, 250, 251, 252];

        var assetId = await _store.PutAsync(original, "image/png", CancellationToken.None);

        using var stream = await _store.OpenAsync(assetId, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, CancellationToken.None);

        buffer.ToArray().ShouldBe(original);
    }

    [Fact]
    public async Task PutAsync_CalledTwice_ReturnsTwoDifferentAssetIds()
    {
        byte[] bytes = [9, 9, 9];

        var first = await _store.PutAsync(bytes, "image/jpeg", CancellationToken.None);
        var second = await _store.PutAsync(bytes, "image/jpeg", CancellationToken.None);

        // Amendment 4 / the orchestrator design: assets are immutable, and every upload gets a NEW
        // id even when the bytes are identical to a previous upload — nothing is ever overwritten.
        first.ShouldNotBe(second);
    }

    [Fact]
    public async Task OpenAsync_WithAnIdThatWasNeverStored_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _store.OpenAsync(Guid.CreateVersion7().ToString(), CancellationToken.None));
    }

    [Fact]
    public async Task PutAsync_AnOlderAsset_IsStillOpenableAfterANewerUploadReplacesIt()
    {
        // Nothing is ever deleted — a publication snapshot must be able to keep reading an asset
        // that is no longer "current" (04-module-school-settings.md line 77).
        var firstId = await _store.PutAsync(new byte[] { 1 }, "image/png", CancellationToken.None);
        var secondId = await _store.PutAsync(new byte[] { 2 }, "image/png", CancellationToken.None);

        using var firstStream = await _store.OpenAsync(firstId, CancellationToken.None);
        using var firstBuffer = new MemoryStream();
        await firstStream.CopyToAsync(firstBuffer, CancellationToken.None);

        firstBuffer.ToArray().ShouldBe([1]);
        secondId.ShouldNotBe(firstId);
    }
}
