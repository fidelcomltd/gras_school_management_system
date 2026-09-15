namespace SchoolManagement.Application.Settings;

/// <summary>
/// Persistence port for spec 6.5.10's <c>registration_counter</c> table. TASK-0005c built the table
/// and its read-only half (<see cref="GetLastSerialAsync"/>, used by the preview); TASK-0051 adds the
/// atomic increment beside it, on this same interface, per that card's own instruction not to build a
/// second one.
/// </summary>
public interface IRegistrationCounterRepository
{
    /// <summary>
    /// The partition's current highest issued serial, or 0 when no row exists yet for
    /// <paramref name="counterKey"/> — meaning nothing has ever been issued under this partition,
    /// never a claim that serial 0 itself was issued.
    /// </summary>
    /// <param name="counterKey">
    /// The admission year as a string (<c>per_year</c>) or
    /// <see cref="SchoolManagement.Domain.Settings.RegistrationCounterPartition.ContinuousKey"/>
    /// (<c>continuous</c>) — see <see cref="SchoolManagement.Domain.Settings.RegistrationCounterPartition.Resolve"/>.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<int> GetLastSerialAsync(string counterKey, CancellationToken cancellationToken);

    /// <summary>
    /// ATOMICALLY increments <paramref name="counterKey"/>'s row (creating it at 1 if absent) and
    /// returns the NEW <c>last_serial</c> — spec 6.5.10's own statement, verbatim: <c>INSERT INTO
    /// registration_counter (counter_key, last_serial) VALUES ($1, 1) ON CONFLICT (counter_key) DO
    /// UPDATE SET last_serial = registration_counter.last_serial + 1 RETURNING last_serial</c>. Takes
    /// a row lock and serialises concurrent callers on the same <paramref name="counterKey"/> — a
    /// <c>MAX(serial) + 1</c> or a <c>COUNT(*)</c> is never an acceptable substitute (spec 6.5.10:
    /// "the mechanism that produces duplicates"). Executes IMMEDIATELY, inside whatever transaction is
    /// already ambient on the caller's <c>DbContext</c> — never through change tracking, so the
    /// increment is visible to the SAME transaction's later statements without a
    /// <c>SaveChangesAsync</c>. Admission approval (TASK-0051) is the ONLY caller; a caller that fails
    /// after this returns and retries the whole attempt calls this again rather than reusing the
    /// value, which is exactly how spec 6.5.10 rule 4's retry works.
    /// </summary>
    /// <param name="counterKey">See <see cref="GetLastSerialAsync"/>.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<int> IncrementAndGetNextSerialAsync(string counterKey, CancellationToken cancellationToken);
}
