using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Reference.SampleRecords;
using SchoolManagement.Domain.Reference;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// REFERENCE SCAFFOLD — EF Core implementation of <see cref="ISampleRecordRepository"/>.
/// </summary>
/// <remarks>
/// <para>
/// THE READ-PATH CONVENTIONS DEMONSTRATED HERE ARE MANDATORY FOR EVERY REPOSITORY:
/// </para>
/// <list type="bullet">
/// <item><c>AsNoTracking()</c> on every read. A tracked read pays for change-tracking snapshots of
/// data nobody will modify, and keeps it alive in the context for the rest of the request.</item>
/// <item>Project columns in the QUERY, so the database returns only the columns needed. Loading whole
/// entities and mapping them afterwards moves every column over the wire, and it is the reason
/// "select *" performance problems appear as the table grows.</item>
/// <item>A TOTAL ORDER before <c>Skip</c>/<c>Take</c>. PostgreSQL gives no ordering guarantee without
/// <c>ORDER BY</c>, so paging over a non-deterministic order can return the same row twice and skip
/// another entirely.</item>
/// <item>Pass the <c>CancellationToken</c> to every async EF call, so an abandoned request stops
/// costing database time.</item>
/// </list>
/// <para>
/// MULTI-COLLECTION INCLUDES: this entity has no navigations, but when one does, an
/// <c>Include</c> of two or more collections produces a cartesian explosion in a single query. Use
/// <c>AsSplitQuery()</c> there, and be aware it means several round trips that are not in one
/// snapshot unless you are inside a transaction. Projection usually beats both.
/// </para>
/// <para>
/// COMPILED QUERIES are deliberately not used here. They pay off for a hot query executed on a tight
/// loop, and cost readability everywhere else; adding one to a reference slice would advertise it as
/// the default. Reach for <c>EF.CompileAsyncQuery</c> only with a measurement that justifies it.
/// </para>
/// </remarks>
internal sealed class SampleRecordRepository(ApplicationDbContext context) : ISampleRecordRepository
{
    /// <inheritdoc />
    public Task AddAsync(SampleRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        // Add, not AddAsync: EF Core's async overload exists only for value generators that need a
        // database round trip to produce a key (HiLo/sequences). This entity's key is supplied by the
        // application, so the async version would add an await that never yields.
        context.SampleRecords.Add(record);

        // No SaveChangesAsync — the unit-of-work behaviour commits the transaction.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> LabelExistsAsync(string label, CancellationToken cancellationToken) =>
        context.SampleRecords
            .AsNoTracking()
            // The global query filter already restricts this to live rows, so a soft-deleted record
            // does not block reuse of its label — matching the filtered unique index on the table.
            .AnyAsync(record => record.Label == label, cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<SampleRecordDto>> ListAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        // Defence in depth: validation already rejected an oversized page, but clamping here means no
        // code path can issue an unbounded query even if it bypassed the pipeline.
        var safePage = page.Clamp();

        var query = context.SampleRecords.AsNoTracking();

        // An exact count is a second query and, on a very large table, a full index scan. It is worth
        // it while the API needs a page count; if this table ever grows past a few million rows,
        // switch the envelope to cursor pagination rather than making the count approximate.
        var totalCount = await query.LongCountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(record => record.CreatedAtUtc)
            // Tie-break on the key so the order is TOTAL. Two rows created in the same transaction
            // share a timestamp, and without this they could swap places between pages.
            .ThenByDescending(record => record.Id)
            .Skip(safePage.Skip)
            .Take(safePage.PageSize)
            // Projected to an anonymous type first so the SELECT lists only these columns. The DTO
            // needs Id as a string, and Guid.ToString() has no SQL translation — formatting it here
            // would push the whole projection into memory and defeat the point.
            .Select(record => new
            {
                record.Id,
                record.Label,
                record.Note,
                record.CreatedAtUtc,
                record.ModifiedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var items = rows.ConvertAll(row => new SampleRecordDto(
            Id: row.Id.ToString("D", CultureInfo.InvariantCulture),
            Label: row.Label,
            Note: row.Note,
            CreatedAtUtc: row.CreatedAtUtc,
            ModifiedAtUtc: row.ModifiedAtUtc));

        return new PagedResult<SampleRecordDto>(items, safePage.Page, safePage.PageSize, totalCount);
    }
}
