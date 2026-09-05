/**
 * GENERATED — DO NOT EDIT.
 *
 * Produced from `contracts/openapi.json` by `openapi-typescript`. Regenerate with
 * `npm run generate:api` in frontend/ — never hand-edit this file. See
 * `src/api/README.md` for the pipeline and CLAUDE.md §3/§4.4 for why.
 */

export interface paths {
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
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
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
        };
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
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
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
}
