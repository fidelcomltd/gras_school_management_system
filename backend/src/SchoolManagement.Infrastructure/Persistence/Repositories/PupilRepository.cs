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
        IReadOnlyCollection<Guid>? allowedArmIds,
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

        // TASK-0059: arm-scoped pupil.view restricts to pupils whose OPEN enrolment names one of the
        // caller's granted arms (spec 02 §5.2 — never a pupil.arm_id shortcut). allowedArmIds is
        // null for an unrestricted (school-wide) caller; ListPupilsQueryHandler never passes an
        // empty, non-null collection, so no additional guard is needed here.
        if (allowedArmIds is { Count: > 0 } arms)
        {
            query = query.Where(pupil => context.Enrolments.Any(enrolment =>
                enrolment.PupilId == pupil.Id && enrolment.EffectiveTo == null && arms.Contains(enrolment.ArmId)));
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

        var hasNextPage = rows.Count > pageSize;
        var page = hasNextPage ? rows.GetRange(0, pageSize) : rows;
        var pupilIds = page.ConvertAll(pupil => pupil.Id);

        // TASK-0062: the queue's own columns (spec 6.5.15) — levelAppliedFor, dateApplicationReceived
        // and missing — read the admission record, one small extra query rather than a join, since
        // this page is at most pageSize + 1 rows.
        var admissionsByPupilId = await context.AdmissionRecords
            .AsNoTracking()
            .Where(record => pupilIds.Contains(record.PupilId))
            .Select(record => new
            {
                record.PupilId,
                record.ClassAdmittedInto,
                record.DateApplicationReceived,
                record.AssessmentRequired,
                record.AssessmentResultRemarks,
                record.DeclarationSigned,
            })
            .ToDictionaryAsync(record => record.PupilId, cancellationToken)
            .ConfigureAwait(false);

        // Spec 6.4.2: "The session list is short and always will be" holds for levels too (nine seeded
        // rows, a school adds a handful more) — the whole set is cheaper to load once than to look up
        // per row, the same choice IClassLevelRepository's own remarks make for its callers.
        var levelNamesById = await context.ClassLevels
            .AsNoTracking()
            .Select(level => new { level.Id, level.Name })
            .ToDictionaryAsync(level => level.Id, level => level.Name, cancellationToken)
            .ConfigureAwait(false);

        var items = page.ConvertAll(pupil =>
        {
            string? levelAppliedFor = null;
            DateOnly? dateApplicationReceived = null;
            List<string>? missing = null;

            if (admissionsByPupilId.TryGetValue(pupil.Id, out var admission))
            {
                levelNamesById.TryGetValue(admission.ClassAdmittedInto, out levelAppliedFor);
                dateApplicationReceived = admission.DateApplicationReceived;

                missing = [];

                // Restricted to what sections A and I's stored fields can check (TASK-0062's own
                // recorded gap) — steps 2 to 8 have no entity yet, so nothing below can ever name them.
                if (admission.AssessmentRequired && string.IsNullOrWhiteSpace(admission.AssessmentResultRemarks))
                {
                    missing.Add("Assessment result (Section A)");
                }

                if (!admission.DeclarationSigned)
                {
                    missing.Add("Declaration (Section I)");
                }
            }

            return PupilMapper.ToDto(
                pupil,
                asOfDate,
                levelAppliedFor: levelAppliedFor,
                dateApplicationReceived: dateApplicationReceived,
                missing: missing);
        });

        string? nextCursor = null;

        if (hasNextPage)
        {
            var last = page[^1];
            nextCursor = PupilListCursor.Encode(last.Surname.ToLowerInvariant(), last.Id);
        }

        return new CursorPage<PupilDto>(items, nextCursor);
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

    /// <inheritdoc />
    public Task<int> CountByRegistrationNumberPrefixAsync(string abbreviationPrefix, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(abbreviationPrefix);

        // IgnoreQueryFilters: status-agnostic by design (the port's own remarks) — harmless here
        // regardless, since a Pending row's RegistrationNumber is always null and can never match.
        return context.Pupils
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(pupil => pupil.RegistrationNumber != null && pupil.RegistrationNumber.StartsWith(abbreviationPrefix))
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByRegistrationNumberAsync(
        string registrationNumber, Guid excludingPupilId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationNumber);

        // IgnoreQueryFilters: status-agnostic, matching CountByRegistrationNumberPrefixAsync's own
        // reasoning — a transferred, withdrawn or graduated pupil still holds their number and must
        // still block a collision.
        return context.Pupils
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                pupil => pupil.Id != excludingPupilId && pupil.RegistrationNumber == registrationNumber,
                cancellationToken);
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
