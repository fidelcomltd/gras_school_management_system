using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// School settings (TASK-0005a; spec 6.2.3, 6.2.9, 6.2.12). Approved contract delta:
/// <c>.agent/decisions/2026-Q3-contract-deltas.md</c> § "TASK-0005 — School settings delta" — build
/// exactly what it specifies. Only the <c>identity</c> group and the <c>config_version</c> read
/// endpoints exist here; TASK-0005b (uploads) and TASK-0005c (registration number, abbreviation)
/// extend the same <see cref="SettingsDto"/> and reuse this same ledger.
/// </summary>
/// <remarks>
/// <c>PATCH /settings/identity</c> is the ONLY mutating route in this file, and it calls both
/// <c>.RequireCsrfToken()</c> (CLAUDE.md §5) and <c>.RequireIdempotencyKey(required: false)</c>
/// (approved delta: accepted, not required — a retry converges the final field values, but would
/// otherwise double the <see cref="SchoolManagement.Domain.Settings.ConfigVersion"/> row and the
/// audit event).
/// </remarks>
public sealed class SettingsEndpoints : IEndpointModule
{
    private const string Tag = "Settings";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var settingsGroup = endpoints
            .MapGroup("/settings")
            .WithTags(Tag);

        MapGetSettings(settingsGroup);
        MapUpdateIdentity(settingsGroup);
        MapUpdateRegNumber(settingsGroup);
        MapGetRegNumberPreview(settingsGroup);
        MapUpdateAbbreviation(settingsGroup);
        MapUpdateGrading(settingsGroup);
        MapResetGrading(settingsGroup);
        MapUpdateAssessment(settingsGroup);
        MapGetResultRules(settingsGroup);
        MapUpdateResultRules(settingsGroup);
        MapUpdateRatingScales(settingsGroup);
        MapUpdateDevelopmentDomains(settingsGroup);

        var configVersionsGroup = endpoints
            .MapGroup("/config-versions")
            .WithTags(Tag);

