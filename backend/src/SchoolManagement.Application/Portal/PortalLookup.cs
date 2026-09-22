using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pins;
using SchoolManagement.Application.Abstractions.Portal;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Portal;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Portal;

/// <summary>What a lookup ended in; each maps to one of spec 6.9.4's parent-facing copies.</summary>
public enum PortalLookupStatus
{
    /// <summary>A new viewing session opened and one use was counted.</summary>
    Opened,

    /// <summary>A live session for the same pin and pupil already existed on this device; no use counted.</summary>
    Resumed,

    /// <summary>Unknown number, unrecognised pin, or a pupil who has left. The one uniform failure.</summary>
    NotFound,

    /// <summary>Pin has no uses left.</summary>
    PinExhausted,

    /// <summary>Pin suspended by the spread control.</summary>
    PinSuspended,

    /// <summary>Pin revoked.</summary>
    PinRevoked,

    /// <summary>Pin's session has closed.</summary>
    PinExpired,

    /// <summary>Validated, but nothing is published for this pupil; no use counted.</summary>
    NotPublished,

    /// <summary>Too many failed attempts from this device.</summary>
    TooManyTriesDevice,

    /// <summary>Too many failed attempts on this registration number.</summary>
    TooManyTriesPupil,
}

/// <summary>The outcome, with just enough for the copy. Never the pin value.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Token">The new or resumed session's cookie token, only on Opened or Resumed.</param>
/// <param name="MaxUses">For the exhausted copy.</param>
/// <param name="SessionName">For the expired copy.</param>
public sealed record PortalLookupResult(PortalLookupStatus Status, string? Token = null, int? MaxUses = null, string? SessionName = null);

/// <summary>Spec 6.9.9 <c>POST /portal/lookup</c>.</summary>
/// <param name="RegistrationNumber">As typed.</param>
/// <param name="Pin">As typed.</param>
/// <param name="SourceAddress">Truncated by the endpoint.</param>
/// <param name="UserAgent">For the use row.</param>
/// <param name="ExistingTokens">Session tokens the device already holds, for resuming.</param>
public sealed record PortalLookupCommand(
    string RegistrationNumber, string Pin, string? SourceAddress, string? UserAgent, IReadOnlyList<string> ExistingTokens) : ICommand<Result<PortalLookupResult>>;

/// <summary>Lengths only; content is never echoed back.</summary>
internal sealed class PortalLookupCommandValidator : AbstractValidator<PortalLookupCommand>
{
    public PortalLookupCommandValidator()
    {
        RuleFor(command => command.RegistrationNumber).NotNull().MaximumLength(60);
        RuleFor(command => command.Pin).NotNull().MaximumLength(40);
    }
}

