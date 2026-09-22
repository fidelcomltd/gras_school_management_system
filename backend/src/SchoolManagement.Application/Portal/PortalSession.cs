using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Portal;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Portal;

/// <summary>How a term shows on the selector (spec 6.9.2 step 4).</summary>
public enum PortalTermAvailability
{
    /// <summary>Published; the parent can open it.</summary>
    Available,

    /// <summary>Not published yet.</summary>
    NotYetReleased,

    /// <summary>Was published and has been withdrawn for correction (spec 6.9.4).</summary>
    BeingCorrected,
}

/// <summary>One selectable term.</summary>
/// <param name="TermId">The term.</param>
/// <param name="TermName">e.g. First Term.</param>
/// <param name="Availability">Its label.</param>
public sealed record PortalTerm(Guid TermId, string TermName, PortalTermAvailability Availability);

/// <summary>A session's terms, newest session first.</summary>
/// <param name="SessionName">e.g. 2026/2027.</param>
/// <param name="Terms">In term order.</param>
public sealed record PortalSessionTerms(string SessionName, IReadOnlyList<PortalTerm> Terms);

/// <summary>An open viewing session, as the term selector shows it.</summary>
/// <param name="UseId">Opaque id; the page links carry it so a parent with two open sessions can switch.</param>
/// <param name="PupilId">The one pupil it is bound to.</param>
/// <param name="PupilName">Shown only now that a pin has validated.</param>
/// <param name="RegistrationNumber">The pupil's current number.</param>
/// <param name="ExpiresAt">For the countdown in the last five minutes.</param>
/// <param name="UsesLeft">For the "check another pupil" copy.</param>
/// <param name="Sessions">The term selector.</param>
public sealed record PortalViewingSession(
    Guid UseId, Guid PupilId, string PupilName, string RegistrationNumber, DateTimeOffset ExpiresAt, int UsesLeft, IReadOnlyList<PortalSessionTerms> Sessions);

/// <summary>Resolves the device's session tokens to their open viewing sessions (spec 6.9.5).</summary>
/// <param name="Tokens">From the cookie.</param>
public sealed record GetPortalSessionsQuery(IReadOnlyList<string> Tokens) : IQuery<Result<IReadOnlyList<PortalViewingSession>>>;

/// <summary>At most five tokens.</summary>
internal sealed class GetPortalSessionsQueryValidator : AbstractValidator<GetPortalSessionsQuery>
{
    public GetPortalSessionsQueryValidator() => RuleFor(query => query.Tokens.Count).LessThanOrEqualTo(PortalSessionRules.MaxSessionsPerDevice);
}

/// <summary>Ends every session the device holds (spec 6.9.9 <c>POST /portal/end</c>).</summary>
/// <param name="Tokens">From the cookie.</param>
public sealed record EndPortalSessionsCommand(IReadOnlyList<string> Tokens) : ICommand<Result>;

/// <summary>At most five tokens.</summary>
internal sealed class EndPortalSessionsCommandValidator : AbstractValidator<EndPortalSessionsCommand>
{
    public EndPortalSessionsCommandValidator() => RuleFor(command => command.Tokens.Count).LessThanOrEqualTo(PortalSessionRules.MaxSessionsPerDevice);
}

/// <summary>Limits shared by the portal endpoints.</summary>
public static class PortalSessionRules
{
    /// <summary>How many concurrent viewing sessions one device keeps in its cookie.</summary>
    public const int MaxSessionsPerDevice = 5;
}

/// <summary>Handles <see cref="GetPortalSessionsQuery"/>. A revoked or suspended pin closes its sessions on the next request.</summary>
internal sealed class GetPortalSessionsHandler(IPortalRepository portal, TimeProvider timeProvider)
    : IRequestHandler<GetPortalSessionsQuery, Result<IReadOnlyList<PortalViewingSession>>>
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PortalViewingSession>>> HandleAsync(GetPortalSessionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var sessions = new List<PortalViewingSession>();

        foreach (var token in request.Tokens.Distinct(StringComparer.Ordinal))
        {
            if (await portal.FindUseAsync(PortalValues.HashToken(token), cancellationToken).ConfigureAwait(false) is not { } found
                || !found.Use.IsOpenAt(now)
                || found.Pin.State is PinState.Revoked or PinState.Suspended)
            {
                continue;
            }

            var pupil = await portal.FindPupilByIdAsync(found.Use.PupilId, cancellationToken).ConfigureAwait(false);
            if (pupil is null)
            {
                continue;
            }

            var terms = await portal.ListTermsAsync(found.Use.PupilId, cancellationToken).ConfigureAwait(false);
            var grouped = terms
                .GroupBy(term => (term.SessionId, term.SessionName))
                .Select(group => new PortalSessionTerms(
                    group.Key.SessionName,
                    group.OrderBy(term => term.TermOrdinal).Select(term => new PortalTerm(term.TermId, term.TermName, AvailabilityOf(term.ResultSetState))).ToList()))
                .ToList();

            sessions.Add(new PortalViewingSession(
                found.Use.Id, found.Use.PupilId, pupil.DisplayName, pupil.RegistrationNumber, found.Use.ExpiresAtUtc,
                Math.Max(0, found.Pin.MaxUses - found.Pin.UseCount), grouped));
        }

        return Result.Success<IReadOnlyList<PortalViewingSession>>(sessions);
    }

    private static PortalTermAvailability AvailabilityOf(ResultSetState? state) => state switch
    {
        ResultSetState.Published => PortalTermAvailability.Available,
        ResultSetState.Withdrawn => PortalTermAvailability.BeingCorrected,
        _ => PortalTermAvailability.NotYetReleased,
    };
}

/// <summary>Handles <see cref="EndPortalSessionsCommand"/>.</summary>
internal sealed class EndPortalSessionsHandler(IPortalRepository portal, TimeProvider timeProvider) : IRequestHandler<EndPortalSessionsCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> HandleAsync(EndPortalSessionsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        foreach (var token in request.Tokens)
        {
            if (await portal.FindUseAsync(PortalValues.HashToken(token), cancellationToken).ConfigureAwait(false) is { } found)
            {
                found.Use.End(timeProvider.GetUtcNow());
            }
        }

        return Result.Success();
    }
}
