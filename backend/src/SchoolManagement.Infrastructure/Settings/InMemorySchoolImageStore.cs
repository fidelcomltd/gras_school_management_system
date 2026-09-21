using System.Collections.Concurrent;
using SchoolManagement.Application.Abstractions.Settings;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// <see cref="ISchoolImageStore"/> held entirely in process memory — for unit and integration tests
/// (orchestrator design, 2026-09-21: "tests never touch the network"). NOT registered for any
/// deployed environment; stage D's <c>CloudinaryImageStore</c> is the real store, and stage D also
/// owns making DI pick between the two by environment.
/// </summary>
internal sealed class InMemorySchoolImageStore : ISchoolImageStore
{
    private readonly ConcurrentDictionary<string, StoredAsset> _assets = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<string> PutAsync(ReadOnlyMemory<byte> bytes, string contentType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        cancellationToken.ThrowIfCancellationRequested();

        // A real GUID per call, matching the orchestrator design's "every upload gets a new
        // generated public_id" — nothing here is ever looked up by content, only by the id handed
        // back, so two identical uploads get two independent, equally-valid asset ids.
        var assetId = Guid.CreateVersion7().ToString();
        _assets[assetId] = new StoredAsset(bytes.ToArray(), contentType);

        return Task.FromResult(assetId);
    }

    /// <inheritdoc />
    public Task<Stream> OpenAsync(string assetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_assets.TryGetValue(assetId, out var asset))
        {
            throw new InvalidOperationException(
                $"No in-memory asset stored for id '{assetId}'. Assets are never deleted, so this " +
                "id was never returned by PutAsync in the first place.");
        }

        Stream stream = new MemoryStream(asset.Bytes, writable: false);
        return Task.FromResult(stream);
    }

    private sealed record StoredAsset(byte[] Bytes, string ContentType);
}
