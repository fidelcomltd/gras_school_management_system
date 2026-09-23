using SchoolManagement.Application.Abstractions.Persistence;
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
/// the caller stages the pupil's other rows first. A failed attempt's counter increments have already run as their own
/// statements, so a genuine collision burns those serials rather than reusing them.
/// </para>
/// </remarks>
internal sealed class RegistrationNumberIssuer(
    IRegistrationCounterRepository registrationCounters,
    IUnitOfWork unitOfWork,
    IPersistenceErrorTranslator persistenceErrorTranslator)
{
    /// <summary>Spec 6.5.10 rule 4: "the application retries the whole transaction up to three times".</summary>
    private const int MaxIssueAttempts = 3;

    private const string DuplicateValueErrorCode = "persistence.duplicate_value";

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
    /// the order given (rule 5), then ONE save. A duplicate retries the whole batch with fresh serials.
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
            var issued = new List<string>(batch.Count);
            foreach (var (pupil, admissionYear) in batch)
            {
                var counterKey = RegistrationCounterPartition.Resolve(profile.SerialReset, admissionYear);
                var serial = await registrationCounters
                    .IncrementAndGetNextSerialAsync(counterKey, cancellationToken)
                    .ConfigureAwait(false);

                // Byte-identical to the preview: both call RegNumberFormat.Compose.
                var number = RegNumberFormat.Compose(profile.Abbreviation, profile.Separator, admissionYear, profile.SerialWidth, serial);
                pupil.IssueRegistrationNumber(number);
                issued.Add(number);
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
                    return Result.Failure<IReadOnlyList<string>>(Error.Conflict(
                        "pupil.registration_number_issue_failed",
                        "Could not issue a registration number. Try again."));
                }
            }
        }
    }
}