/// <summary>Normalising and hashing shared by the portal handlers.</summary>
public static class PortalValues
{
    /// <summary>A registration number with separators removed and uppercased, as spec 6.9.2 step 2 requires.</summary>
    public static string NormalizeRegistrationNumber(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '-' or '/' or ' ' or '.' or '\t')
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>A fresh opaque cookie token: 32 random bytes, base64url.</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>What is stored for a token: its hex SHA-256.</summary>
    public static string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Handles <see cref="PortalLookupCommand"/> (spec 6.9.2 step 3, 6.9.3, 6.9.4, 6.8.8).</summary>
internal sealed class PortalLookupHandler(IPortalRepository portal, IPinSecrets secrets, TimeProvider timeProvider)
    : IRequestHandler<PortalLookupCommand, Result<PortalLookupResult>>
{
    private const int AddressFailureLimit = 10;
    private static readonly TimeSpan AddressFailureWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AddressBlock = TimeSpan.FromMinutes(30);
    private const int NumberFailureLimit = 5;
    private static readonly TimeSpan NumberFailureWindow = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan NumberBlock = TimeSpan.FromMinutes(60);

    /// <inheritdoc />
    public async Task<Result<PortalLookupResult>> HandleAsync(PortalLookupCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var number = PortalValues.NormalizeRegistrationNumber(request.RegistrationNumber);
        var pinValue = PinValue.Normalize(request.Pin);
        var prefix = pinValue.Length >= Pin.PrefixLength ? pinValue[..Pin.PrefixLength] : null;

        if (request.SourceAddress is { } address
            && IsBlocked(await portal.ListFailureTimesByAddressAsync(address, now - AddressBlock - AddressFailureWindow, cancellationToken).ConfigureAwait(false), now, AddressFailureLimit, AddressFailureWindow, AddressBlock))
        {
            return await RecordAsync(PortalAttemptOutcome.BlockedAddress, new PortalLookupResult(PortalLookupStatus.TooManyTriesDevice)).ConfigureAwait(false);
        }

        if (IsBlocked(await portal.ListFailureTimesByRegistrationNumberAsync(number, now - NumberBlock - NumberFailureWindow, cancellationToken).ConfigureAwait(false), now, NumberFailureLimit, NumberFailureWindow, NumberBlock))
        {
            return await RecordAsync(PortalAttemptOutcome.BlockedRegistrationNumber, new PortalLookupResult(PortalLookupStatus.TooManyTriesPupil)).ConfigureAwait(false);
        }

        // Spec 6.9.2 step 3: validate the pin, then resolve the number. Either failing is the same copy.
        var pin = pinValue.Length == 0 ? null : await portal.FindPinForUpdateAsync(secrets.LookupKeyFor(pinValue), cancellationToken).ConfigureAwait(false);
        if (pin is null || !secrets.Verify(pinValue, pin.PinHash))
        {
            return await RecordAsync(PortalAttemptOutcome.NotFound, new PortalLookupResult(PortalLookupStatus.NotFound)).ConfigureAwait(false);
        }

        var pupil = number.Length == 0 ? null : await portal.FindPupilAsync(number, cancellationToken).ConfigureAwait(false);
        if (pupil is null || pupil.Status is PupilStatus.Withdrawn or PupilStatus.Transferred)
        {
            return await RecordAsync(PortalAttemptOutcome.NotFound, new PortalLookupResult(PortalLookupStatus.NotFound), pin.Id).ConfigureAwait(false);
        }

        switch (pin.State)
        {
            case PinState.Revoked:
                return await RecordAsync(PortalAttemptOutcome.PinRevoked, new PortalLookupResult(PortalLookupStatus.PinRevoked), pin.Id).ConfigureAwait(false);
            case PinState.Suspended:
                return await RecordAsync(PortalAttemptOutcome.PinSuspended, new PortalLookupResult(PortalLookupStatus.PinSuspended), pin.Id).ConfigureAwait(false);
        }

        var pinSession = await portal.FindPinSessionAsync(pin.BatchId, cancellationToken).ConfigureAwait(false);
        if (pinSession is { State: SessionState.Closed } closed)
        {
            return await RecordAsync(PortalAttemptOutcome.PinExpired, new PortalLookupResult(PortalLookupStatus.PinExpired, SessionName: closed.Name), pin.Id).ConfigureAwait(false);
        }

        // Spec 6.9.5: a returning parent within the window on the same device resumes without spending a use.
        foreach (var token in request.ExistingTokens)
        {
            if (await portal.FindUseAsync(PortalValues.HashToken(token), cancellationToken).ConfigureAwait(false) is { } open
                && open.Use.IsOpenAt(now) && open.Use.PinId == pin.Id && open.Use.PupilId == pupil.PupilId)
            {
                return Result.Success(new PortalLookupResult(PortalLookupStatus.Resumed, token));
            }
        }

        if (!pin.IsUsable)
        {
            return await RecordAsync(PortalAttemptOutcome.PinExhausted, new PortalLookupResult(PortalLookupStatus.PinExhausted, MaxUses: pin.MaxUses), pin.Id).ConfigureAwait(false);
        }

        var terms = await portal.ListTermsAsync(pupil.PupilId, cancellationToken).ConfigureAwait(false);
        if (!terms.Any(term => term.ResultSetState == ResultSetState.Published))
        {
            return await RecordAsync(PortalAttemptOutcome.NotPublished, new PortalLookupResult(PortalLookupStatus.NotPublished), pin.Id).ConfigureAwait(false);
        }

        // Spec 6.9.3 spread control: more than 3 distinct pupils overall, or more than 2 within 10 minutes, suspends.
        var isNewPupil = !await portal.HasOpenedAsync(pin.Id, pupil.PupilId, cancellationToken).ConfigureAwait(false);
        if (isNewPupil)
        {
            var recent = await portal.ListPupilsOpenedSinceAsync(pin.Id, now - Pin.SpreadWindow, cancellationToken).ConfigureAwait(false);
            if (pin.DistinctPupilCount + 1 > Pin.MaxDistinctPupils || recent.Count + 1 > Pin.MaxDistinctPupilsInWindow)
            {
                pin.Suspend(pin.DistinctPupilCount + 1 > Pin.MaxDistinctPupils
                    ? $"Opened more than {Pin.MaxDistinctPupils.ToString(CultureInfo.InvariantCulture)} different pupils."
                    : $"Opened more than {Pin.MaxDistinctPupilsInWindow.ToString(CultureInfo.InvariantCulture)} different pupils within 10 minutes.");
                return await RecordAsync(PortalAttemptOutcome.PinSuspended, new PortalLookupResult(PortalLookupStatus.PinSuspended), pin.Id).ConfigureAwait(false);
            }
        }

        var newToken = PortalValues.NewToken();
        pin.RecordUse(isNewPupil);
        await portal.AddUseAsync(PinUse.Open(pin.Id, pupil.PupilId, PortalValues.HashToken(newToken), now, request.SourceAddress, request.UserAgent), cancellationToken).ConfigureAwait(false);
        return await RecordAsync(PortalAttemptOutcome.Success, new PortalLookupResult(PortalLookupStatus.Opened, newToken), pin.Id).ConfigureAwait(false);

        async Task<Result<PortalLookupResult>> RecordAsync(PortalAttemptOutcome outcome, PortalLookupResult result, Guid? pinId = null)
        {
            await portal.AddAttemptAsync(PortalAttempt.Record(number, prefix, pinId, outcome, request.SourceAddress, now), cancellationToken).ConfigureAwait(false);
            return Result.Success(result);
        }
    }

    /// <summary>
    /// A block starts when <paramref name="limit"/> failures fall inside one <paramref name="window"/>, and lasts
    /// <paramref name="block"/> from the failure that reached the limit.
    /// </summary>
    internal static bool IsBlocked(IReadOnlyList<DateTimeOffset> failures, DateTimeOffset now, int limit, TimeSpan window, TimeSpan block)
    {
        var ordered = failures.Order().ToList();
        for (var index = limit - 1; index < ordered.Count; index++)
        {
            if (ordered[index] - ordered[index - limit + 1] <= window && now - ordered[index] < block)
            {
                return true;
            }
        }

        return false;
    }
}
