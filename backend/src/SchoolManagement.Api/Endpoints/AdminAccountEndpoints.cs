using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Auth.AdminAccounts;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Admin account management (TASK-0027; spec 6.1.2, 6.1.3, 6.1.7-6.1.14). Approved contract delta:
/// <c>.agent/decisions/2026-Q3-contract-deltas.md</c> entry <c>TASK-0019/0027</c>, Part 2 — build
/// exactly what it specifies. Roles, assignments, <c>GET /privileges</c> and 6.1.7 rules 1-3 are
/// deliberately absent: TASK-0028 (approved delta B4).
/// </summary>
/// <remarks>
/// <para>
/// TWO ROUTES ARE MAPPED WITH <c>RequireAuthenticatedCaller()</c> RATHER THAN
/// <c>RequirePrivilege(...)</c>: <c>PATCH /{id}</c> and <c>POST /{id}/status</c>. Both have a
/// DATA-DEPENDENT privilege requirement spec 6.1.2/6.1.10 state in terms of "the account itself" or
/// the requested transition, which a single route-declarative privilege cannot express — the
/// handlers resolve and enforce the exact requirement instead. Every other route here has one fixed
/// privilege and is gated declaratively.
/// </para>
/// <para>
/// EVERY MUTATING ENDPOINT CALLS <c>.RequireCsrfToken()</c> (CLAUDE.md §5) and declares
/// <c>Idempotency-Key</c> per the approved delta's table: REQUIRED on create, ACCEPTED on the rest —
/// none of them are the "genuine duplicate create" class that made create's key required.
/// </para>
/// <para>
/// Per-user rate-limit partitioning (this card's <c>Program.cs</c> ordering fix) with the SENSITIVE
/// policy on every mutation and the group's DEFAULT policy on the two reads.
/// </para>
/// </remarks>
public sealed class AdminAccountEndpoints : IEndpointModule
{
    private const string Tag = "Admins";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/admins")
            .WithTags(Tag);

        MapCreate(group);
        MapList(group);
        MapGet(group);
        MapUpdate(group);
        MapChangeStatus(group);
        MapResetPassword(group);
        MapRevokeSessions(group);
    }

    private static void MapCreate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                CreateAdminAccountCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);

                return result.Match(response => TypedResults.Created($"/api/v1/admins/{response.Id}", response));
            })
            .RequirePrivilege(Privileges.Admin.Create)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateAdminAccount")
            .WithSummary("Create an administrator account")
            .WithDescription(
                "Spec 6.1.9 step 1: staff name, email and phone only — role assignment is a separate " +
                "step (TASK-0028) and an account with zero assignments can exist and sign in. Returns " +
                "the generated temporary password ONCE; it is never returned again by any endpoint. " +
                "`Idempotency-Key` is REQUIRED: a retry with the same key returns the same account " +
                "(with the temporary password redacted on the replay) instead of creating a second one.")
            .Produces<CreateAdminAccountResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapList(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null,
                [FromQuery] AdminAccountStatus? status = null,
                [FromQuery] string? search = null) =>
            {
                var result = await sender.SendAsync(
                    new ListAdminAccountsQuery(cursor, pageSize, status, search),
                    cancellationToken);

                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Admin.View)
            .WithName("ListAdminAccounts")
            .WithSummary("List administrator accounts")
            .WithDescription(
                "Cursor-paginated per spec 9.5 — never offset. Default sort is status ascending " +
                "(active first) then staff name ascending; deactivated accounts are excluded unless " +
                "`status` names them explicitly (spec 6.1.8). `search` matches staff name or email, " +
                "case-insensitively, by substring. Role, scope-arm and session filters are TASK-0028. " +
                $"`pageSize` defaults to {CursorPageRequest.DefaultPageSize} and is capped at " +
                $"{CursorPageRequest.MaxPageSize}.")
            .Produces<CursorPage<AdminAccountSummaryDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGet(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetAdminAccountQuery(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Admin.View)
            .WithName("GetAdminAccount")
            .WithSummary("Read one administrator account")
            .WithDescription(
                "Assignments, the resolved effective privilege set and the last ten audit events by " +
                "this account are TASK-0028 (approved delta B4) — this endpoint returns the account's " +
                "own fields only.")
            .Produces<AdminAccountDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdate(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateAdminAccountCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("UpdateAdminAccount")
            .WithSummary("Edit an administrator account")
            .WithDescription(
                "Spec 6.1.2: requires `admin.update`, OR the account editing ITSELF — and even then, " +
                "only `staffName` and `phone`; changing your own email still requires `admin.update`. " +
                "`isSuperAdmin` is settable only by a caller who already holds it (spec 6.1.7 rule 4); " +
                "a rejected attempt still writes an audit event. Session tokens rotate for the target " +
                "account when `isSuperAdmin` actually changes (spec 9.1). `Idempotency-Key` is " +
                "accepted, not required — a retry converges the same final state but would otherwise " +
                "double the audit event.")
            .Produces<AdminAccountDetailDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapChangeStatus(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/status", async (
                Guid id,
                ChangeAdminAccountStatusCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("ChangeAdminAccountStatus")
            .WithSummary("Suspend, reactivate or deactivate an administrator account")
            .WithDescription(
                "Spec 6.1.10: active/suspended requires `admin.suspend`; moving to deactivated " +
                "requires `admin.deactivate`; reactivating a DEACTIVATED account requires " +
                "`admin.deactivate` held by a Super Admin and does NOT restore revoked assignments. " +
                "A caller can never change their OWN status (an added safeguard, not a spec line — " +
                "see `backend/docs/ASSUMPTIONS.md` §2.16). `reason` is required, at least ten " +
                "characters, when moving to deactivated (spec 6.1.12). Suspension and deactivation " +
                "revoke the account's existing sessions immediately (spec 6.1.10); the at-least-one-" +
                "active-Super-Admin invariant (spec 4.1) is enforced transactionally under a row " +
                "lock, not a pre-flight read.")
            .Produces<AdminAccountDetailDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapResetPassword(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/password-reset", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new ResetAdminAccountPasswordCommand(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Admin.PasswordReset)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("ResetAdminAccountPassword")
            .WithSummary("Force a password reset for an administrator account")
            .WithDescription(
                "Spec 6.1.11: mints a new temporary password, sets `mustChangePassword`, and revokes " +
                "every active session for the account. Human §5 sign-off (2026-09-06): the " +
                "`admin.password.reset` grant is the whole gate — an acting admin may exercise this " +
                "against any other account, with no step-up re-authentication and no additional " +
                "Super-Admin requirement. Returns the temporary password ONCE; a redacted `null` " +
                "replays on a repeated `Idempotency-Key`.")
            .Produces<ResetAdminAccountPasswordResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapRevokeSessions(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}/sessions", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new RevokeAdminAccountSessionsCommand(id), cancellationToken);
                return result.Match(TypedResults.NoContent);
            })
            .RequirePrivilege(Privileges.Admin.SessionRevoke)
            .RequireCsrfToken()
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("RevokeAdminAccountSessions")
            .WithSummary("Revoke every active session for an administrator account")
            .WithDescription(
                "Spec 6.1.14. Human §5 sign-off (2026-09-06): the `admin.session.revoke` grant is the " +
                "whole gate, same ruling as the password-reset endpoint. Always `204`, including when " +
                "the account already has no active sessions.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
