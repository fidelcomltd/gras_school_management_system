/**
 * GENERATED — DO NOT EDIT.
 *
 * Produced from `contracts/openapi.json` by `openapi-typescript`. Regenerate with
 * `npm run generate:api` in frontend/ — never hand-edit this file. See
 * `src/api/README.md` for the pipeline and CLAUDE.md §3/§4.4 for why.
 */

export interface paths {
    "/api/v1/admins": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * List administrator accounts
         * @description Cursor-paginated per spec 9.5 — never offset. Default sort is status ascending (active first) then staff name ascending; deactivated accounts are excluded unless `status` names them explicitly (spec 6.1.8). `search` matches staff name or email, case-insensitively, by substring. Role, scope-arm and session filters are TASK-0028. `pageSize` defaults to 25 and is capped at 100.
         */
        get: operations["ListAdminAccounts"];
        put?: never;
        /**
         * Create an administrator account
         * @description Spec 6.1.9 step 1: staff name, email and phone only — role assignment is a separate step (TASK-0028) and an account with zero assignments can exist and sign in. Returns the generated temporary password ONCE; it is never returned again by any endpoint. `Idempotency-Key` is REQUIRED: a retry with the same key returns the same account (with the temporary password redacted on the replay) instead of creating a second one.
         */
        post: operations["CreateAdminAccount"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/admins/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Read one administrator account
         * @description Assignments, the resolved effective privilege set and the last ten audit events by this account are TASK-0028 (approved delta B4) — this endpoint returns the account's own fields only.
         */
        get: operations["GetAdminAccount"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        /**
         * Edit an administrator account
         * @description Spec 6.1.2: requires `admin.update`, OR the account editing ITSELF — and even then, only `staffName` and `phone`; changing your own email still requires `admin.update`. `isSuperAdmin` is settable only by a caller who already holds it (spec 6.1.7 rule 4); a rejected attempt still writes an audit event. Session tokens rotate for the target account when `isSuperAdmin` actually changes (spec 9.1). `Idempotency-Key` is accepted, not required — a retry converges the same final state but would otherwise double the audit event.
         */
        patch: operations["UpdateAdminAccount"];
        trace?: never;
    };
    "/api/v1/admins/{id}/status": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Suspend, reactivate or deactivate an administrator account
         * @description Spec 6.1.10: active/suspended requires `admin.suspend`; moving to deactivated requires `admin.deactivate`; reactivating a DEACTIVATED account requires `admin.deactivate` held by a Super Admin and does NOT restore revoked assignments. A caller can never change their OWN status (an added safeguard, not a spec line — see `backend/docs/ASSUMPTIONS.md` §2.16). `reason` is required, at least ten characters, when moving to deactivated (spec 6.1.12). Suspension and deactivation revoke the account's existing sessions immediately (spec 6.1.10); the at-least-one-active-Super-Admin invariant (spec 4.1) is enforced transactionally under a row lock, not a pre-flight read.
         */
        post: operations["ChangeAdminAccountStatus"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/admins/{id}/password-reset": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Force a password reset for an administrator account
         * @description Spec 6.1.11: mints a new temporary password, sets `mustChangePassword`, and revokes every active session for the account. Human §5 sign-off (2026-09-06): the `admin.password.reset` grant is the whole gate — an acting admin may exercise this against any other account, with no step-up re-authentication and no additional Super-Admin requirement. Returns the temporary password ONCE; a redacted `null` replays on a repeated `Idempotency-Key`.
         */
        post: operations["ResetAdminAccountPassword"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/admins/{id}/sessions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        /**
         * Revoke every active session for an administrator account
         * @description Spec 6.1.14. Human §5 sign-off (2026-09-06): the `admin.session.revoke` grant is the whole gate, same ruling as the password-reset endpoint. Always `204`, including when the account already has no active sessions.
         */
        delete: operations["RevokeAdminAccountSessions"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/csrf": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Issue a CSRF token
         * @description Sets the `__Host-XSRF-TOKEN` cookie and returns the same value in the response body. Call on app load, including a reload while already signed in — the cookie is bound to the caller's current session when one is live, and to an anonymous subject otherwise, so this never invalidates an existing session's ability to mutate. Also call it once before sign-in: sign-in is itself CSRF-protected, so this is what bootstraps the pair. Echo the returned value verbatim in an `X-CSRF-Token` header on every subsequent mutating `/auth/*` request; axios does this automatically via its `xsrfCookieName`/`xsrfHeaderName` configuration.
         */
        get: operations["GetCsrfToken"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/sign-in": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Sign in with email and password
         * @description Requires an `X-CSRF-Token` header matching the `__Host-XSRF-TOKEN` cookie from `GET /auth/csrf`. On success, sets the `__Host-Session` cookie and rotates the CSRF cookie to one bound to the new session. Rate-limited under the sensitive policy (spec 6.1.11). A wrong password, an unknown email, and a locked account given a wrong password all return the identical generic 401 body — `423` fires ONLY when the submitted password is correct and the account is currently locked, so only the real account holder ever learns that.
         */
        post: operations["SignIn"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/sign-out": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Sign out
         * @description Revokes the current session if one exists. Always `204`, including when called with no session or an already-dead one — a repeat sign-out is naturally idempotent. Still requires a valid CSRF token.
         */
        post: operations["SignOut"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/me": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Report the caller's own account and session state
         * @description Replaces `GET /api/v1/reference/whoami` (removed). Always `200` once authenticated — including while `mustChangePassword` is true, which is how the frontend learns the flag on a hard reload rather than only right after sign-in.
         */
        get: operations["GetMe"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/refresh": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Proactively extend the session's idle window
         * @description Extends the idle timeout ahead of expiry; does not rotate the session token (spec 9.1 rotates only on privilege and password change). Call this BEFORE a session goes stale — all three 401 variants are terminal here too, so a reactive 401 from any endpoint should route straight to sign-in rather than calling this. Subject to the must-change-password gate: returns `403 auth.password_change_required` while that flag is set, unlike `me`.
         */
        post: operations["RefreshSession"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/password": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Change the caller's own password
         * @description Requires the current password (a re-authentication check for a sensitive action, spec 9.1). Rotates the session token and revokes every OTHER active session for the account (spec 6.1.11) — this session survives. Rejects a new password matching any of the last five hashes. Rate-limited under the sensitive policy: `currentPassword` is an online guessing surface too.
         */
        post: operations["ChangePassword"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/privileges": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Read the privilege register, grouped by module
         * @description Every privilege the system understands (spec 4.4), grouped 4.4.1 through 4.4.6 in spec table order. Authenticated only — spec 6.1.14 requires no specific privilege to read this: every signed-in admin needs to see the full menu of privileges to understand what a role can be built from. NOT paged: this is a fixed, compile-time, 93-row register, not a growing list, so the usual cursor-pagination rule (spec 9.5) does not apply. Legacy `guardian.*` aliases never appear here — canonical codes only.
         */
        get: operations["GetPrivilegeRegister"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/reference/ping": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Check that the API is reachable
         * @description Echoes the supplied name with the server's UTC time and the API version. Useful as a smoke test of routing, serialisation and the request pipeline. Requires no authentication and touches no database.
         */
        get: operations["Ping"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/reference/records": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * List sample records, newest first
         * @description Returns one page of records in the standard pagination envelope. `page` is 1-based and defaults to 1; `pageSize` defaults to 20 and is capped at 100. A `pageSize` above the cap is REJECTED with 422 rather than silently reduced, so a client paging through results cannot skip rows while believing it read everything.
         */
        get: operations["ListSampleRecords"];
        put?: never;
        /**
         * Create a sample record
         * @description Creates a record and returns 201 with a `Location` header. The label must be unique among live (not soft-deleted) records; a duplicate returns 409. Runs inside a transaction that is rolled back if the command fails.
         */
        post: operations["CreateSampleRecord"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/reference/arms/{armId}/secure": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Read an arm-scoped resource, gated by the privilege substrate
         * @description Requires `arm.view`, scoped to the arm named in the path. Anonymous callers get 401; an authenticated caller who lacks the privilege, or who holds it only over a different arm, gets 403 with a generic body naming neither the privilege nor whether the arm exists.
         */
        get: operations["GetSecureArm"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/roles": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * List roles
         * @description Cursor-paginated per spec 9.5 — never offset. Archived roles are excluded unless `status` names them explicitly (spec 9.4's default-scope rule). `search` matches the role name, case-insensitively, by substring. `sort` is `name` (default) or `status`; `direction` is `asc` (default) or `desc`. `pageSize` defaults to 25 and is capped at 100.
         */
        get: operations["ListRoles"];
        put?: never;
        /**
         * Create a role
         * @description Spec 6.1.4: name, description and at least one privilege code. Rejects the reserved name `Super Admin`, case-insensitive, and any privilege code that does not resolve (after legacy `guardian.*` alias resolution) to the register, naming the offender. Spec 6.1.7 rule 2: every requested privilege must already be held by the caller — a new role starts with none, so every one requested counts as an addition. `Idempotency-Key` is REQUIRED: a retry with the same key returns the same role instead of creating a second one.
         */
        post: operations["CreateRole"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/roles/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Read one role
         * @description Returns the role's own fields — assignments and effective privileges are TASK-0030's detail-view additions, not this endpoint.
         */
        get: operations["GetRole"];
        put?: never;
        post?: never;
        /**
         * Delete a role
         * @description Spec 9.4: hard-deletes unconditionally today, because no `role_assignment` table exists yet, so nothing can ever have referenced a role (TASK-0030 adds the has-ever-been-assigned branch that archives instead). A system role returns 409.
         */
        delete: operations["DeleteRole"];
        options?: never;
        head?: never;
        /**
         * Edit a role
         * @description Every field is independently optional; an absent field is left unchanged (spec 6.1.4, 6.1.9). A system role (the seeded Super Admin) rejects the whole request with 409, regardless of which fields it touches. A `privileges` array REPLACES the whole set; spec 6.1.7 rule 2 applies only to codes newly present that were not already on the role — removal is unrestricted. `Idempotency-Key` is accepted, not required.
         */
        patch: operations["UpdateRole"];
        trace?: never;
    };
    "/api/v1/sessions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * List sessions
         * @description Cursor-paginated per spec 9.5, always sorted `name` descending — newest first (spec 6.3.8). `state` is the only filter. `pageSize` defaults to 25 and is capped at 100.
         */
        get: operations["ListSessions"];
        put?: never;
        /**
         * Create a session
         * @description Spec 6.3.5: creates the session AND its three terms in one transaction, all `upcoming`. Name must be `YYYY/YYYY` with the second year exactly the first plus one, and unique; dates must not overlap an existing session. `Idempotency-Key` is REQUIRED: a retry with the same key returns the same session instead of creating a second one.
         */
        post: operations["CreateSession"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sessions/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Read one session with its terms
         * @description Arms grouped by level, enrolment counts and the publication position (spec 6.3.8) are not yet in this response — they need Arm/Pupil/result sets, which do not exist in this codebase yet.
         */
        get: operations["GetSession"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        /**
         * Edit a session's name and dates
         * @description Spec 6.3.10: name and dates, while `upcoming` or `active` — a `closed` session returns 409. Every field is independently optional; an absent field is left unchanged. `Idempotency-Key` is accepted, not required.
         */
        patch: operations["UpdateSession"];
        trace?: never;
    };
    "/api/v1/settings": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Read the school settings
         * @description Everything in one payload for the settings area (spec 6.2.12). Returns only the `identity` group as of TASK-0005a; later cards extend this same envelope additively with sibling groups.
         */
        get: operations["GetSettings"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/settings/identity": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        /**
         * Update the school's identity
         * @description School name, short name, address, phone, email, motto, head teacher name (spec 6.2.3). `timezone` and `abbreviation` are not editable here — timezone is fixed, and the abbreviation has its own endpoint and its own optimistic-concurrency pointer. `expectedVersion` must match the identity group's current `versionNumber` (from `GET /settings`) or the save is rejected `409` before anything is written, and BOTH the winning and the losing attempt are recorded on the audit trail (spec 6.2.11).
         */
        patch: operations["UpdateSchoolIdentity"];
        trace?: never;
    };
    "/api/v1/config-versions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * List configuration-version history, newest first
         * @description Cursor-paginated per spec 9.5 — never offset. `cursor` is the opaque `nextCursor` from a previous page; omit it for the first page. `pageSize` defaults to 25 and is capped at 100.
         */
        get: operations["ListConfigVersions"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/config-versions/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Read one configuration version in full, including its snapshot
         * @description Includes the full `snapshot` — the whole serialised configuration as of this save (spec 6.2.9), not only the group that changed.
         */
        get: operations["GetConfigVersion"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/terms/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        /**
         * Edit a term's schedule
         * @description Spec 6.3.10: dates, label, times school opened, next resumption date. Every field is independently optional; an absent field is left unchanged. Times school opened is rejected once the term is `closed` (spec 6.3.6: printed on results already issued). `Idempotency-Key` is accepted, not required.
         */
        patch: operations["UpdateTerm"];
        trace?: never;
    };
    "/api/v1/terms/{id}/open": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Open a term
         * @description Spec 6.3.6: moves `upcoming` to `active`. Blocked unless the previous term in the session is closed (or this is ordinal 1 and no term anywhere is currently active). The rejection names the reason. Opening ordinal 1 also moves this session to `active` and the previously active session (if any) to `closed` (spec 6.3.5). The "at least one arm exists" precondition (spec 6.3.6) is NOT checked — Arm does not exist in this codebase yet, so `open` is more permissive than spec until that card lands.
         */
        post: operations["OpenTerm"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/terms/{id}/close": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Close a term
         * @description Spec 6.3.6, 6.3.9: moves `active` to `closed`, writing `closedAtUtc`/`closedBy`. Rejected if `timesSchoolOpened` is blank, naming the term. The result-set precondition (spec 6.3.6: blocked by Draft/Awaiting Approval/Approved sets) is NOT checked — result sets do not exist in this codebase yet, so `close` is more permissive than spec until that card lands.
         */
        post: operations["CloseTerm"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/terms/{id}/reopen": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Reopen a closed term
         * @description Spec 6.3.6: `term.close` PLUS `isSuperAdmin` (checked in the handler — not a privilege code), a reason of at least 10 characters, and refused outright if the following term has already been opened.
         */
        post: operations["ReopenTerm"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        /**
         * @description `GET /api/v1/admins/{id}` (spec 6.1.8). Deliberately omits assignments, the resolved
         *             effective privilege set and the last ten audit events by this account — the approved delta (B4)
         *             splits these to TASK-0028 along with roles and assignments themselves; adding them later is
         *             additive.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "staffName": "Ngozi Adeyemi",
         *       "email": "ngozi.adeyemi@example.com",
         *       "phone": "+2348012345678",
         *       "status": "Active",
         *       "isSuperAdmin": false,
         *       "mustChangePassword": false,
         *       "lastLoginAtUtc": "2026-08-03T09:30:00+00:00",
         *       "createdAtUtc": "2026-08-03T09:30:00+00:00"
         *     }
         */
        AdminAccountDetailDto: {
            /**
             * @description Opaque identifier.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description Display name.
             * @example Ngozi Adeyemi
             */
            staffName: string;
            /**
             * @description Login identifier.
             * @example ngozi.adeyemi@example.com
             */
            email: string;
            /**
             * @description `null` only for the pre-existing bootstrap account.
             * @example +2348012345678
             */
            phone: null | string;
            /** @description Active, suspended or deactivated (spec 6.1.10). */
            status: components["schemas"]["AdminAccountStatus"];
            /**
             * @description Whether the flag-bypass privilege path applies (spec 6.1.7 rule 4).
             * @example false
             */
            isSuperAdmin: boolean;
            /**
             * @description Whether the forced-change gate currently applies.
             * @example false
             */
            mustChangePassword: boolean;
            /**
             * Format: date-time
             * @description `null` if the account has never signed in.
             * @example 2026-08-03T09:30:00+00:00
             */
            lastLoginAtUtc: null | string;
            /**
             * Format: date-time
             * @description When the account was created.
             * @example 2026-08-03T09:30:00+00:00
             */
            createdAtUtc: string;
        };
        /**
         * @description Lifecycle state of an AdminAccount (spec 6.1.10). Only AdminAccountStatus.Active may
         *     sign in.
         * @example Active
         * @enum {unknown}
         */
        AdminAccountStatus: "Active" | "Suspended" | "Deactivated";
        /**
         * @description One row of `GET /api/v1/admins` (spec 6.1.8). Deliberately omits `rolesHeld` and
         *     `scopeSummary` — the approved delta (`decisions/2026-Q3-contract-deltas.md`, entry
         *     `TASK-0019/0027`, B4) splits roles and assignments to TASK-0028, which adds both fields
         *     additively once a `role_assignment` table exists to compute them from.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "staffName": "Ngozi Adeyemi",
         *       "email": "ngozi.adeyemi@example.com",
         *       "phone": "+2348012345678",
         *       "status": "Active",
         *       "isSuperAdmin": false,
         *       "mustChangePassword": false,
         *       "lastLoginAtUtc": "2026-08-03T09:30:00+00:00",
         *       "createdAtUtc": "2026-08-03T09:30:00+00:00"
         *     }
         */
        AdminAccountSummaryDto: {
            /**
             * @description Opaque identifier.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description Display name.
             * @example Ngozi Adeyemi
             */
            staffName: string;
            /**
             * @description Login identifier.
             * @example ngozi.adeyemi@example.com
             */
            email: string;
            /**
             * @description `null` only for the pre-existing bootstrap account.
             * @example +2348012345678
             */
            phone: null | string;
            /** @description Active, suspended or deactivated (spec 6.1.10). */
            status: components["schemas"]["AdminAccountStatus"];
            /**
             * @description Whether the flag-bypass privilege path applies (spec 6.1.7 rule 4).
             * @example false
             */
            isSuperAdmin: boolean;
            /**
             * @description Whether the forced-change gate currently applies.
             * @example false
             */
            mustChangePassword: boolean;
            /**
             * Format: date-time
             * @description `null` if the account has never signed in.
             * @example 2026-08-03T09:30:00+00:00
             */
            lastLoginAtUtc: null | string;
            /**
             * Format: date-time
             * @description When the account was created.
             * @example 2026-08-03T09:30:00+00:00
             */
            createdAtUtc: string;
        };
        /**
         * @description Shared response shape returned by `sign-in`, `me`, `refresh` and `password`
         *     (approved contract delta §0), so the frontend never needs a second round trip to learn its own
         *     state after any of the four.
         * @example {
         *       "accountId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "email": "admin@example.com",
         *       "staffName": "Chisom Maxwell",
         *       "isSuperAdmin": true,
         *       "mustChangePassword": false,
         *       "effectivePrivileges": [
         *         {
         *           "privilege": "admin.view",
         *           "scope": "SchoolWide",
         *           "armIds": []
         *         }
         *       ],
         *       "sessionExpiresAt": "2026-08-03T09:30:00+00:00",
         *       "sessionAbsoluteExpiresAt": "2026-08-03T09:30:00+00:00"
         *     }
         */
        AuthSessionResponse: {
            /**
             * @description Opaque identifier (root CLAUDE.md §8 — never parsed by the client).
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            accountId: string;
            /**
             * @description The account's login identifier.
             * @example admin@example.com
             */
            email: string;
            /**
             * @description Display name.
             * @example Chisom Maxwell
             */
            staffName: string;
            /**
             * @description Whether the flag-bypass privilege path applies (TASK-0003 §1).
             * @example true
             */
            isSuperAdmin: boolean;
            /**
             * @description Whether the forced-change gate currently applies to this account.
             * @example false
             */
            mustChangePassword: boolean;
            /**
             * @description The caller's resolved effective privilege set. For this card, populated only via the
             *     bool AuthSessionResponse.IsSuperAdmin flag path — see `SuperAdminFlagEffectivePrivilegeProvider`.
             * @example [
             *       {
             *         "privilege": "admin.view",
             *         "scope": "SchoolWide",
             *         "armIds": []
             *       }
             *     ]
             */
            effectivePrivileges: components["schemas"]["EffectivePrivilegeDto"][];
            /**
             * Format: date-time
             * @description The sooner of the session's idle and absolute deadlines, recomputed on every response.
             * @example 2026-08-03T09:30:00+00:00
             */
            sessionExpiresAt: string;
            /**
             * Format: date-time
             * @description The session's fixed absolute deadline (spec 6.1.11's 8-hour cap), set once at sign-in.
             * @example 2026-08-03T09:30:00+00:00
             */
            sessionAbsoluteExpiresAt: string;
        };
        /**
         * @description `POST /api/v1/admins/{id}/status` (spec 6.1.10, 6.1.14). The privilege required is
         *             DATA-DEPENDENT on Status — `admin.suspend` for the active/suspended pair,
         *             `admin.deactivate` for deactivation, and `admin.deactivate` HELD BY A SUPER ADMIN for
         *             reactivating a deactivated account — so this route is mapped with `RequireAuthenticatedCaller()`
         *             and the handler resolves the exact requirement.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "status": "Suspended",
         *       "reason": null
         *     }
         */
        ChangeAdminAccountStatusCommand: {
            /**
             * Format: uuid
             * @description The account whose status is changing.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /** @description The target status. */
            status: components["schemas"]["AdminAccountStatus"];
            /**
             * @description Required, at least ten characters, when Status is
             *     AdminAccountStatus.Deactivated (spec 6.1.12: "A reason is mandatory on:...
             *     admin deactivation").
             */
            reason: null | string;
        };
        /**
         * @description Self-service password change (spec 6.1.11, spec 6.1.14). Approved contract delta:
         *     `POST /api/v1/auth/password`. The account comes from the caller's own session — this is never
         *     how another account's password is changed (that is `admin.password.reset`, TASK-0019).
         * @example {
         *       "currentPassword": "correct horse battery staple 9",
         *       "newPassword": "another horse battery staple 4"
         *     }
         */
        ChangePasswordCommand: {
            /**
             * @description Must match the account's current password (re-authentication for a
             *                 sensitive action).
             * @example correct horse battery staple 9
             */
            currentPassword: string;
            /**
             * @description Must satisfy spec 6.1.11's composition rule and must not match any of the
             *                 last int AuthPolicy.PasswordHistoryLimit hashes (checked in the handler, which needs the
             *                 password hasher — not expressible as a synchronous FluentValidation rule).
             * @example another horse battery staple 4
             */
            newPassword: string;
        };
        /**
         * @description The full body of `GET /api/v1/config-versions/{id}`.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "versionNumber": 3,
         *       "changedGroup": "Identity",
         *       "actorAdminId": "0192f0c4-0000-7000-8000-000000000099",
         *       "reason": null,
         *       "createdAtUtc": "2026-08-03T09:30:00+00:00",
         *       "snapshot": {
         *         "schoolProfile": {
         *           "schoolName": "Golden Royal Ark School",
         *           "shortName": "GRAS",
         *           "abbreviation": "GRAS",
         *           "address": "12 Ark Crescent, Lekki, Lagos",
         *           "phone": "+2348012345678",
         *           "email": "info@goldenroyalark.example",
         *           "motto": "Excellence Through Character",
         *           "headTeacherName": "Chisom Maxwell",
         *           "timezone": "Africa/Lagos",
         *           "identityVersionNumber": 3,
         *           "abbreviationVersionNumber": 0
         *         }
         *       }
         *     }
         */
        ConfigVersionDetailDto: {
            /**
             * @description Opaque id.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * Format: int64
             * @description The globally monotonic version number (spec 6.2.9).
             * @example 3
             */
            versionNumber: number | string;
            /**
             * @description Which settings group this save changed.
             * @example Identity
             */
            changedGroup: string;
            /**
             * @description The acting administrator's id, or `null` for a system action.
             * @example 0192f0c4-0000-7000-8000-000000000099
             */
            actorAdminId: null | string;
            /**
             * @description The reason given for this save, or `null` when the changed group's rule does not
             *     require one (6.2.10) — always `null` for an `Identity` row.
             */
            reason: null | string;
            /**
             * Format: date-time
             * @description When this version was written.
             * @example 2026-08-03T09:30:00+00:00
             */
            createdAtUtc: string;
            /**
             * @description The whole serialised configuration as of this save (6.2.9) — every group's values at that moment,
             *     not only the one that changed.
             */
            snapshot: components["schemas"]["JsonElement"];
        };
        /**
         * @description One row of `GET /api/v1/config-versions`'s cursor-paged list — everything except the
         *     snapshot itself, which only the detail endpoint returns.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "versionNumber": 3,
         *       "changedGroup": "Identity",
         *       "actorAdminId": "0192f0c4-0000-7000-8000-000000000099",
         *       "createdAtUtc": "2026-08-03T09:30:00+00:00"
         *     }
         */
        ConfigVersionSummaryDto: {
            /**
             * @description Opaque id. Pass to `GET /api/v1/config-versions/{id}` for the full detail.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * Format: int64
             * @description The globally monotonic version number (spec 6.2.9).
             * @example 3
             */
            versionNumber: number | string;
            /**
             * @description Which settings group this save changed.
             * @example Identity
             */
            changedGroup: string;
            /**
             * @description The acting administrator's id, or `null` for a system action.
             * @example 0192f0c4-0000-7000-8000-000000000099
             */
            actorAdminId: null | string;
            /**
             * Format: date-time
             * @description When this version was written.
             * @example 2026-08-03T09:30:00+00:00
             */
            createdAtUtc: string;
        };
        /**
         * @description `POST /api/v1/admins` (spec 6.1.9 step 1, 6.1.14): "Step one takes staff name, email and
         *             phone and creates the account." Role assignment (step two) is TASK-0028 — an account with zero
         *             assignments can exist and sign in, per spec 6.1.9's own text.
         * @example {
         *       "staffName": "Ngozi Adeyemi",
         *       "email": "ngozi.adeyemi@example.com",
         *       "phone": "08012345678"
         *     }
         */
        CreateAdminAccountCommand: {
            /**
             * @description Two words minimum, letters/spaces/hyphens/apostrophes only (spec 6.1.3).
             * @example Ngozi Adeyemi
             */
            staffName: string;
            /**
             * @description Login identifier. Unique across active and suspended accounts (spec 6.1.3).
             * @example ngozi.adeyemi@example.com
             */
            email: string;
            /**
             * @description Nigerian format — `08012345678` or `+2348012345678`.
             * @example 08012345678
             */
            phone: string;
        };
        /**
         * @description The created account, including the one-time temporary password (spec 6.1.9: "displays it once on
         *     screen with a copy button, and never displays it again").
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "staffName": "Ngozi Adeyemi",
         *       "email": "ngozi.adeyemi@example.com",
         *       "phone": "+2348012345678",
         *       "status": "Active",
         *       "mustChangePassword": true,
         *       "createdAtUtc": "2026-08-03T09:30:00+00:00",
         *       "temporaryPassword": "aB3xQ9mK2pL7vN4wR8dT"
         *     }
         */
        CreateAdminAccountResponse: {
            /**
             * @description Opaque identifier.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description Display name.
             * @example Ngozi Adeyemi
             */
            staffName: string;
            /**
             * @description Login identifier.
             * @example ngozi.adeyemi@example.com
             */
            email: string;
            /**
             * @description Normalised `+234` form.
             * @example +2348012345678
             */
            phone: string;
            /** @description Always `Active` on creation (spec 6.1.10). */
            status: components["schemas"]["AdminAccountStatus"];
            /**
             * @description Always `true` on creation (spec 6.1.3).
             * @example true
             */
            mustChangePassword: boolean;
            /**
             * Format: date-time
             * @description When the account was created.
             * @example 2026-08-03T09:30:00+00:00
             */
            createdAtUtc: string;
            /**
             * @description The generated plaintext password. Present on the live response; REDACTED (`null`)
             *     on a stored idempotency replay — see RedactFromIdempotencyReplayAttribute and the
             *     approved delta's orchestrator amendment A2 (spec 6.1.9/6.1.14: shown once, never again — a replay
             *     that returned it verbatim would be a second display).
             * @example aB3xQ9mK2pL7vN4wR8dT
             */
            temporaryPassword: null | string;
        };
        /**
         * @description `POST /api/v1/roles` (spec 6.1.4; approved delta
         *             `.agent/decisions/2026-Q3-contract-deltas.md` entry `TASK-0028` §2). Rejects the reserved
         *             name `Super Admin`, case-insensitive, and an unknown privilege code naming the offender.
         *             Spec 6.1.7 rule 2 applies to every requested privilege (the role starts with none, so every one
         *             requested is an "add") — enforced by the handler, not this type.
         * @example {
         *       "name": "Class Teacher",
         *       "description": "Enters marks and views pupil records for an assigned arm.",
         *       "privileges": [
         *         "result.score.enter",
         *         "pupil.view"
         *       ]
         *     }
         */
        CreateRoleCommand: {
            /**
             * @description 1..60 characters.
             * @example Class Teacher
             */
            name: string;
            /**
             * @description 0..300 characters, or `null`.
             * @example Enters marks and views pupil records for an assigned arm.
             */
            description: null | string;
            /**
             * @description At least one privilege code. Legacy `guardian.*` aliases are accepted and resolved to their
             *     canonical replacement before storage.
             * @example [
             *       "result.score.enter",
             *       "pupil.view"
             *     ]
             */
            privileges: string[];
        };
        /**
         * @description REFERENCE SLICE — the minimal COMMAND. Copy this shape for anything that changes state.
         * @example {
         *       "label": "Term 1 timetable draft",
         *       "note": "Carried over from the previous academic year."
         *     }
         */
        CreateSampleRecordCommand: {
            /**
             * @description A short label for the record. Required, unique among live records.
             * @example Term 1 timetable draft
             */
            label: string;
            /**
             * @description An optional free-text note.
             * @example Carried over from the previous academic year.
             */
            note: null | string;
        };
        /**
         * @description Response to a successful CreateSampleRecordCommand.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40"
         *     }
         */
        CreateSampleRecordResponse: {
            /**
             * @description The new record's opaque identifier. Returned in the body as well as in the `Location`
             *     header, so a client need not parse the URL to learn it.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
        };
        /**
         * @description `POST /api/v1/sessions` (spec 6.3.5). Creates the session AND its three terms in one
         *             transaction — spec 6.3.5: "There is no route that creates a session without terms, because a
         *             session with two terms is not a state the school ever wants." CreateSessionHandler
         *             is the only place in this codebase that adds a row to the sessions table.
         * @example {
         *       "name": "2026/2027",
         *       "startDate": "2026-09-14",
         *       "endDate": "2027-07-25",
         *       "term1": {
         *         "startDate": "2026-09-14",
         *         "endDate": "2026-12-18",
         *         "nextResumptionDate": "2027-01-05"
         *       },
         *       "term2": {
         *         "startDate": "2027-01-05",
         *         "endDate": "2027-04-02",
         *         "nextResumptionDate": "2027-04-20"
         *       },
         *       "term3": {
         *         "startDate": "2027-04-20",
         *         "endDate": "2027-07-25",
         *         "nextResumptionDate": null
         *       }
         *     }
         */
        CreateSessionCommand: {
            /**
             * @description Format `YYYY/YYYY`; the second year must be exactly the first plus one. Unique.
             * @example 2026/2027
             */
            name: string;
            /**
             * Format: date
             * @description Must fall inside the first named year.
             * @example 2026-09-14
             */
            startDate: string;
            /**
             * Format: date
             * @description Must fall inside the second named year.
             * @example 2027-07-25
             */
            endDate: string;
            /** @description First Term's dates. */
            term1: components["schemas"]["CreateSessionTermInput"];
            /** @description Second Term's dates. */
            term2: components["schemas"]["CreateSessionTermInput"];
            /** @description Third Term's dates. */
            term3: components["schemas"]["CreateSessionTermInput"];
        };
        /**
         * @description One term's dates as supplied at session creation (spec 6.3.5: "The form takes the session name,
         *     then three rows of start date, end date and next resumption date"). The term's `name` is
         *     never taken from the client here — it defaults to "First/Second/Third Term" by ordinal and is
         *     editable afterwards via `PATCH /api/v1/terms/{id}` (spec 6.3.4).
         * @example {
         *       "startDate": "2026-09-14",
         *       "endDate": "2026-12-18",
         *       "nextResumptionDate": "2027-01-05"
         *     }
         */
        CreateSessionTermInput: {
            /**
             * Format: date
             * @description Inside the session's range; later than the previous term's end date.
             * @example 2026-09-14
             */
            startDate: string;
            /**
             * Format: date
             * @description Later than StartDate; earlier than the next term's start date.
             * @example 2026-12-18
             */
            endDate: string;
            /**
             * Format: date
             * @description May be left blank (spec 6.3.4: required only to publish, which does not exist yet).
             * @example 2027-01-05
             */
            nextResumptionDate: null | string;
        };
        /**
         * @description Response to `GET /api/v1/auth/csrf`.
         * @example {
         *       "csrfToken": "CfDJ8N-example-opaque-csrf-token-value"
         *     }
         */
        CsrfTokenResponse: {
            /**
             * @description Opaque token — also set as the `__Host-XSRF-TOKEN` cookie. Echo verbatim in an `X-CSRF-Token`
             *     header on every mutating `/auth/*` request.
             * @example CfDJ8N-example-opaque-csrf-token-value
             */
            csrfToken: string;
        };
        /**
         * @description The cursor-pagination response envelope (spec 9.5). string? CursorPage&lt;TItem&gt;.NextCursor is opaque to the
         *     client — it must be echoed back verbatim as the next request's cursor, and never parsed or
         *     constructed by hand.
         * @example {
         *       "items": [
         *         {
         *           "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *           "staffName": "Ngozi Adeyemi",
         *           "email": "ngozi.adeyemi@example.com",
         *           "phone": "+2348012345678",
         *           "status": "Active",
         *           "isSuperAdmin": false,
         *           "mustChangePassword": false,
         *           "lastLoginAtUtc": "2026-08-03T09:30:00+00:00",
         *           "createdAtUtc": "2026-08-03T09:30:00+00:00"
         *         }
         *       ],
         *       "nextCursor": "MHxuZ296aSBhZGV5ZW1pfDAxOTJmMGM0LTdjM2UtN2ExYi05ZjJkLTNiOGU1YTZjMWQ0MA=="
         *     }
         */
        CursorPageOfAdminAccountSummaryDto: {
            /**
             * @description The page of items, newest first. Empty (never null) when there is nothing more to return.
             * @example [
             *       {
             *         "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
             *         "staffName": "Ngozi Adeyemi",
             *         "email": "ngozi.adeyemi@example.com",
             *         "phone": "+2348012345678",
             *         "status": "Active",
             *         "isSuperAdmin": false,
             *         "mustChangePassword": false,
             *         "lastLoginAtUtc": "2026-08-03T09:30:00+00:00",
             *         "createdAtUtc": "2026-08-03T09:30:00+00:00"
             *       }
             *     ]
             */
            items: components["schemas"]["AdminAccountSummaryDto"][];
            /**
             * @description `null` when this is the last page.
             * @example MHxuZ296aSBhZGV5ZW1pfDAxOTJmMGM0LTdjM2UtN2ExYi05ZjJkLTNiOGU1YTZjMWQ0MA==
             */
            nextCursor: null | string;
        };
        /**
         * @description The cursor-pagination response envelope (spec 9.5). string? CursorPage&lt;TItem&gt;.NextCursor is opaque to the
         *     client — it must be echoed back verbatim as the next request's cursor, and never parsed or
         *     constructed by hand.
         * @example {
         *       "items": [
         *         {
         *           "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *           "versionNumber": 3,
         *           "changedGroup": "Identity",
         *           "actorAdminId": "0192f0c4-0000-7000-8000-000000000099",
         *           "createdAtUtc": "2026-08-03T09:30:00+00:00"
         *         }
         *       ],
         *       "nextCursor": "MQ=="
         *     }
         */
        CursorPageOfConfigVersionSummaryDto: {
            /**
             * @description The page of items, newest first. Empty (never null) when there is nothing more to return.
             * @example [
             *       {
             *         "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
             *         "versionNumber": 3,
             *         "changedGroup": "Identity",
             *         "actorAdminId": "0192f0c4-0000-7000-8000-000000000099",
             *         "createdAtUtc": "2026-08-03T09:30:00+00:00"
             *       }
             *     ]
             */
            items: components["schemas"]["ConfigVersionSummaryDto"][];
            /**
             * @description `null` when this is the last page.
             * @example MQ==
             */
            nextCursor: null | string;
        };
        /**
         * @description The cursor-pagination response envelope (spec 9.5). string? CursorPage&lt;TItem&gt;.NextCursor is opaque to the
         *     client — it must be echoed back verbatim as the next request's cursor, and never parsed or
         *     constructed by hand.
         * @example {
         *       "items": [
         *         {
         *           "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *           "name": "Class Teacher",
         *           "description": "Enters marks and views pupil records for an assigned arm.",
         *           "isSystem": false,
         *           "privileges": [
         *             "pupil.view",
         *             "result.score.enter"
         *           ],
         *           "status": "Active"
         *         }
         *       ],
         *       "nextCursor": "Y2xhc3MgdGVhY2hlch8wMTkyZjBjNC03YzNlLTdhMWItOWYyZC0zYjhlNWE2YzFkNDA="
         *     }
         */
        CursorPageOfRoleDto: {
            /**
             * @description The page of items, newest first. Empty (never null) when there is nothing more to return.
             * @example [
             *       {
             *         "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
             *         "name": "Class Teacher",
             *         "description": "Enters marks and views pupil records for an assigned arm.",
             *         "isSystem": false,
             *         "privileges": [
             *           "pupil.view",
             *           "result.score.enter"
             *         ],
             *         "status": "Active"
             *       }
             *     ]
             */
            items: components["schemas"]["RoleDto"][];
            /**
             * @description `null` when this is the last page.
             * @example Y2xhc3MgdGVhY2hlch8wMTkyZjBjNC03YzNlLTdhMWItOWYyZC0zYjhlNWE2YzFkNDA=
             */
            nextCursor: null | string;
        };
        /**
         * @description The cursor-pagination response envelope (spec 9.5). string? CursorPage&lt;TItem&gt;.NextCursor is opaque to the
         *     client — it must be echoed back verbatim as the next request's cursor, and never parsed or
         *     constructed by hand.
         * @example {
         *       "items": [
         *         {
         *           "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
         *           "name": "2026/2027",
         *           "startDate": "2026-09-14",
         *           "endDate": "2027-07-25",
         *           "state": "Active"
         *         }
         *       ],
         *       "nextCursor": "MjAyNi8yMDI3"
         *     }
         */
        CursorPageOfSessionDto: {
            /**
             * @description The page of items, newest first. Empty (never null) when there is nothing more to return.
             * @example [
             *       {
             *         "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
             *         "name": "2026/2027",
             *         "startDate": "2026-09-14",
             *         "endDate": "2027-07-25",
             *         "state": "Active"
             *       }
             *     ]
             */
            items: components["schemas"]["SessionDto"][];
            /**
             * @description `null` when this is the last page.
             * @example MjAyNi8yMDI3
             */
            nextCursor: null | string;
        };
        /**
         * @description One entry of IReadOnlyList&lt;EffectivePrivilegeDto&gt; AuthSessionResponse.EffectivePrivileges, mirroring PrivilegeGrant
         *                 minus its session id — that field is server-internal and never crosses the wire.
         * @example {
         *       "privilege": "admin.view",
         *       "scope": "SchoolWide",
         *       "armIds": []
         *     }
         */
        EffectivePrivilegeDto: {
            /**
             * @description The canonical privilege code.
             * @example admin.view
             */
            privilege: string;
            /** @description Whether this grant applies school-wide or over a named list of arms. */
            scope: components["schemas"]["ScopeType"];
            /**
             * @description The arms this grant covers. Empty when Scope is school-wide.
             * @example []
             */
            armIds: string[];
        };
        /**
         * @description An RFC 9457 problem response for a validation failure, returned with status 422. Extends the standard problem shape with `errors`: an object keyed by request property name, whose values are the messages for that property, suitable for attaching to form fields. A 400 (rather than 422) means the request itself could not be parsed.
         * @example {
         *       "type": "urn:schoolmanagement:error:request.validation_failed",
         *       "title": "Validation failed",
         *       "status": 422,
         *       "detail": "One or more validation errors occurred.",
         *       "instance": "/api/v1/reference/records",
         *       "errorCode": "request.validation_failed",
         *       "traceId": "0af7651916cd43dd8448eb211c80319c",
         *       "errors": {
         *         "Label": [
         *           "Label is required."
         *         ],
         *         "PageSize": [
         *           "PageSize must be at most 100."
         *         ]
         *       }
         *     }
         */
        HttpValidationProblemDetails: {
            /** @example urn:schoolmanagement:error:sample_record.label_taken */
            type?: null | string;
            /** @example Conflict with current state */
            title?: null | string;
            /**
             * Format: int32
             * @example 409
             */
            status?: null | number | string;
            /** @example A record with that label already exists. */
            detail?: null | string;
            /** @example /api/v1/reference/records */
            instance?: null | string;
            /**
             * @example {
             *       "Label": [
             *         "Label is required."
             *       ],
             *       "PageSize": [
             *         "PageSize must be at most 100."
             *       ]
             *     }
             */
            errors?: {
                [key: string]: string[];
            };
            /** @description Stable, machine-readable error code. Clients branch on this, never on `detail`. Absent when this response was produced directly by the framework rather than by this API's own result mapping. */
            errorCode?: string;
            /** @description Correlation id for this specific response occurrence. Present on every error response; quote it when reporting a problem. */
            traceId: string;
        };
        /**
         * @description The whole serialised configuration as of this version (spec 6.2.9) — free-form JSON, because every settings card adds its own section to the same snapshot shape. Read it as an opaque object; do not assume today's set of keys is complete.
         * @example {
         *       "schoolProfile": {
         *         "schoolName": "Golden Royal Ark School",
         *         "shortName": "GRAS",
         *         "abbreviation": "GRAS",
         *         "address": "12 Ark Crescent, Lekki, Lagos",
         *         "phone": "+2348012345678",
         *         "email": "info@goldenroyalark.example",
         *         "motto": "Excellence Through Character",
         *         "headTeacherName": "Chisom Maxwell",
         *         "timezone": "Africa/Lagos",
         *         "identityVersionNumber": 3,
         *         "abbreviationVersionNumber": 0
         *       }
         *     }
         */
        JsonElement: unknown;
        /**
         * @description The one pagination response envelope for the whole API. Consistency here is what lets the
         *     frontend write a single generic paging hook instead of one per endpoint.
         * @example {
         *       "items": [
         *         {
         *           "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *           "label": "Term 1 timetable draft",
         *           "note": "Carried over from the previous academic year.",
         *           "createdAtUtc": "2026-08-03T09:30:00+00:00",
         *           "modifiedAtUtc": null
         *         }
         *       ],
         *       "page": 2,
         *       "pageSize": 20,
         *       "totalCount": 137,
         *       "totalPages": 7,
         *       "hasNextPage": true,
         *       "hasPreviousPage": true
         *     }
         */
        PagedResultOfSampleRecordDto: {
            /**
             * @description The page of items. Empty (never null) when the page is past the end.
             * @example [
             *       {
             *         "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
             *         "label": "Term 1 timetable draft",
             *         "note": "Carried over from the previous academic year.",
             *         "createdAtUtc": "2026-08-03T09:30:00+00:00",
             *         "modifiedAtUtc": null
             *       }
             *     ]
             */
            items: components["schemas"]["SampleRecordDto"][];
            /**
             * Format: int32
             * @description The 1-based page number that produced this response.
             * @example 2
             */
            page: number | string;
            /**
             * Format: int32
             * @description The page size that produced this response, after clamping.
             * @example 20
             */
            pageSize: number | string;
            /**
             * Format: int64
             * @description Total matching rows across all pages. long because a table can exceed
             *     int rows, and discovering that in production is not the moment to find out.
             * @example 137
             */
            totalCount: number | string;
            /**
             * Format: int32
             * @description Total number of pages available at this page size. Zero when there are no rows.
             * @example 7
             */
            totalPages?: number | string;
            /**
             * @description Whether a page after this one exists.
             * @example true
             */
            hasNextPage?: boolean;
            /**
             * @description Whether a page before this one exists.
             * @example true
             */
            hasPreviousPage?: boolean;
        };
        /**
         * @description Response to a PingQuery. Confirms the service is reachable and that its clock,
         *     serialisation, and API version are what the caller expects.
         * @example {
         *       "message": "Hello, Ada.",
         *       "serverTimeUtc": "2026-08-03T09:30:00+00:00",
         *       "apiVersion": "1.0"
         *     }
         */
        PingResponse: {
            /**
             * @description A greeting echoing the supplied name.
             * @example Hello, Ada.
             */
            message: string;
            /**
             * Format: date-time
             * @description Server time when the request was handled. Always UTC with an explicit offset (ISO-8601), per the
             *     repo-wide rule that timestamps cross the wire as DateTimeOffset and are converted
             *     to a local zone only in the UI.
             * @example 2026-08-03T09:30:00+00:00
             */
            serverTimeUtc: string;
            /**
             * @description The API version that served the request, matching the URL segment — useful when a client is
             *     unsure which version a proxy routed it to.
             * @example 1.0
             */
            apiVersion: string;
        };
        /**
         * @description One row of the privilege register (spec 4.4).
         * @example {
         *       "code": "result.score.enter",
         *       "permits": "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction.",
         *       "scopable": true
         *     }
         */
        PrivilegeDescriptorDto: {
            /**
             * @description The canonical privilege code, for example `result.score.enter`. Never a legacy
             *     `guardian.*` alias — the register is canonical codes only.
             * @example result.score.enter
             */
            code: string;
            /**
             * @description Verbatim spec 4.4 "Permits" cell for this row.
             * @example Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction.
             */
            permits: string;
            /**
             * @description Whether the privilege may be granted over a list of arms rather than school-wide.
             * @example true
             */
            scopable: boolean;
        };
        /**
         * @description One module group of the privilege register (one of spec 4.4.1 through 4.4.6).
         * @example {
         *       "key": "results",
         *       "title": "Results",
         *       "privileges": [
         *         {
         *           "code": "result.score.enter",
         *           "permits": "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction.",
         *           "scopable": true
         *         }
         *       ]
         *     }
         */
        PrivilegeGroupDto: {
            /**
             * @description The group's stable key: `administration`, `settings`, `academic_structure`,
             *     `pupils_and_subjects`, `results` or `pins_and_reports`. Crosses the wire as a
             *     plain string (root CLAUDE.md §8) — a client must tolerate a key it does not recognise, since a
             *     seventh group is an additive change.
             * @example results
             */
            key: string;
            /**
             * @description Verbatim spec 4.4.x section heading, for example "Academic structure".
             * @example Results
             */
            title: string;
            /**
             * @description The group's privileges, in spec table order.
             * @example [
             *       {
             *         "code": "result.score.enter",
             *         "permits": "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction.",
             *         "scopable": true
             *       }
             *     ]
             */
            privileges: components["schemas"]["PrivilegeDescriptorDto"][];
        };
        /**
         * @description The privilege register, grouped 4.4.1 through 4.4.6 in spec table order.
         * @example {
         *       "groups": [
         *         {
         *           "key": "results",
         *           "title": "Results",
         *           "privileges": [
         *             {
         *               "code": "result.score.enter",
         *               "permits": "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction.",
         *               "scopable": true
         *             }
         *           ]
         *         }
         *       ]
         *     }
         */
        PrivilegeRegisterResponse: {
            /**
             * @description One entry per module group, in spec section order.
             * @example [
             *       {
             *         "key": "results",
             *         "title": "Results",
             *         "privileges": [
             *           {
             *             "code": "result.score.enter",
             *             "permits": "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction.",
             *             "scopable": true
             *           }
             *         ]
             *       }
             *     ]
             */
            groups: components["schemas"]["PrivilegeGroupDto"][];
        };
        /**
         * @description An RFC 9457 problem response. Returned for every error. Branch on the `errorCode` extension member — it is stable — and never on `detail`, which is human-readable prose that may be reworded. `traceId` identifies this specific occurrence in the server logs; quote it when reporting a problem. `type` is a stable URN of the form `urn:schoolmanagement:error:<code>`.
         * @example {
         *       "type": "urn:schoolmanagement:error:sample_record.label_taken",
         *       "title": "Conflict with current state",
         *       "status": 409,
         *       "detail": "A record with that label already exists.",
         *       "instance": "/api/v1/reference/records",
         *       "errorCode": "sample_record.label_taken",
         *       "traceId": "0af7651916cd43dd8448eb211c80319c"
         *     }
         */
        ProblemDetails: {
            /** @example urn:schoolmanagement:error:sample_record.label_taken */
            type?: null | string;
            /** @example Conflict with current state */
            title?: null | string;
            /**
             * Format: int32
             * @example 409
             */
            status?: null | number | string;
            /** @example A record with that label already exists. */
            detail?: null | string;
            /** @example /api/v1/reference/records */
            instance?: null | string;
            /** @description Stable, machine-readable error code. Clients branch on this, never on `detail`. Absent when this response was produced directly by the framework (for example a model-binding failure or an authentication challenge) rather than by this API's own result mapping. */
            errorCode?: string;
            /** @description Correlation id for this specific response occurrence. Present on every error response; quote it when reporting a problem. */
            traceId: string;
            /**
             * Format: date-time
             * @description UTC time the account's lockout ends (spec 6.1.11: five failed attempts locks it for fifteen minutes). Present only on the `423 Locked` response `POST /auth/sign-in` returns when the SUBMITTED password is correct but the account is currently locked — never on any other problem response.
             * @example 2026-08-03T09:30:00+00:00
             */
            lockedUntil?: string;
        };
        /**
         * @description `POST /api/v1/terms/{id}/reopen` (spec 6.3.6): Super Admin only, a reason of at least
         *             int Term.ReopenReasonMinLength characters, refused outright if the following term has
         *             already been opened.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "reason": "A mark was entered against the wrong subject and discovered after publication."
         *     }
         */
        ReopenTermCommand: {
            /**
             * Format: uuid
             * @description The closed term being reopened.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description At least 10 characters — spec 6.3.6's audit trail for why marks moved after close.
             * @example A mark was entered against the wrong subject and discovered after publication.
             */
            reason: string;
        };
        /**
         * @description The new one-time temporary password (spec 6.1.11: "displays it once").
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "temporaryPassword": "aB3xQ9mK2pL7vN4wR8dT"
         *     }
         */
        ResetAdminAccountPasswordResponse: {
            /**
             * @description The account.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description The generated plaintext password. Present on the live response; REDACTED (`null`)
             *     on a stored idempotency replay — see RedactFromIdempotencyReplayAttribute.
             * @example aB3xQ9mK2pL7vN4wR8dT
             */
            temporaryPassword: null | string;
        };
        /**
         * @description The wire shape of a role (spec 6.1.4; approved delta `.agent/decisions/2026-Q3-contract-deltas.md`
         *     entry `TASK-0028` §2). Returned by every role endpoint — create, get, list (as the item
         *     shape) and update all share this one DTO, matching how `AdminAccountDetailDto` is reused
         *     across its own family.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "name": "Class Teacher",
         *       "description": "Enters marks and views pupil records for an assigned arm.",
         *       "isSystem": false,
         *       "privileges": [
         *         "pupil.view",
         *         "result.score.enter"
         *       ],
         *       "status": "Active"
         *     }
         */
        RoleDto: {
            /**
             * @description Opaque identifier.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description 1..60 characters.
             * @example Class Teacher
             */
            name: string;
            /**
             * @description 0..300 characters, or `null`.
             * @example Enters marks and views pupil records for an assigned arm.
             */
            description: null | string;
            /**
             * @description True only for the seeded Super Admin role. A system role cannot be edited, renamed, deleted or
             *     have privileges removed (spec 6.1.4).
             * @example false
             */
            isSystem: boolean;
            /**
             * @description Canonical codes, at least one, sorted deterministically.
             * @example [
             *       "pupil.view",
             *       "result.score.enter"
             *     ]
             */
            privileges: string[];
            /**
             * @description RoleStatus.Archived roles are excluded from the default list (spec 9.4) but remain
             *             individually readable and editable (except by `PATCH`/`DELETE` only when
             *             IsSystem is true).
             */
            status: components["schemas"]["RoleStatus"];
        };
        /**
         * @description Lifecycle state of a Role (spec 6.1.4). "An archived role cannot be newly assigned
         *     but existing assignments continue until the session ends."
         * @example Active
         * @enum {unknown}
         */
        RoleStatus: "Active" | "Archived";
        /**
         * @description REFERENCE SLICE — read model for a sample record.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "label": "Term 1 timetable draft",
         *       "note": "Carried over from the previous academic year.",
         *       "createdAtUtc": "2026-08-03T09:30:00+00:00",
         *       "modifiedAtUtc": null
         *     }
         */
        SampleRecordDto: {
            /**
             * @description Opaque identifier. A STRING on the wire even though it is a GUID in the database, per the
             *     repo-wide rule that IDs are opaque to clients — that way the storage key type can change without
             *     breaking them.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description The record's short label.
             * @example Term 1 timetable draft
             */
            label: string;
            /**
             * @description The optional note, or `null` if none was supplied.
             * @example Carried over from the previous academic year.
             */
            note: null | string;
            /**
             * Format: date-time
             * @description When the record was created. UTC, ISO-8601 with offset.
             * @example 2026-08-03T09:30:00+00:00
             */
            createdAtUtc: string;
            /**
             * Format: date-time
             * @description When the record was last modified, or `null` if never.
             * @example 2026-08-03T09:30:00+00:00
             */
            modifiedAtUtc: null | string;
        };
        /**
         * @description How a role assignment's grant is bounded. Spec 4.2: "Scope is one of two things: school-wide,
         *     or a list of specific arms in a specific session."
         * @example SchoolWide
         * @enum {unknown}
         */
        ScopeType: "SchoolWide" | "ArmList";
        /**
         * @description The arm-scoped resource `GetSecureArm` returns once the privilege check passes.
         * @example {
         *       "armId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40"
         *     }
         */
        SecureArmResponse: {
            /**
             * Format: uuid
             * @description The arm named in the request path — the resolved scope target.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            armId: string;
        };
        /**
         * @description The detail shape of a session (spec 6.3.8, 6.3.10): its own fields plus its three terms. Arms
         *     grouped by level, enrolment counts and the publication position are deferred for the same reason
         *     as SessionDto; the promotion panel is all of TASK-0036.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
         *       "name": "2026/2027",
         *       "startDate": "2026-09-14",
         *       "endDate": "2027-07-25",
         *       "state": "Active",
         *       "terms": [
         *         {
         *           "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *           "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
         *           "ordinal": 1,
         *           "name": "First Term",
         *           "startDate": "2026-09-14",
         *           "endDate": "2026-12-18",
         *           "timesSchoolOpened": 62,
         *           "nextResumptionDate": "2027-01-05",
         *           "state": "Closed",
         *           "closedAtUtc": "2026-08-03T09:30:00+00:00",
         *           "closedBy": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d42"
         *         },
         *         {
         *           "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d43",
         *           "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
         *           "ordinal": 2,
         *           "name": "Second Term",
         *           "startDate": "2027-01-05",
         *           "endDate": "2027-04-02",
         *           "timesSchoolOpened": null,
         *           "nextResumptionDate": "2027-04-20",
         *           "state": "Active",
         *           "closedAtUtc": null,
         *           "closedBy": null
         *         },
         *         {
         *           "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d44",
         *           "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
         *           "ordinal": 3,
         *           "name": "Third Term",
         *           "startDate": "2027-04-20",
         *           "endDate": "2027-07-25",
         *           "timesSchoolOpened": null,
         *           "nextResumptionDate": null,
         *           "state": "Upcoming",
         *           "closedAtUtc": null,
         *           "closedBy": null
         *         }
         *       ]
         *     }
         */
        SessionDetailDto: {
            /**
             * @description Opaque identifier.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41
             */
            id: string;
            /**
             * @description `YYYY/YYYY`.
             * @example 2026/2027
             */
            name: string;
            /**
             * Format: date
             * @description Inside the first named year.
             * @example 2026-09-14
             */
            startDate: string;
            /**
             * Format: date
             * @description Inside the second named year.
             * @example 2027-07-25
             */
            endDate: string;
            /** @description upcoming, active or closed. The client tolerates an unknown member (§8). */
            state: components["schemas"]["SessionState"];
            /**
             * @description Exactly three, ordered by ordinal — a session is never created without them.
             * @example [
             *       {
             *         "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
             *         "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
             *         "ordinal": 1,
             *         "name": "First Term",
             *         "startDate": "2026-09-14",
             *         "endDate": "2026-12-18",
             *         "timesSchoolOpened": 62,
             *         "nextResumptionDate": "2027-01-05",
             *         "state": "Closed",
             *         "closedAtUtc": "2026-08-03T09:30:00+00:00",
             *         "closedBy": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d42"
             *       },
             *       {
             *         "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d43",
             *         "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
             *         "ordinal": 2,
             *         "name": "Second Term",
             *         "startDate": "2027-01-05",
             *         "endDate": "2027-04-02",
             *         "timesSchoolOpened": null,
             *         "nextResumptionDate": "2027-04-20",
             *         "state": "Active",
             *         "closedAtUtc": null,
             *         "closedBy": null
             *       },
             *       {
             *         "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d44",
             *         "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
             *         "ordinal": 3,
             *         "name": "Third Term",
             *         "startDate": "2027-04-20",
             *         "endDate": "2027-07-25",
             *         "timesSchoolOpened": null,
             *         "nextResumptionDate": null,
             *         "state": "Upcoming",
             *         "closedAtUtc": null,
             *         "closedBy": null
             *       }
             *     ]
             */
            terms: components["schemas"]["TermDto"][];
        };
        /**
         * @description The list-item shape of a session (spec 6.3.8). Arms, enrolled-pupil and publication counts are
         *     deliberately absent — spec 6.3.8 asks for them, but Arm/Pupil/result sets do not exist in this
         *     codebase yet, and this module ships nothing it cannot populate honestly (never a fabricated
         *     `0`). See TASK-0035's Log for the tracked deferral.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
         *       "name": "2026/2027",
         *       "startDate": "2026-09-14",
         *       "endDate": "2027-07-25",
         *       "state": "Upcoming"
         *     }
         */
        SessionDto: {
            /**
             * @description Opaque identifier.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41
             */
            id: string;
            /**
             * @description `YYYY/YYYY`.
             * @example 2026/2027
             */
            name: string;
            /**
             * Format: date
             * @description Inside the first named year.
             * @example 2026-09-14
             */
            startDate: string;
            /**
             * Format: date
             * @description Inside the second named year.
             * @example 2027-07-25
             */
            endDate: string;
            /** @description upcoming, active or closed. The client tolerates an unknown member (§8). */
            state: components["schemas"]["SessionState"];
        };
        /**
         * @description Lifecycle of an AcademicSession (spec 6.3.3). Only one session system-wide may be
         *     SessionState.Active — enforced by a partial unique index (spec 6.3.9), not application code.
         * @example Active
         * @enum {unknown}
         */
        SessionState: "Upcoming" | "Active" | "Closed";
        /**
         * @description The response body of `GET /api/v1/settings`. Only SettingsIdentityGroupDto SettingsDto.Identity exists as of
         *     TASK-0005a; TASK-0005b and TASK-0005c extend this same envelope additively with sibling groups
         *     (logo/signature are read through SettingsIdentityGroupDto SettingsDto.Identity's own follow-up serving endpoints rather
         *     than a new top-level field, and registration-number/abbreviation get their own group here).
         * @example {
         *       "identity": {
         *         "schoolName": "Golden Royal Ark School",
         *         "shortName": "GRAS",
         *         "address": "12 Ark Crescent, Lekki, Lagos",
         *         "phone": "+2348012345678",
         *         "email": "info@goldenroyalark.example",
         *         "motto": "Excellence Through Character",
         *         "headTeacherName": "Chisom Maxwell",
         *         "timezone": "Africa/Lagos",
         *         "versionNumber": 3
         *       }
         *     }
         */
        SettingsDto: {
            /** @description The school identity group. */
            identity: components["schemas"]["SettingsIdentityGroupDto"];
        };
        /**
         * @description The school identity group, both inside SettingsDto and as
         *     `PATCH /api/v1/settings/identity`'s own success body (spec 6.2.3).
         * @example {
         *       "schoolName": "Golden Royal Ark School",
         *       "shortName": "GRAS",
         *       "address": "12 Ark Crescent, Lekki, Lagos",
         *       "phone": "+2348012345678",
         *       "email": "info@goldenroyalark.example",
         *       "motto": "Excellence Through Character",
         *       "headTeacherName": "Chisom Maxwell",
         *       "timezone": "Africa/Lagos",
         *       "versionNumber": 3
         *     }
         */
        SettingsIdentityGroupDto: {
            /**
             * @description Full school name. Appears in full on the result sheet header.
             * @example Golden Royal Ark School
             */
            schoolName: string;
            /**
             * @description Used where the full name will not fit, for example the pin slip.
             * @example GRAS
             */
            shortName: string;
            /**
             * @description Multi-line permitted.
             * @example 12 Ark Crescent, Lekki, Lagos
             */
            address: string;
            /**
             * @description Nigerian format, normalised to `+234` form.
             * @example +2348012345678
             */
            phone: string;
            /**
             * @description Valid email format, stored lower-invariant.
             * @example info@goldenroyalark.example
             */
            email: string;
            /**
             * @description `null` when unset. Printed under the school name if present.
             * @example Excellence Through Character
             */
            motto: null | string;
            /**
             * @description Printed above the head teacher's signature block.
             * @example Chisom Maxwell
             */
            headTeacherName: string;
            /**
             * @description Always `Africa/Lagos`. Fixed; a `PATCH` cannot change it.
             * @example Africa/Lagos
             */
            timezone: string;
            /**
             * Format: int32
             * @description The identity group's current optimistic-concurrency pointer. Echo this back as
             *     `expectedVersion` on the next `PATCH`.
             * @example 3
             */
            versionNumber: number | string;
        };
        /**
         * @description Signs an administrator in (spec 6.1.11, spec 9.1). Approved contract delta:
         *     `POST /api/v1/auth/sign-in`.
         * @example {
         *       "email": "admin@example.com",
         *       "password": "correct horse battery staple 9"
         *     }
         */
        SignInCommand: {
            /**
             * @description The account's login identifier. SignInCommandValidator DOES
             *                 check this is a well-formed email address — that check runs (and can reject) before the Argon2id
             *                 verify, but it does not reopen spec 6.1.11's timing concern: format validation happens identically
             *                 whether or not any account with that shape of address exists, so it cannot distinguish "known
             *                 email" from "unknown email" the way the Argon2id verify's presence/absence would.
             * @example admin@example.com
             */
            email: string;
            /**
             * @description The submitted plaintext password. Never logged (redacted by field name — see
             *                 `RedactSensitivePropertiesEnricher` — and never included in the request-logging behaviour's
             *                 output in the first place, since that behaviour logs only the request TYPE name).
             * @example correct horse battery staple 9
             */
            password: string;
        };
        /**
         * @description The wire shape of a term (spec 6.3.4), shared by every term-facing endpoint and nested inside
         *     SessionDetailDto.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "ordinal": 1,
         *       "name": "First Term",
         *       "startDate": "2026-09-14",
         *       "endDate": "2026-12-18",
         *       "timesSchoolOpened": null,
         *       "nextResumptionDate": "2027-01-05",
         *       "state": "Upcoming",
         *       "closedAtUtc": null,
         *       "closedBy": null
         *     }
         */
        TermDto: {
            /**
             * @description Opaque identifier.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description The owning session's id.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            sessionId: string;
            /**
             * Format: int32
             * @description 1, 2 or 3. Immutable.
             * @example 1
             */
            ordinal: number | string;
            /**
             * @description Editable label; logic keys off Ordinal, never this.
             * @example First Term
             */
            name: string;
            /**
             * Format: date
             * @description Inside the session's range.
             * @example 2026-09-14
             */
            startDate: string;
            /**
             * Format: date
             * @description Later than StartDate.
             * @example 2026-12-18
             */
            endDate: string;
            /**
             * Format: int32
             * @description `null` while blank; 1..200 once set; immutable once closed.
             */
            timesSchoolOpened: null | number | string;
            /**
             * Format: date
             * @description `null` until filled in.
             * @example 2027-01-05
             */
            nextResumptionDate: null | string;
            /** @description upcoming, active or closed. The client tolerates an unknown member (§8). */
            state: components["schemas"]["TermState"];
            /**
             * Format: date-time
             * @description `null` unless State is closed.
             * @example 2026-08-03T09:30:00+00:00
             */
            closedAtUtc: null | string;
            /** @description The admin who closed it, or `null`. */
            closedBy: null | string;
        };
        /**
         * @description Lifecycle of a Term (spec 6.3.4, 6.3.6). Only one term system-wide may be
         *     TermState.Active — enforced by a partial unique index (spec 6.3.9), not application code.
         * @example Upcoming
         * @enum {unknown}
         */
        TermState: "Upcoming" | "Active" | "Closed";
        /**
         * @description `PATCH /api/v1/admins/{id}` (spec 6.1.9, 6.1.14, 6.1.7 rule 4). Two authorisation shapes
         *             reach this one command: an `admin.update` holder editing any account (name, email, phone,
         *             and — rule 4 permitting — bool? UpdateAdminAccountCommand.IsSuperAdmin), or the account itself editing only its OWN
         *             string UpdateAdminAccountCommand.StaffName and string UpdateAdminAccountCommand.Phone (spec 6.1.2's self-edit carve-out — NOT email,
         *             which the caller must hold `admin.update` to change even on their own account).
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "staffName": "Ngozi Adeyemi-Bello",
         *       "email": "ngozi.adeyemi@example.com",
         *       "phone": "08012345678",
         *       "isSuperAdmin": null
         *     }
         */
        UpdateAdminAccountCommand: {
            /**
             * Format: uuid
             * @description The account being edited.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description Two words minimum, letters/spaces/hyphens/apostrophes only.
             * @example Ngozi Adeyemi-Bello
             */
            staffName: string;
            /**
             * @description Login identifier. Self-edit callers must submit the account's current value unchanged.
             * @example ngozi.adeyemi@example.com
             */
            email: string;
            /**
             * @description Nigerian format.
             * @example 08012345678
             */
            phone: string;
            /**
             * @description `null` to leave unchanged. A non-null value that differs from the account's
             *             current flag is rule 4 territory (spec 6.1.7): only an acting admin who already holds
             *             bool AdminAccount.IsSuperAdmin may change it, and a rejected attempt writes an audit
             *             event even though the request as a whole still fails.
             */
            isSuperAdmin: null | boolean;
        };
        /**
         * @description `PATCH /api/v1/roles/{id}` (spec 6.1.4, 6.1.9; approved delta entry `TASK-0028` §2):
         *             "`UpdateRoleRequest name?, description?, privileges?, status? (all optional; absent =
         *             unchanged)`." Every field is independently optional — `null` leaves that field
         *             untouched. To clear string? UpdateRoleCommand.Description to "no description," send an empty string rather
         *             than omitting the field: `null` here is indistinguishable from "not provided,"
         *             exactly like `UpdateAdminAccountCommand.IsSuperAdmin`'s own null-means-unchanged convention.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "name": "Senior Class Teacher",
         *       "description": null,
         *       "privileges": null,
         *       "status": null
         *     }
         */
        UpdateRoleCommand: {
            /**
             * Format: uuid
             * @description The role being edited.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description `null` to leave unchanged. Rejected if it is the reserved name, case-insensitive.
             * @example Senior Class Teacher
             */
            name: null | string;
            /** @description `null` to leave unchanged; an empty string clears it. */
            description: null | string;
            /**
             * @description `null` to leave unchanged. A non-null value REPLACES the whole set (never a diff) —
             *             spec 6.1.7 rule 2 applies only to codes newly present that were not already on the role.
             */
            privileges: null | string[];
            status: null | components["schemas"]["RoleStatus"];
        };
        /**
         * @description `PATCH /api/v1/settings/identity` (spec 6.2.3). string SchoolProfile.Abbreviation and
         *             string SchoolProfile.Timezone are deliberately absent from this body — the abbreviation is
         *             TASK-0005c's own endpoint, and the timezone is fixed and not editable in this version.
         * @example {
         *       "schoolName": "Golden Royal Ark School",
         *       "shortName": "GRAS",
         *       "address": "12 Ark Crescent, Lekki, Lagos",
         *       "phone": "08012345678",
         *       "email": "info@goldenroyalark.example",
         *       "motto": "Excellence Through Character",
         *       "headTeacherName": "Chisom Maxwell",
         *       "expectedVersion": 2
         *     }
         */
        UpdateSchoolIdentityCommand: {
            /**
             * @description Full school name.
             * @example Golden Royal Ark School
             */
            schoolName: string;
            /**
             * @description Used where the full name will not fit.
             * @example GRAS
             */
            shortName: string;
            /**
             * @description Multi-line permitted.
             * @example 12 Ark Crescent, Lekki, Lagos
             */
            address: string;
            /**
             * @description Nigerian format — `08012345678` or `+2348012345678`.
             * @example 08012345678
             */
            phone: string;
            /**
             * @description Valid email format.
             * @example info@goldenroyalark.example
             */
            email: string;
            /**
             * @description `null` to leave the school with no motto.
             * @example Excellence Through Character
             */
            motto: null | string;
            /**
             * @description Printed above the head teacher's signature block.
             * @example Chisom Maxwell
             */
            headTeacherName: string;
            /**
             * Format: int32
             * @description The identity group's current `versionNumber`, as last read from `GET /settings`. A
             *     stale value is rejected `409 settings.identity.stale_version` before anything is written.
             * @example 2
             */
            expectedVersion: number | string;
        };
        /**
         * @description `PATCH /api/v1/sessions/{id}` (spec 6.3.10): "Name and dates, while upcoming or active."
         *             Every field is independently optional; an absent field is left unchanged — the same convention
         *             `UpdateRoleCommand` established.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "name": "2026/2027",
         *       "startDate": null,
         *       "endDate": null
         *     }
         */
        UpdateSessionCommand: {
            /**
             * Format: uuid
             * @description The session being edited.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /**
             * @description `null` to leave unchanged.
             * @example 2026/2027
             */
            name: null | string;
            /**
             * Format: date
             * @description `null` to leave unchanged.
             */
            startDate: null | string;
            /**
             * Format: date
             * @description `null` to leave unchanged.
             */
            endDate: null | string;
        };
        /**
         * @description `PATCH /api/v1/terms/{id}` (spec 6.3.10): "Dates, label, times school opened, next
         *             resumption date." Every field is independently optional; an absent field is left unchanged.
         *             int? UpdateTermCommand.TimesSchoolOpened cannot be CLEARED back to blank through this command — spec never
         *             asks for that operation, only for filling it in once and (implicitly, spec 6.3.6) never touching
         *             it again after close.
         * @example {
         *       "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
         *       "name": null,
         *       "startDate": null,
         *       "endDate": null,
         *       "timesSchoolOpened": 118,
         *       "nextResumptionDate": "2027-01-05"
         *     }
         */
        UpdateTermCommand: {
            /**
             * Format: uuid
             * @description The term being edited.
             * @example 0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40
             */
            id: string;
            /** @description `null` to leave unchanged. */
            name: null | string;
            /**
             * Format: date
             * @description `null` to leave unchanged.
             */
            startDate: null | string;
            /**
             * Format: date
             * @description `null` to leave unchanged.
             */
            endDate: null | string;
            /**
             * Format: int32
             * @description `null` to leave unchanged; rejected once the term is closed.
             * @example 118
             */
            timesSchoolOpened: null | number | string;
            /**
             * Format: date
             * @description `null` to leave unchanged.
             * @example 2027-01-05
             */
            nextResumptionDate: null | string;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
    ListAdminAccounts: {
        parameters: {
            query?: {
                cursor?: string;
                pageSize?: number | string;
                status?: components["schemas"]["AdminAccountStatus"];
                search?: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CursorPageOfAdminAccountSummaryDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CreateAdminAccount: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Required on this route. */
                "Idempotency-Key": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CreateAdminAccountCommand"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CreateAdminAccountResponse"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetAdminAccount: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AdminAccountDetailDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    UpdateAdminAccount: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["UpdateAdminAccountCommand"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AdminAccountDetailDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ChangeAdminAccountStatus: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ChangeAdminAccountStatusCommand"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AdminAccountDetailDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ResetAdminAccountPassword: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ResetAdminAccountPasswordResponse"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    RevokeAdminAccountSessions: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description No Content */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetCsrfToken: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CsrfTokenResponse"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    SignIn: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["SignInCommand"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AuthSessionResponse"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Locked */
            423: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    SignOut: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description No Content */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetMe: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AuthSessionResponse"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    RefreshSession: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AuthSessionResponse"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ChangePassword: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ChangePasswordCommand"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AuthSessionResponse"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetPrivilegeRegister: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["PrivilegeRegisterResponse"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    Ping: {
        parameters: {
            query: {
                name: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["PingResponse"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Internal Server Error */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListSampleRecords: {
        parameters: {
            query?: {
                page?: number | string;
                pageSize?: number | string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["PagedResultOfSampleRecordDto"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Internal Server Error */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CreateSampleRecord: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CreateSampleRecordCommand"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CreateSampleRecordResponse"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Internal Server Error */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetSecureArm: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                armId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SecureArmResponse"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListRoles: {
        parameters: {
            query?: {
                cursor?: string;
                pageSize?: number | string;
                status?: components["schemas"]["RoleStatus"];
                search?: string;
                sort?: string;
                direction?: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CursorPageOfRoleDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CreateRole: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Required on this route. */
                "Idempotency-Key": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CreateRoleCommand"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["RoleDto"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetRole: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["RoleDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    DeleteRole: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description No Content */
            204: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    UpdateRole: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["UpdateRoleCommand"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["RoleDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListSessions: {
        parameters: {
            query?: {
                cursor?: string;
                pageSize?: number | string;
                state?: components["schemas"]["SessionState"];
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CursorPageOfSessionDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CreateSession: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Required on this route. */
                "Idempotency-Key": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CreateSessionCommand"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SessionDetailDto"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetSession: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SessionDetailDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    UpdateSession: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["UpdateSessionCommand"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SessionDetailDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetSettings: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SettingsDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    UpdateSchoolIdentity: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["UpdateSchoolIdentityCommand"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SettingsIdentityGroupDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListConfigVersions: {
        parameters: {
            query?: {
                cursor?: string;
                pageSize?: number | string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CursorPageOfConfigVersionSummaryDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetConfigVersion: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ConfigVersionDetailDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    UpdateTerm: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["UpdateTermCommand"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["TermDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    OpenTerm: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["TermDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CloseTerm: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["TermDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ReopenTerm: {
        parameters: {
            query?: never;
            header: {
                /** @description The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, approved contract delta §5). Obtain it from GET /auth/csrf or from a prior response's Set-Cookie. */
                "X-CSRF-Token": string;
                /** @description Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, no whitespace. Optional. A retry with the same key returns the stored response unchanged and sets the `Idempotency-Replay` response header, rather than repeating the request's effect. */
                "Idempotency-Key"?: string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ReopenTermCommand"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["TermDto"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Too Many Requests */
            429: {
                headers: {
                    /** @description Present and set to "true" only when this response is a replay of a prior request that used the same Idempotency-Key, rather than a fresh execution. */
                    "Idempotency-Replay"?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
}