        MapListConfigVersions(configVersionsGroup);
        MapGetConfigVersion(configVersionsGroup);
    }

    private static void MapGetSettings(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetSettingsQuery(), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.View)
            .WithName("GetSettings")
            .WithSummary("Read the school settings")
            .WithDescription(
                "Everything in one payload for the settings area (spec 6.2.12). Returns only the " +
                "`identity` group as of TASK-0005a; later cards extend this same envelope additively " +
                "with sibling groups.")
            .Produces<SettingsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateIdentity(RouteGroupBuilder group) =>
        group.MapPatch("/identity", async (
                UpdateSchoolIdentityCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.IdentityUpdate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("UpdateSchoolIdentity")
            .WithSummary("Update the school's identity")
            .WithDescription(
                "School name, short name, address, phone, email, motto, head teacher name (spec " +
                "6.2.3). `timezone` and `abbreviation` are not editable here — timezone is fixed, and " +
                "the abbreviation has its own endpoint and its own optimistic-concurrency pointer. " +
                "`expectedVersion` must match the identity group's current `versionNumber` (from " +
                "`GET /settings`) or the save is rejected `409` before anything is written, and BOTH " +
                "the winning and the losing attempt are recorded on the audit trail (spec 6.2.11).")
            .Produces<SettingsIdentityGroupDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateRegNumber(RouteGroupBuilder group) =>
        group.MapPatch("/reg-number", async (
                UpdateRegNumberCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.RegNumberUpdate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("UpdateRegNumber")
            .WithSummary("Update the registration-number pattern")
            .WithDescription(
                "Separator, serial width, reset rule (spec 6.2.4). `yearSource` is fixed and not " +
                "editable here. Reducing `serialWidth` below what the counter partition currently " +
                "active under the SAVED `serialReset` already needs is rejected `409`, naming the " +
                "real serial and the minimum width that fits it (spec 6.2.10). `expectedVersion` " +
                "must match the reg-number group's current `versionNumber` (from `GET /settings`) or " +
                "the save is rejected `409` before anything is written.")
            .Produces<SettingsRegNumberGroupDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGetRegNumberPreview(RouteGroupBuilder group) =>
        group.MapGet("/reg-number/preview", async (
                [FromQuery] string separator,
                [FromQuery] int serialWidth,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(
                    new GetRegNumberPreviewQuery(separator, serialWidth),
                    cancellationToken);

                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.View)
            .WithName("GetRegNumberPreview")
            .WithSummary("Preview the next registration number under unsaved parameters")
            .WithDescription(
                "`separator`/`serialWidth` are UNSAVED — supplied as query parameters, not read from " +
                "settings. The abbreviation and the counter partition (which the currently SAVED " +
                "`serialReset` selects, spec 6.2.4) both come from saved configuration. Uses the next " +
                "serial that would actually be issued; with an empty register that is serial 1.")
            .Produces<RegNumberPreviewDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateAbbreviation(RouteGroupBuilder group) =>
        group.MapPatch("/abbreviation", async (
                UpdateAbbreviationCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.AbbreviationUpdate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("UpdateAbbreviation")
            .WithSummary("Change the school's registration-number abbreviation")
            .WithDescription(
                "Requires the literal confirmation token `CHANGE` and a reason (spec 6.2.4). " +
                "Rewrites no issued number — the counter is keyed on admission year alone, never the " +
                "abbreviation, so a mid-session change neither restarts the serial nor produces two " +
                "pupils whose numbers differ only by prefix. A value already used historically is " +
                "allowed (spec 6.2.11). `expectedVersion` must match the abbreviation group's " +
                "current `versionNumber` or the save is rejected `409` before anything is written.")
            .Produces<SettingsAbbreviationGroupDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateGrading(RouteGroupBuilder group) =>
        group.MapPut("/grading", async (
                UpdateGradingCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.GradingUpdate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("UpdateGrading")
            .WithSummary("Replace the grading scale")
            .WithDescription(
                "Whole scale as one array, atomic (spec 6.2.5, 6.2.12) — a band omitted from the " +
                "array is deleted. All ten save-time rules run over the whole submitted scale as one " +
                "unit; on the first failure nothing is written and the response's `bandIndex` " +
                "extension names the offending band's position in the submitted array. " +
                "`expectedVersion` must match the grading group's current `versionNumber` (from " +
                "`GET /settings`) or the save is rejected `409` before anything is written. `reason` " +
                "is required, at least ten characters, only when a result set is Published in the " +
                "active session (spec 6.2.9); otherwise it is ignored.")
            .Produces<SettingsGradingGroupDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapResetGrading(RouteGroupBuilder group) =>
        group.MapPost("/grading/reset", async (
                ResetGradingCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.ResetDefaults)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("ResetGrading")
            .WithSummary("Restore the grading scale to its seeded defaults")
            .WithDescription(
                "Restores the nine seeded bands (spec 6.2.13). Treated as an ordinary edit under " +
                "spec 6.2.9/6.2.11 — the same `expectedVersion`/`reason` contract as " +
                "`PUT /settings/grading` applies.")
            .Produces<SettingsGradingGroupDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateAssessment(RouteGroupBuilder group) =>
        group.MapPut("/assessment", async (
                UpdateAssessmentCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.AssessmentUpdate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("UpdateAssessment")
            .WithSummary("Replace the assessment structure")
            .WithDescription(
                "Whole structure as one array, atomic (spec 6.2.6, 6.2.12). A component's `id`, when " +
                "supplied, must match an existing component — that is how a rename/reorder is told " +
                "apart from an add or a remove, which matters once the session lock engages. Once a " +
                "mark has been entered anywhere in the active session, adding, removing, or changing " +
                "an existing component's `maxMark`/`isExamination` is rejected `409`; renaming and " +
                "reordering stay allowed (spec 6.2.6). `expectedVersion` must match the assessment " +
                "group's current `versionNumber` or the save is rejected `409` before anything is " +
                "written. `reason` is required, at least ten characters, only when a result set is " +
                "Published in the active session (spec 6.2.9); otherwise it is ignored.")
            .Produces<SettingsAssessmentGroupDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGetResultRules(RouteGroupBuilder group) =>
        group.MapGet("/result-rules", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetResultRulesQuery(), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.View)
            .WithName("GetResultRules")
            .WithSummary("Read the result rules")
            .WithDescription(
                "Tie-break, position scope, level position, pass mark and minimum subjects for " +
                "position the computation engine reads (spec 6.2.8). A fresh database returns 6.2.8's " +
                "seeded defaults, with `coreSubjectIds` empty until the administrator sets it.")
            .Produces<ResultRulesDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateResultRules(RouteGroupBuilder group) =>
        group.MapPut("/result-rules", async (
                UpdateResultRulesCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.ResultRulesUpdate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("UpdateResultRules")
            .WithSummary("Replace the result rules")
            .WithDescription(
                "The whole row as one save (spec 6.2.8). `expectedVersion` must match the result-rules " +
                "group's current `versionNumber` (from `GET /settings/result-rules`) or the save is " +
                "rejected `409 settings.resultrules.stale_version` before anything is written. " +
                "`primaryPositionScope` and `tieBreakRule` are rejected `409 settings.resultrules.locked` " +
                "once any result set is Published in the active session; `annualMethod` and the three " +
                "weights are rejected the same way once Third Term is published for any arm (spec " +
                "6.2.10) — changing a locked field to its current value is not a change and is never " +
                "refused. `reason` is required, at least ten characters, only when a result set is " +
                "Published in the active session (spec 6.2.9); otherwise it is ignored.")
            .Produces<ResultRulesDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateRatingScales(RouteGroupBuilder group) =>
        group.MapPut("/rating-scales", async (
                UpdateRatingScalesCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.RatingScalesUpdate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("UpdateRatingScales")
            .WithSummary("Replace the rating scales")
            .WithDescription(
                "Whole set as one array, atomic (spec 6.2.13). A scale's or point's `id`, when " +
                "supplied, must match an existing row — that is how a rename/reorder is told apart " +
                "from an add or a remove, matching `PUT /settings/assessment`'s own convention, and " +
                "matters because stage 2/3 rating blocks reference a scale BY that id. An existing " +
                "scale whose id is absent from the submitted array is removed, rejected " +
                "`409 settings.ratingscales.in_use` when a rating block still references it; an id " +
                "that matches no current row is rejected `422 settings.ratingscales.unknown_scale_id` " +
                "/ `settings.ratingscales.unknown_point_id`. All other save-time rules run over the " +
                "whole submitted set as one unit; on the first failure nothing is written and the " +
                "response's `scaleIndex`/`pointIndex` extensions name the offending position in the " +
                "submitted array. `expectedVersion` must match the rating-scales group's current " +
                "`versionNumber` (from `GET /settings`) or the save is rejected `409` before anything " +
                "is written. `reason` is required, at least ten characters, only when a result set is " +
                "Published in the active session (spec 6.2.9); otherwise it is ignored.")
            .Produces<SettingsRatingScaleGroupDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateDevelopmentDomains(RouteGroupBuilder group) =>
        group.MapPut("/development-domains", async (
                UpdateDevelopmentDomainsCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Settings.DevelopmentDomainsUpdate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("UpdateDevelopmentDomains")
            .WithSummary("Replace the nursery development domains and their indicators")
            .WithDescription(
                "Whole set as one array, atomic (spec 6.2.13). A domain's or indicator's `id`, when " +
                "supplied, must match an existing row — that is how a rename/reorder/archive is told " +
                "apart from an add or a remove, matching `PUT /settings/rating-scales`'s own " +
                "convention. An unknown `sectionId` or `ratingScaleId` is rejected " +
                "`422 settings.developmentdomains.unknown_section_id` / `unknown_rating_scale_id`, " +
                "naming the offending domain's `domainIndex`; an unknown domain or indicator `id` is " +
                "rejected `422 settings.developmentdomains.unknown_domain_id` / `unknown_indicator_id`. " +
                "A duplicate domain name within the same section, or a duplicate indicator name within " +
                "a domain, is rejected `422 settings.developmentdomains.duplicate_name` / " +
                "`duplicate_indicator_name` — the same domain name is allowed in a different section. " +
                "Archiving a submitted id is always allowed and never gated; an EXISTING domain or " +
                "indicator whose id is absent from the submission is being removed, and is refused " +
                "`409 settings.developmentdomains.indicator_rated` if it, or any indicator of an " +
                "omitted domain, has ever been rated — archive it instead. `expectedVersion` must " +
                "match the development-domains group's current `versionNumber` (from `GET /settings`) " +
                "or the save is rejected `409` before anything is written. `reason` is required, at " +
                "least ten characters, only when a result set is Published in the active session " +
                "(spec 6.2.9); otherwise it is ignored.")
            .Produces<SettingsDevelopmentDomainGroupDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapListConfigVersions(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null) =>
            {
                var result = await sender.SendAsync(
                    new ListConfigVersionsQuery(cursor, pageSize),
                    cancellationToken);

                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Audit.View)
            .WithName("ListConfigVersions")
            .WithSummary("List configuration-version history, newest first")
            .WithDescription(
                "Cursor-paginated per spec 9.5 — never offset. `cursor` is the opaque `nextCursor` " +
                "from a previous page; omit it for the first page. `pageSize` defaults to " +
                $"{CursorPageRequest.DefaultPageSize} and is capped at {CursorPageRequest.MaxPageSize}.")
            .Produces<CursorPage<ConfigVersionSummaryDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGetConfigVersion(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetConfigVersionQuery(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Audit.View)
            .WithName("GetConfigVersion")
            .WithSummary("Read one configuration version in full, including its snapshot")
            .WithDescription(
                "Includes the full `snapshot` — the whole serialised configuration as of this save " +
                "(spec 6.2.9), not only the group that changed.")
            .Produces<ConfigVersionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
