using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Reference;

namespace SchoolManagement.Application.Reference.SampleRecords;

/// <summary>
/// REFERENCE SLICE — persistence port for <see cref="SampleRecord"/>. Implemented in Infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This interface is why Application never names an EF Core type. Note the asymmetry, which is
/// intentional and is the pattern to copy:
/// </para>
/// <list type="bullet">
/// <item>WRITES take and return the domain ENTITY, because a command has to enforce invariants
/// through the aggregate.</item>
/// <item>READS return a DTO, never an entity. The implementation projects in the SQL query, so the
/// database returns only the needed columns and nothing is tracked.</item>
/// </list>
/// <para>
/// A repository method never calls <c>SaveChangesAsync</c> — the unit-of-work behaviour owns the
/// transaction boundary. <c>AddAsync</c> stages the insert; the behaviour commits it.
/// </para>
/// </remarks>
public interface ISampleRecordRepository
{
    /// <summary>
    /// Stages a new record for insertion. Does NOT commit; the unit-of-work behaviour does that when
    /// the command returns a successful result.
    /// </summary>
    /// <param name="record">The record to add.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task AddAsync(SampleRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a live (not soft-deleted) record already uses <paramref name="label"/>.
    /// </summary>
    /// <remarks>
    /// Read-then-write is racy under concurrency: two simultaneous requests can both see "free" and
    /// both insert. It is here to produce a friendly 409 in the common case; the filtered unique
    /// index on the table is what actually guarantees correctness, and the handler surfaces the
    /// resulting constraint violation as the same 409. Never rely on this check alone.
    /// </remarks>
    /// <param name="label">The label to test.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<bool> LabelExistsAsync(string label, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one page of records, newest first, projected to DTOs.
    /// </summary>
    /// <param name="page">The page to fetch. Clamped defensively by the implementation.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<PagedResult<SampleRecordDto>> ListAsync(PageRequest page, CancellationToken cancellationToken);
}
