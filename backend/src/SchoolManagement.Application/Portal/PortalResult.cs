using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Portal;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Results.Sheets;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Portal;

/// <summary>What opening a term ended in (spec 6.9.2 step 5, 6.9.4).</summary>
public enum PortalResultStatus
{
    /// <summary>The sheet is available.</summary>
    Shown,

    /// <summary>No open session for this device; the session-ended copy.</summary>
    SessionEnded,

    /// <summary>The term is not published for this pupil's class.</summary>
    NotReleased,

    /// <summary>Published, then withdrawn for correction.</summary>
    BeingCorrected,

    /// <summary>Published, but this pupil has no result in it.</summary>
    NoResult,
}

/// <summary>The outcome of opening a term.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Sheet">Present when shown.</param>
/// <param name="TermName">For the not-released copy.</param>
/// <param name="UseId">The session it was opened in, for the page's links.</param>
/// <param name="PdfKey">Present when shown: identifies the sheet for the PDF cache and the verification token.</param>
public sealed record PortalResultView(PortalResultStatus Status, ResultSheet? Sheet = null, string? TermName = null, Guid? UseId = null, ResultPdfKey? PdfKey = null);

/// <summary>Spec 6.9.9 <c>GET /portal/result/{term_id}</c>, for the pupil the chosen session is bound to.</summary>
/// <param name="Tokens">From the cookie.</param>
/// <param name="UseId">Which open session, when the device has more than one.</param>
/// <param name="TermId">The term.</param>
/// <param name="ForPdf">Recorded separately in the session's viewed list.</param>
public sealed record GetPortalResultQuery(IReadOnlyList<string> Tokens, Guid? UseId, Guid TermId, bool ForPdf = false) : IQuery<Result<PortalResultView>>;

/// <summary>At most five tokens.</summary>
internal sealed class GetPortalResultQueryValidator : AbstractValidator<GetPortalResultQuery>
{
    public GetPortalResultQueryValidator() => RuleFor(query => query.Tokens.Count).LessThanOrEqualTo(PortalSessionRules.MaxSessionsPerDevice);
}

/// <summary>Handles <see cref="GetPortalResultQuery"/>. Viewing never spends a use (spec 6.8.8).</summary>
internal sealed class GetPortalResultHandler(IPortalRepository portal, IResultSheetReader sheets, TimeProvider timeProvider)
    : IRequestHandler<GetPortalResultQuery, Result<PortalResultView>>
{
    /// <inheritdoc />
    public async Task<Result<PortalResultView>> HandleAsync(GetPortalResultQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await PortalSessionResolver.ResolveAsync(portal, request.Tokens, request.UseId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false)
            is not { } session)
        {
            return Result.Success(new PortalResultView(PortalResultStatus.SessionEnded));
        }

        var terms = await portal.ListTermsAsync(session.Use.PupilId, cancellationToken).ConfigureAwait(false);
        var term = terms.FirstOrDefault(candidate => candidate.TermId == request.TermId);
        switch (term?.ResultSetState)
        {
            case null when term is null:
                return Result.Success(new PortalResultView(PortalResultStatus.NoResult, UseId: session.Use.Id));
            case ResultSetState.Withdrawn:
                return Result.Success(new PortalResultView(PortalResultStatus.BeingCorrected, TermName: term.TermName, UseId: session.Use.Id));
            case not ResultSetState.Published:
                return Result.Success(new PortalResultView(PortalResultStatus.NotReleased, TermName: term!.TermName, UseId: session.Use.Id));
        }

        var data = await sheets.ReadAsync(session.Use.PupilId, request.TermId, cancellationToken).ConfigureAwait(false);
        var sheet = data is { State: ResultSetState.Published } ? ResultSheetBuilder.Build(data) : null;
        if (sheet is null || data!.Lines.Count == 0)
        {
            return Result.Success(new PortalResultView(PortalResultStatus.NoResult, TermName: term.TermName, UseId: session.Use.Id));
        }

        return Result.Success(new PortalResultView(
            PortalResultStatus.Shown, sheet, term.TermName, session.Use.Id, new ResultPdfKey(data.ResultSetId, session.Use.PupilId, data.RevisionNumber)));
    }
}

/// <summary>
/// Picks the device's open viewing session: the one named by <c>useId</c>, else the most recent. Shared by every page that
/// shows a pupil's results, so they can never disagree about who may see what.
/// </summary>
internal static class PortalSessionResolver
{
    /// <summary>The open, unrevoked, unsuspended session, or null.</summary>
    public static async Task<(Domain.Portal.PinUse Use, Pin Pin)?> ResolveAsync(
        IPortalRepository portal, IReadOnlyList<string> tokens, Guid? useId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        (Domain.Portal.PinUse Use, Pin Pin)? chosen = null;
        foreach (var token in tokens)
        {
            if (await portal.FindUseAsync(PortalValues.HashToken(token), cancellationToken).ConfigureAwait(false) is { } found
                && found.Use.IsOpenAt(now)
                && found.Pin.State is not (PinState.Revoked or PinState.Suspended)
                && (useId is null || found.Use.Id == useId))
            {
                chosen = found;
            }
        }

        return chosen;
    }
}
