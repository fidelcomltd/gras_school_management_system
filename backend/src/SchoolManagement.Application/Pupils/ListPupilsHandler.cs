using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils;

/// <summary>Handles <see cref="ListPupilsQuery"/>.</summary>
/// <remarks>
/// PRIVILEGE AND SCOPE ARE DATA-DEPENDENT, so <c>PupilEndpoints</c> maps this route with
/// <c>RequireAuthenticatedCaller()</c> rather than <c>RequirePrivilege(...)</c> — see
/// <see cref="PupilAccessGuard"/>'s remarks for why <c>ScopeParameterKind.Pupil</c> cannot be used
/// here.
/// </remarks>
internal sealed class ListPupilsQueryHandler(
    IPupilRepository pupils,
    IEffectivePrivilegeProvider grantsProvider,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<ListPupilsQuery, Result<CursorPage<PupilDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<PupilDto>>> HandleAsync(
        ListPupilsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<CursorPage<PupilDto>>(
                Error.Unauthenticated("authentication.required", "Sign in to perform this action."));
        }

        if (request.Cursor is not null && !PupilListCursor.TryDecode(request.Cursor, out _, out _))
        {
            return Result.Failure<CursorPage<PupilDto>>(Error.Validation(
                "pupils.invalid_cursor", "The cursor is invalid or has expired. Start again from the first page."));
        }

        var grants = await grantsProvider.GetGrantsAsync(userId, cancellationToken).ConfigureAwait(false);
        var scope = PupilAccessGuard.Resolve(grants, Privileges.Pupil.View);

        if (scope == PupilAccessScope.Forbidden)
        {
            return Result.Failure<CursorPage<PupilDto>>(Error.Forbidden(
                "pupil.view_forbidden", "You do not hold the privilege required to list pupils."));
        }

        if (scope == PupilAccessScope.ArmRestricted)
        {
            // No pupil carries an arm reference until enrolment exists (TASK-0050's own boundary —
            // see PupilAccessGuard's remarks), so an arm-scoped grant matches no row today. Honest
            // empty page, not a 403: the caller DOES hold the privilege, just over arms that (today)
            // contain nothing.
            return Result.Success(new CursorPage<PupilDto>([], null));
        }

        var pageSize = Math.Clamp(request.PageSize ?? CursorPageRequest.DefaultPageSize, 1, CursorPageRequest.MaxPageSize);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var page = await pupils
            .ListAsync(request.Status, request.Search, request.Cursor, pageSize, today, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(page);
    }
}
