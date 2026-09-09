namespace SchoolManagement.Application.Settings;

/// <summary>
/// READ-ONLY persistence port for spec 6.5.10's <c>registration_counter</c> table. TASK-0005c owns
/// only the table and this read; incrementing it is admission approval's job (TASK-0051), through a
/// raw atomic <c>INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING</c> statement this interface
/// deliberately has no member shaped for — see
/// <see cref="SchoolManagement.Domain.Settings.RegistrationCounter"/>'s remarks for why.
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
}
