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
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Pupils.AnyAsync(pupil => pupil.Id == id, cancellationToken);

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
        var hasCursor = PupilRegisterCursor.TryDecode(
            cursor, out var cursorLevelOrdinal, out var cursorArmKey, out var cursorSurnameKey, out var cursorId);

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

        // TASK-0059: arm-scoped pupil.view restricts to pupils whose OPEN enrolment names one of the
        // caller's granted arms (spec 02 §5.2 — never a pupil.arm_id shortcut). allowedArmIds is
        // null for an unrestricted (school-wide) caller; ListPupilsQueryHandler never passes an
        // empty, non-null collection, so no additional guard is needed here.
        if (allowedArmIds is { Count: > 0 } arms)
        {
            query = query.Where(pupil => context.Enrolments.Any(enrolment =>
                enrolment.PupilId == pupil.Id && enrolment.EffectiveTo == null && arms.Contains(enrolment.ArmId)));
        }

        // TASK-0061 (spec 6.5.15): class-progression order, then arm, then surname, then id. A pupil's
        // OWN row carries no arm/level reference (Enrolment's remarks — never a denormalised shortcut);
        // the level ordinal and arm key are resolved through the pupil's OPEN enrolment, a correlated
        // subquery per field, with the ruling's NON-NULL sentinel
        // (PupilRegisterCursor.UnenrolledLevelOrdinal/UnenrolledArmKey) standing in for a pupil with
        // none — transferred/withdrawn/graduated, or the pending→withdrawn lapsed application of
        // 6.5.14 — so the ordering never carries a NULL. Ruling part 2, decisions/2026-Q3.md
        // 2026-09-15 (TASK-0061).
        //
        // THE TRANSLATION TRAP (found empirically — every shape below was tried and ruled out the same
        // way): Npgsql's EF Core provider refuses to translate `string.Compare`/`string.CompareOrdinal`
        // — needed for the Surname/ArmKey tie-break — the moment ANY other condition (a join, a
        // correlated subquery, even a plain `HashSet<Guid>.Contains(pupil.Id)` or a second, separately
        // chained `.Where()`) sits in the SAME compiled query, reporting
        // "Translation of method 'string.Compare' failed" regardless of nesting depth or which specific
        // extra condition it was. It ONLY succeeds in the exact minimal shape
        // `ListAdmissionsQueueAsync` already uses below (a plain status equality, then a lone
        // string.Compare-or-ILike `.Where()`, nothing else). So the ordinal comparison here runs
        // client-side in .NET, never in SQL — see `ListRegisterAsync` below — while every OTHER piece
        // of the cursor logic (bucket membership, ORDER BY, LIMIT) stays server-side.
        return await ListRegisterAsync(
                query, hasCursor, cursorLevelOrdinal, cursorArmKey, cursorSurnameKey, cursorId, pageSize, term, asOfDate, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CursorPage<PupilDto>> ListRegisterAsync(
        IQueryable<Pupil> query,
        bool hasCursor,
        int cursorLevelOrdinal,
        string cursorArmKey,
        string cursorSurnameKey,
        Guid cursorId,
        int pageSize,
        string? term,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        // Arms+levels: a handful of rows (spec 6.4.2's own "the session list is short and always will
        // be" reasoning — the same one ListAdmissionsQueueAsync already relies on for ClassLevels).
        var armLevelByArmId = await context.Arms
            .AsNoTracking()
            .Select(arm => new { arm.Id, arm.LabelKey, arm.ClassLevelId })
            .Join(
                context.ClassLevels.AsNoTracking().Select(level => new { level.Id, level.ProgressionOrder }),
                arm => arm.ClassLevelId,
                level => level.Id,
                (arm, level) => new { arm.Id, arm.LabelKey, level.ProgressionOrder })
            .ToDictionaryAsync(row => row.Id, row => (row.ProgressionOrder, row.LabelKey), cancellationToken)
            .ConfigureAwait(false);

        static (Pupil Pupil, int LevelOrdinal, string ArmKey) ToRow(Pupil pupil, int levelOrdinal, string armKey) =>
            (pupil, levelOrdinal, armKey);

        if (!hasCursor)
        {
            var firstPageRows = await SelectOrderedByClassKeyAsync(query, pageSize, cancellationToken).ConfigureAwait(false);

            return ToRegisterPage(firstPageRows, pageSize, term, asOfDate);
        }

        // The bucket (level, arm) the cursor's own row sat in, as small in-memory arm-id lists — every
        // pupil enrolled in a STRICTLY LATER bucket qualifies outright, no surname comparison needed; a
        // pupil in the SAME bucket needs the surname/id tie-break (done client-side, see above).
        var armIdsAfterCursorBucket = new List<Guid>();
        var armIdsAtCursorBucket = new List<Guid>();

        foreach (var (armId, classKey) in armLevelByArmId)
        {
            var comparison = classKey.ProgressionOrder != cursorLevelOrdinal
                ? classKey.ProgressionOrder.CompareTo(cursorLevelOrdinal)
                : string.CompareOrdinal(classKey.LabelKey, cursorArmKey);

            if (comparison > 0)
            {
                armIdsAfterCursorBucket.Add(armId);
            }
            else if (comparison == 0)
            {
                armIdsAtCursorBucket.Add(armId);
            }
        }

        // The unenrolled sentinel is the LAST bucket (ruling part 2): every enrolled pupil is "after
        // cursor" only when the cursor itself was not already IN that trailing block, and every
        // unenrolled pupil is in the cursor's own bucket exactly when the cursor was too.
        var cursorIsAtSentinel = cursorLevelOrdinal == PupilRegisterCursor.UnenrolledLevelOrdinal;

        // Group 1: every row in a bucket strictly after the cursor's — qualifies outright, ordered and
        // limited entirely server-side (no string.Compare in this query at all, so LIMIT is real).
        var afterBucketQuery = query.Where(pupil =>
            context.Enrolments.Any(enrolment =>
                enrolment.PupilId == pupil.Id && enrolment.EffectiveTo == null &&
                armIdsAfterCursorBucket.Contains(enrolment.ArmId)) ||
            (!cursorIsAtSentinel &&
                !context.Enrolments.Any(enrolment => enrolment.PupilId == pupil.Id && enrolment.EffectiveTo == null)));

        var afterBucketRows = await SelectOrderedByClassKeyAsync(afterBucketQuery, pageSize, cancellationToken)
            .ConfigureAwait(false);

        // Group 2: rows in the SAME bucket as the cursor — bounded by one arm's roster (spec 9.5: "an
        // arm is at most a hundred pupils"), or the trailing sentinel block when the cursor itself was
        // already in it. Fetched whole (no string.Compare in THIS query either), then the surname/id
        // tie-break and ordering run in plain .NET — see the trap note above for why.
        var sameBucketQuery = query.Where(pupil =>
            context.Enrolments.Any(enrolment =>
                enrolment.PupilId == pupil.Id && enrolment.EffectiveTo == null &&
                armIdsAtCursorBucket.Contains(enrolment.ArmId)) ||
            (cursorIsAtSentinel &&
                !context.Enrolments.Any(enrolment => enrolment.PupilId == pupil.Id && enrolment.EffectiveTo == null)));

        var sameBucketCandidates = await sameBucketQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

        var sameBucketRows = sameBucketCandidates
            .Where(pupil =>
                string.CompareOrdinal(pupil.Surname.ToLowerInvariant(), cursorSurnameKey) > 0 ||
                (pupil.Surname.Equals(cursorSurnameKey, StringComparison.OrdinalIgnoreCase) &&
                    pupil.Id.CompareTo(cursorId) > 0))
            .OrderBy(pupil => pupil.Surname, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pupil => pupil.Id)
            .Select(pupil => ToRow(pupil, cursorLevelOrdinal, cursorArmKey))
            .ToList();

        // Same-bucket rows sort BEFORE the strictly-after-bucket rows (they share the cursor's own
        // bucket, which is earlier than any "after" bucket) — concatenate in that order, then take one
        // extra row to learn whether a further page exists, exactly like every other page fetch here.
        var rows = sameBucketRows
            .Concat(afterBucketRows)
            .Take(pageSize + 1)
            .ToList();

        return ToRegisterPage(rows, pageSize, term, asOfDate);
    }

    /// <summary>
    /// Projects <paramref name="pupils"/> with each row's (LevelOrdinal, ArmKey) resolved through a
    /// correlated subquery over its OPEN enrolment (TASK-0061), ordered class-progression-then-arm-
    /// then-surname-then-id, and limited to <paramref name="pageSize"/> + 1. Shared by the first page
    /// (no cursor) and the "strictly after the cursor's bucket" group — both need the SAME projection,
    /// and NEITHER carries a `string.Compare` call, so both stay entirely server-side.
    /// </summary>
    private async Task<List<(Pupil Pupil, int LevelOrdinal, string ArmKey)>> SelectOrderedByClassKeyAsync(
        IQueryable<Pupil> pupils, int pageSize, CancellationToken cancellationToken)
    {
        var rows = await pupils
            .Select(pupil => new
            {
                Pupil = pupil,
                LevelOrdinal = (
                    from enrolment in context.Enrolments
                    where enrolment.PupilId == pupil.Id && enrolment.EffectiveTo == null
                    join arm in context.Arms on enrolment.ArmId equals arm.Id
                    join level in context.ClassLevels on arm.ClassLevelId equals level.Id
                    select (int?)level.ProgressionOrder)
                    .FirstOrDefault() ?? PupilRegisterCursor.UnenrolledLevelOrdinal,
                ArmKey = (
                    from enrolment in context.Enrolments
                    where enrolment.PupilId == pupil.Id && enrolment.EffectiveTo == null
                    join arm in context.Arms on enrolment.ArmId equals arm.Id
                    select arm.LabelKey)
                    .FirstOrDefault() ?? PupilRegisterCursor.UnenrolledArmKey,
            })
            .OrderBy(row => row.LevelOrdinal)
            .ThenBy(row => row.ArmKey)
            .ThenBy(row => row.Pupil.Surname.ToLower())
            .ThenBy(row => row.Pupil.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(row => (row.Pupil, row.LevelOrdinal, row.ArmKey));
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
    public async Task<IReadOnlyList<string>> ListTakenRegistrationNumbersAsync(
        IReadOnlyCollection<string> registrationNumbers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registrationNumbers);

        if (registrationNumbers.Count == 0)
        {
            return [];
        }

        var numbers = registrationNumbers.ToArray();

        // IgnoreQueryFilters: the unique index spans every status, so the pre-check must too.
        return await context.Pupils
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(pupil => pupil.RegistrationNumber != null && numbers.Contains(pupil.RegistrationNumber))
            .Select(pupil => pupil.RegistrationNumber!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PupilRegisterEntry>> ListByDatesOfBirthAsync(
        IReadOnlyCollection<DateOnly> datesOfBirth, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datesOfBirth);

        if (datesOfBirth.Count == 0)
        {
            return [];
        }

        var dates = datesOfBirth.ToArray();

        // IgnoreQueryFilters: every status, as FindDuplicatesAsync — a pending admission for the same child is exactly what to catch.
        return await context.Pupils
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(pupil => dates.Contains(pupil.DateOfBirth))
            .OrderBy(pupil => pupil.CreatedAtUtc)
            .Select(pupil => new PupilRegisterEntry(
                pupil.Id, pupil.Surname, pupil.FirstName, pupil.DateOfBirth, pupil.RegistrationNumber, pupil.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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

    /// <summary>
    /// Builds the register's page and its <see cref="PupilRegisterCursor"/>-encoded <c>nextCursor</c>
    /// (TASK-0061) — the widened counterpart of the admissions queue's own inline paging in
    /// <see cref="ListAdmissionsQueueAsync"/>, which stays on <see cref="PupilListCursor"/> untouched.
    /// </summary>
    private static CursorPage<PupilDto> ToRegisterPage(
        List<(Pupil Pupil, int LevelOrdinal, string ArmKey)> rows, int pageSize, string? searchTerm, DateOnly asOfDate)
    {
        var hasNextPage = rows.Count > pageSize;
        var page = hasNextPage ? rows.GetRange(0, pageSize) : rows;

        var items = page
            .ConvertAll(row =>
            {
                var matchedField = PupilSearchMatcher.Resolve(
                    row.Pupil.Surname, row.Pupil.FirstName, row.Pupil.MiddleName, row.Pupil.RegistrationNumber, searchTerm);

                return PupilMapper.ToDto(row.Pupil, asOfDate, matchedField);
            });

        string? nextCursor = null;

        if (hasNextPage)
        {
            var last = page[^1];
            nextCursor = PupilRegisterCursor.Encode(last.LevelOrdinal, last.ArmKey, last.Pupil.Surname.ToLowerInvariant(), last.Pupil.Id);
        }

        return new CursorPage<PupilDto>(items, nextCursor);
    }
}
