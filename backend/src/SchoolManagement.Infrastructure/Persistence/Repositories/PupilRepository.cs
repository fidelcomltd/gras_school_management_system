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
    public async Task<Pupil?> FindTrackedByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        await context.Database
            .SqlQuery<Guid>($"SELECT id FROM pupils WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await FindTrackedByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

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
        // TASK-0068: the cursor is ONE row-value comparison in SQL,
        // (level, armKey, lower(surname), id) > (cursor), over the same projection the ORDER BY sorts,
        // so both use the database collation and cannot disagree. TASK-0061 had split the read in two
        // and compared the surname in .NET (ordinal), because Npgsql will not translate
        // string.Compare next to a subquery; on a glibc en_US database that disagreed with the ORDER BY
        // on punctuation and lost O'Brien after Oakes, and it loaded the whole leavers block into memory.
        // EF.Functions.GreaterThan is a different translation that composes with the subqueries.
        var rows = Project(query);

        if (hasCursor)
        {
            rows = rows.Where(row => EF.Functions.GreaterThan(
                ValueTuple.Create(row.LevelOrdinal, row.ArmKey, row.SurnameKey, row.Pupil.Id),
                ValueTuple.Create(cursorLevelOrdinal, cursorArmKey, cursorSurnameKey, cursorId)));
        }

        var page = await rows
            .OrderBy(row => row.LevelOrdinal)
            .ThenBy(row => row.ArmKey)
            .ThenBy(row => row.SurnameKey)
            .ThenBy(row => row.Pupil.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToRegisterPage(page, pageSize, term, asOfDate);
    }

    /// <summary>
    /// Each pupil with its sort key: the level ordinal and arm key of its OPEN enrolment (the sentinel when it has
    /// none), and the surname lowered BY THE DATABASE, which is also what the next cursor carries, so the cursor's
    /// own row compares equal to itself.
    /// </summary>
    private IQueryable<RegisterRow> Project(IQueryable<Pupil> pupils) =>
        pupils.Select(pupil => new RegisterRow
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
            SurnameKey = pupil.Surname.ToLower(),
        });

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
        string? contactPhone,
        int maxResults,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surname);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);

        // Spec 6.5.11 step 1: surname + first name + date of birth, or separately surname + a contact phone (stored canonical,
        // so the caller passes the +234 form). The phone half catches a sibling or a re-admission under another first name.
        var contacts = context.Set<PupilContact>().AsNoTracking();
        var surnamePattern = EscapeLike(surname);
        var firstNamePattern = EscapeLike(firstName);
        var rows = await context.Pupils
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(pupil =>
                (EF.Functions.ILike(pupil.Surname, surnamePattern, LikeEscape) &&
                    EF.Functions.ILike(pupil.FirstName, firstNamePattern, LikeEscape) &&
                    pupil.DateOfBirth == dateOfBirth) ||
                (contactPhone != null &&
                    EF.Functions.ILike(pupil.Surname, surnamePattern, LikeEscape) &&
                    contacts.Any(contact => contact.PupilId == pupil.Id && (contact.Phone == contactPhone || contact.WhatsappNumber == contactPhone))))
            .OrderBy(pupil => pupil.CreatedAtUtc)
            .Take(maxResults)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(pupil => PupilMapper.ToDto(pupil, asOfDate));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(Pupil Pupil, Guid ArmId)>> ListActiveEnrolledInSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var rows = await (
                from enrolment in context.Enrolments.AsNoTracking()
                join arm in context.Arms.AsNoTracking() on enrolment.ArmId equals arm.Id
                join pupil in context.Pupils.AsNoTracking() on enrolment.PupilId equals pupil.Id
                where enrolment.EffectiveTo == null && arm.SessionId == sessionId && pupil.Status == PupilStatus.Active
                select new { Pupil = pupil, enrolment.ArmId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(row => (row.Pupil, row.ArmId));
    }

    /// <summary>The escape character for the duplicate check's case-insensitive exact matches.</summary>
    private const string LikeEscape = "\\";

    /// <summary>A name as a literal ILIKE pattern: "%" and "_" match themselves, never anything.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Pupil>> ListPendingReadOnlyAsync(CancellationToken cancellationToken) =>
        await context.Pupils.IgnoreQueryFilters().AsNoTracking()
            .Where(pupil => pupil.Status == PupilStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Pupil>> ListActiveEnrolledInArmAsync(Guid armId, CancellationToken cancellationToken) =>
        await (
                from enrolment in context.Enrolments.AsNoTracking()
                join pupil in context.Pupils.AsNoTracking() on enrolment.PupilId equals pupil.Id
                where enrolment.EffectiveTo == null && enrolment.ArmId == armId && pupil.Status == PupilStatus.Active
                select pupil)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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
        List<RegisterRow> rows, int pageSize, string? searchTerm, DateOnly asOfDate)
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
            nextCursor = PupilRegisterCursor.Encode(last.LevelOrdinal, last.ArmKey, last.SurnameKey, last.Pupil.Id);
        }

        return new CursorPage<PupilDto>(items, nextCursor);
    }

    /// <summary>One register row with its sort key (TASK-0061, TASK-0068).</summary>
    private sealed class RegisterRow
    {
        public required Pupil Pupil { get; init; }

        public required int LevelOrdinal { get; init; }

        public required string ArmKey { get; init; }

        public required string SurnameKey { get; init; }
    }
}
