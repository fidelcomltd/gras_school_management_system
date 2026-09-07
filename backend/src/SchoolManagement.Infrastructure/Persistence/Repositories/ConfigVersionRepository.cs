using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IConfigVersionRepository"/>.</summary>
internal sealed class ConfigVersionRepository(ApplicationDbContext context) : IConfigVersionRepository
{
    /// <inheritdoc />
    public Task AddAsync(ConfigVersion configVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configVersion);
        cancellationToken.ThrowIfCancellationRequested();

        context.ConfigVersions.Add(configVersion);

        // No SaveChangesAsync — the unit-of-work behaviour commits the transaction.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<CursorPage<ConfigVersionSummaryDto>> ListAsync(
        long? beforeVersionNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = context.ConfigVersions.AsNoTracking().AsQueryable();

        if (beforeVersionNumber is { } cursor)
        {
            query = query.Where(version => version.VersionNumber < cursor);
        }

        // Take one extra row to learn whether a further page exists, without a second COUNT query —
        // the standard cursor-pagination technique, and the reason this never falls back to Skip.
        var rows = await query
            .OrderByDescending(version => version.VersionNumber)
            .Take(pageSize + 1)
            .Select(version => new
            {
                version.Id,
                version.VersionNumber,
                version.ChangedGroup,
                version.ActorAdminId,
                version.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var page = hasMore ? rows.GetRange(0, pageSize) : rows;

        var items = page.ConvertAll(row => new ConfigVersionSummaryDto(
            row.Id.ToString("D", CultureInfo.InvariantCulture),
            row.VersionNumber,
            row.ChangedGroup.ToString(),
            row.ActorAdminId,
            row.CreatedAtUtc));

        var nextCursor = hasMore ? OpaqueCursor.Encode(page[^1].VersionNumber) : null;

        return new CursorPage<ConfigVersionSummaryDto>(items, nextCursor);
    }

    /// <inheritdoc />
    public async Task<ConfigVersionDetailDto?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await context.ConfigVersions
            .AsNoTracking()
            .Where(version => version.Id == id)
            .Select(version => new
            {
                version.Id,
                version.VersionNumber,
                version.ChangedGroup,
                version.ActorAdminId,
                version.Reason,
                version.CreatedAtUtc,
                version.SnapshotJson,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new ConfigVersionDetailDto(
            row.Id.ToString("D", CultureInfo.InvariantCulture),
            row.VersionNumber,
            row.ChangedGroup.ToString(),
            row.ActorAdminId,
            row.Reason,
            row.CreatedAtUtc,
            JsonSerializer.Deserialize<JsonElement>(row.SnapshotJson));
    }
}
