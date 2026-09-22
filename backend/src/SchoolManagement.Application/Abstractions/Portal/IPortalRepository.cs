using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Portal;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Abstractions.Portal;

/// <summary>The pupil a typed registration number resolves to (current number or a retired alias).</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Status">Withdrawn and transferred pupils are not reachable (spec 6.8.12).</param>
/// <param name="RegistrationNumber">The pupil's current number.</param>
/// <param name="DisplayName">Shown only after a pin has validated.</param>
public sealed record PortalPupil(Guid PupilId, PupilStatus Status, string RegistrationNumber, string DisplayName);

/// <summary>One term row on the parent's term selector (spec 6.9.2 step 4).</summary>
/// <param name="SessionId">The academic session.</param>
/// <param name="SessionName">e.g. 2026/2027.</param>
/// <param name="TermId">The term.</param>
/// <param name="TermName">e.g. First Term.</param>
/// <param name="TermOrdinal">1 to 3.</param>
/// <param name="ResultSetState">The pupil's arm's result set that term, or null when none exists.</param>
public sealed record PortalTermRow(Guid SessionId, string SessionName, Guid TermId, string TermName, int TermOrdinal, ResultSetState? ResultSetState);

/// <summary>Persistence port for the parent portal. Everything here is narrow on purpose (spec 6.9.8).</summary>
public interface IPortalRepository
{
    /// <summary>The pin with this lookup key, tracked and row-locked so two lookups cannot overspend it. Null when none.</summary>
    Task<Pin?> FindPinForUpdateAsync(string lookupKey, CancellationToken cancellationToken);

    /// <summary>The batch's session state and name, for the expired check and its copy.</summary>
    Task<(SessionState State, string Name)?> FindPinSessionAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>
    /// The pupil whose current or retired registration number, with separators removed and uppercased, equals
    /// <paramref name="normalizedRegistrationNumber"/>. Pending pupils are never returned.
    /// </summary>
    Task<PortalPupil?> FindPupilAsync(string normalizedRegistrationNumber, CancellationToken cancellationToken);

    /// <summary>A pupil by id, for a session already bound to them. Null when missing or pending.</summary>
    Task<PortalPupil?> FindPupilByIdAsync(Guid pupilId, CancellationToken cancellationToken);

    /// <summary>When the counted failures from this address happened since <paramref name="since"/>.</summary>
    Task<IReadOnlyList<DateTimeOffset>> ListFailureTimesByAddressAsync(string sourceAddress, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>When the counted failures against this registration number happened since <paramref name="since"/>.</summary>
    Task<IReadOnlyList<DateTimeOffset>> ListFailureTimesByRegistrationNumberAsync(string normalizedRegistrationNumber, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Whether this pin has ever opened this pupil.</summary>
    Task<bool> HasOpenedAsync(Guid pinId, Guid pupilId, CancellationToken cancellationToken);

    /// <summary>The distinct pupils this pin opened since <paramref name="since"/>.</summary>
    Task<IReadOnlySet<Guid>> ListPupilsOpenedSinceAsync(Guid pinId, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>The pupil's term rows across every session they were enrolled in, newest session first.</summary>
    Task<IReadOnlyList<PortalTermRow>> ListTermsAsync(Guid pupilId, CancellationToken cancellationToken);

    /// <summary>The sessions in which the pupil has a computed annual result (spec 6.7.10).</summary>
    Task<IReadOnlySet<Guid>> ListAnnualSessionIdsAsync(Guid pupilId, CancellationToken cancellationToken);

    /// <summary>A viewing session by its token hash, with its pin, or null. Tracked.</summary>
    Task<(PinUse Use, Pin Pin)?> FindUseAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Stages an attempt row. Does NOT commit.</summary>
    Task AddAttemptAsync(PortalAttempt attempt, CancellationToken cancellationToken);

    /// <summary>Stages a viewing session. Does NOT commit.</summary>
    Task AddUseAsync(PinUse use, CancellationToken cancellationToken);
}
