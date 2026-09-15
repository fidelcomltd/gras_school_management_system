using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Abstractions.Pupils;

/// <summary>Persistence port for <see cref="PupilRegNumberHistory"/> (TASK-0063, spec 6.5.10).</summary>
public interface IPupilRegNumberHistoryRepository
{
    /// <summary>Appends a new row. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits. Never updated or deleted afterwards.</summary>
    Task AddAsync(PupilRegNumberHistory row, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="registrationNumber"/> already appears as SOMEONE's old, aliased
    /// number — the other half of TASK-0063's two-table uniqueness check (spec 6.5.10: "must be
    /// unique against both <c>pupil.registration_number</c> and <c>pupil_reg_number_history</c>").
    /// No exclusion parameter: unlike <c>IPupilRepository.ExistsByRegistrationNumberAsync</c>, a
    /// history row is never the same pupil's own live number, so there is no "self" to exclude.
    /// </summary>
    Task<bool> ExistsAsync(string registrationNumber, CancellationToken cancellationToken);
}
