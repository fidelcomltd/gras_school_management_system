using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IPupilRepository"/>.</summary>
/// <remarks>
/// Every method here reads through <c>context.Pupils</c> (a plain <c>DbSet&lt;Pupil&gt;</c> LINQ
/// source), never <c>Database.SqlQuery&lt;T&gt;</c> materialising into a non-entity POCO the way
/// <c>AdminAccountRepository.ListAsync</c> does — that choice matters here specifically because EF
/// Core's model-level query filter (<c>PupilConfiguration.HasQueryFilter</c>, the pending-exclusion
/// invariant) is an ENTITY-level concept: it composes automatically onto any LINQ query against
/// <c>context.Pupils</c>, but a raw SQL query materialising into an unmapped row type has no entity
/// context for EF to attach the filter to and would silently bypass it. The composite
/// (surname, id) keyset comparison a raw SQL row-value expression would make trivial is instead
/// written as <c>string.Compare(...)</c>/<c>Guid.CompareTo(...)</c> LINQ, which the Npgsql provider
/// translates to the equivalent SQL comparison — verified by <c>PupilEndpointsTests</c>' pagination
/// cases actually exercising a second page against real Postgres, not assumed.
/// </remarks>
internal sealed class PupilRepository(ApplicationDbContext context) : IPupilRepository
{
    /// <inheritdoc />
    public Task AddAsync(Pupil pupil, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pupil);
        cancellationToken.ThrowIfCancellationRequested();

        context.Pupils.Add(pupil);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Pupil?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Pupils.IgnoreQueryFilters().FirstOrDefaultAsync(pupil => pupil.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Pupil?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Pupils.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(pupil => pupil.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<CursorPage<PupilDto>> ListAsync(
        PupilStatus? status,
        string? search,
        string? cursor,
        int pageSize,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var hasCursor = PupilListCursor.TryDecode(cursor, out var cursorSurnameKey, out var cursorId);

        // status == Pending is the one caller-driven opt-out GET /pupils itself accepts (the
        // contract delta's own "unless status=pending is passed"); anything else — including no
        // status at all — goes through the FILTERED DbSet, so the invariant is enforced by the model
        // rather than repeated here.
        var query = status == PupilStatus.Pending
            ? context.Pupils.IgnoreQueryFilters().Where(pupil => pupil.Status == PupilStatus.Pending)
            : context.Pupils.AsQueryable();

        if (status is { } explicitStatus && explicitStatus != PupilStatus.Pending)
        {
            query = query.Where(pupil => pupil.Status == explicitStatus);
        }

        query = query.AsNoTracking();

        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (term is not null)
        {
            query = query.Where(pupil =>
                EF.Functions.ILike(pupil.Surname, $"%{term}%") ||
                EF.Functions.ILike(pupil.FirstName, $"%{term}%") ||
                (pupil.MiddleName != null && EF.Functions.ILike(pupil.MiddleName, $"%{term}%")) ||
                (pupil.RegistrationNumber != null && EF.Functions.ILike(pupil.RegistrationNumber, $"%{term}%")));
        }

        if (hasCursor)
        {
            query = query.Where(pupil =>
                string.Compare(pupil.Surname.ToLower(), cursorSurnameKey, StringComparison.Ordinal) > 0 ||
                (EF.Functions.ILike(pupil.Surname, cursorSurnameKey) && pupil.Id.CompareTo(cursorId) > 0));
        }

        // Take one extra row to learn whether a further page exists, without a second COUNT query.
        var rows = await query
            .OrderBy(pupil => pupil.Surname.ToLower())
            .ThenBy(pupil => pupil.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToPage(rows, pageSize, term, asOfDate);
    }

    /// <inheritdoc />
    public async Task<CursorPage<PupilDto>> ListAdmissionsQueueAsync(
        string? cursor,
        int pageSize,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var hasCursor = PupilListCursor.TryDecode(cursor, out var cursorSurnameKey, out var cursorId);

        var query = context.Pupils
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(pupil => pupil.Status == PupilStatus.Pending);

        if (hasCursor)
        {
            query = query.Where(pupil =>
                string.Compare(pupil.Surname.ToLower(), cursorSurnameKey, StringComparison.Ordinal) > 0 ||
                (EF.Functions.ILike(pupil.Surname, cursorSurnameKey) && pupil.Id.CompareTo(cursorId) > 0));
        }

        var rows = await query
            .OrderBy(pupil => pupil.Surname.ToLower())
            .ThenBy(pupil => pupil.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToPage(rows, pageSize, searchTerm: null, asOfDate);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PupilDto>> FindDuplicatesAsync(
        string surname,
        string firstName,
        DateOnly dateOfBirth,
        int maxResults,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surname);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);

        var rows = await context.Pupils
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(pupil =>
                EF.Functions.ILike(pupil.Surname, surname) &&
                EF.Functions.ILike(pupil.FirstName, firstName) &&
                pupil.DateOfBirth == dateOfBirth)
            .OrderBy(pupil => pupil.CreatedAtUtc)
            .Take(maxResults)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(pupil => PupilMapper.ToDto(pupil, asOfDate));
    }

    private static CursorPage<PupilDto> ToPage(List<Pupil> rows, int pageSize, string? searchTerm, DateOnly asOfDate)
    {
        var hasNextPage = rows.Count > pageSize;
        var page = hasNextPage ? rows.GetRange(0, pageSize) : rows;

        var items = page
            .ConvertAll(pupil =>
            {
                var matchedField = PupilSearchMatcher.Resolve(
                    pupil.Surname, pupil.FirstName, pupil.MiddleName, pupil.RegistrationNumber, searchTerm);

                return PupilMapper.ToDto(pupil, asOfDate, matchedField);
            });

        string? nextCursor = null;

        if (hasNextPage)
        {
            var last = page[^1];
            nextCursor = PupilListCursor.Encode(last.Surname.ToLowerInvariant(), last.Id);
        }

        return new CursorPage<PupilDto>(items, nextCursor);
    }
}
