using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// Issues a registration number (spec 6.5.10) and saves it: the ONE issuance path, shared by admission approval and
/// bulk import (rule 5: "Bulk import uses the same counter, incrementing once per row inside the import transaction").
/// </summary>
/// <remarks>
/// <para>
/// THE COUNTER IS A ROW LOCK, NEVER A MAXIMUM QUERY. <see cref="IRegistrationCounterRepository.IncrementAndGetNextSerialAsync"/>
/// runs the raw <c>INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING</c> statement, immediately, inside the caller's
/// ambient transaction (<c>UnitOfWorkBehavior</c>).
/// </para>
/// <para>
/// RETRY ON A UNIQUE-INDEX VIOLATION (rule 4): the loop calls <see cref="IUnitOfWork.SaveChangesAsync"/> directly, the
/// sanctioned escape hatch <c>IUnitOfWork</c>'s remarks name. EF Core wraps a save made under an already-open,
/// caller-managed transaction in an automatic SAVEPOINT, so a failed attempt rolls back only that savepoint and the
/// ambient transaction stays usable for the next attempt. Everything the caller has staged is saved with the number, so
/// the caller stages the pupil's other rows first. Before saving, the drawn numbers are checked against the register in
/// one query per draw and a held number is replaced by one more serial (see <see cref="DrawAsync"/>), so a clash
/// (possible only after a manual correction) burns that one serial; the index and the whole-batch retry remain the
/// backstop for a concurrent insert.
/// </para>
/// </remarks>
internal sealed class RegistrationNumberIssuer(
    IRegistrationCounterRepository registrationCounters,
    IPupilRepository pupils,
    IUnitOfWork unitOfWork,
    IPersistenceErrorTranslator persistenceErrorTranslator)
{
    /// <summary>Spec 6.5.10 rule 4: "the application retries the whole transaction up to three times".</summary>
    private const int MaxIssueAttempts = 3;

    private const string DuplicateValueErrorCode = "persistence.duplicate_value";

    private static readonly Error IssueFailed = Error.Conflict(
        "pupil.registration_number_issue_failed", "Could not issue a registration number. Try again.");

    /// <summary>
    /// Takes the next serial for <paramref name="admissionYear"/>'s counter, composes the number from SAVED settings,
    /// writes it to <paramref name="pupil"/> and saves everything staged. Returns the issued number.
    /// </summary>
    /// <param name="pupil">A tracked pupil, already approved or created active.</param>
    /// <param name="admissionYear"><c>admission_record.date_admitted</c>'s year, never today's (spec 6.5.10).</param>
    /// <param name="profile">The saved settings; the abbreviation is frozen into the string here.</param>
    /// <param name="cancellationToken">Propagated.</param>
    public async Task<Result<string>> IssueAndSaveAsync(
        Pupil pupil, int admissionYear, SchoolProfile profile, CancellationToken cancellationToken)
    {
        var issued = await IssueAndSaveAsync([(pupil, admissionYear)], profile, cancellationToken).ConfigureAwait(false);
        return issued.IsFailure ? Result.Failure<string>(issued.Error) : Result.Success(issued.Value[0]);
    }

    /// <summary>
    /// The same, for several pupils in order: one counter increment per pupil, so a batch takes consecutive serials in
    /// the order given (rule 5), then ONE save. A duplicate at save (a concurrent insert the pre-check could not see)
    /// retries the whole batch with fresh serials.
    /// </summary>
    /// <param name="batch">Tracked pupils with their admission years, in issue order.</param>
    /// <param name="profile">The saved settings.</param>
    /// <param name="cancellationToken">Propagated.</param>
    public async Task<Result<IReadOnlyList<string>>> IssueAndSaveAsync(
        IReadOnlyList<(Pupil Pupil, int AdmissionYear)> batch, SchoolProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(profile);

        for (var attempt = 1; ; attempt++)
        {
            var drawn = await DrawAsync(batch, profile, cancellationToken).ConfigureAwait(false);
            if (drawn.IsFailure)
            {
                return Result.Failure<IReadOnlyList<string>>(drawn.Error);
            }

            var issued = drawn.Value;
            for (var index = 0; index < batch.Count; index++)
            {
                batch[index].Pupil.IssueRegistrationNumber(issued[index]);
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result.Success<IReadOnlyList<string>>(issued);
            }
            catch (Exception exception) when (
                persistenceErrorTranslator.TryTranslate(exception) is { Code: DuplicateValueErrorCode })
            {
                if (attempt == MaxIssueAttempts)
                {
                    return Result.Failure<IReadOnlyList<string>>(IssueFailed);
                }
            }
        }
    }

    /// <summary>
    /// Draws one serial per pupil, admission year by admission year. A drawn number some pupil already holds is burned
    /// and one more serial drawn in its place, and the free numbers go to the pupils in serial order, so file order holds
    /// and a clash costs exactly one serial. More than <see cref="MaxIssueAttempts"/> draws per pupil fails, as rule 4's
    /// retry limit does.
    /// </summary>
    private async Task<Result<string[]>> DrawAsync(
        IReadOnlyList<(Pupil Pupil, int AdmissionYear)> batch, SchoolProfile profile, CancellationToken cancellationToken)
    {
        var issued = new string[batch.Count];
        var byYear = Enumerable.Range(0, batch.Count).GroupBy(index => batch[index].AdmissionYear);
        foreach (var year in byYear)
        {
            var rows = year.ToList();
            var free = new List<string>(rows.Count);
            var burned = 0;
            while (free.Count < rows.Count)
            {
                var candidates = new List<string>(rows.Count - free.Count);
                for (var draw = free.Count; draw < rows.Count; draw++)
                {
                    candidates.Add(await NextNumberAsync(year.Key, profile, cancellationToken).ConfigureAwait(false));
                }

                var taken = (await pupils.ListTakenRegistrationNumbersAsync(candidates, cancellationToken).ConfigureAwait(false))
                    .ToHashSet(StringComparer.Ordinal);
                free.AddRange(candidates.Where(number => !taken.Contains(number)));
                burned += taken.Count;
                if (burned > (MaxIssueAttempts - 1) * rows.Count)
                {
                    return Result.Failure<string[]>(IssueFailed);
                }
            }

            for (var position = 0; position < rows.Count; position++)
            {
                issued[rows[position]] = free[position];
            }
        }

        return Result.Success(issued);
    }

    // One increment of the partition's counter, composed from SAVED settings: byte-identical to the preview, since both
    // call RegNumberFormat.Compose.
    private async Task<string> NextNumberAsync(int admissionYear, SchoolProfile profile, CancellationToken cancellationToken)
    {
        var counterKey = RegistrationCounterPartition.Resolve(profile.SerialReset, admissionYear);
        var serial = await registrationCounters.IncrementAndGetNextSerialAsync(counterKey, cancellationToken).ConfigureAwait(false);
        return RegNumberFormat.Compose(profile.Abbreviation, profile.Separator, admissionYear, profile.SerialWidth, serial);
    }
}
