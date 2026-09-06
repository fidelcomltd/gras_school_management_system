# Approved contract deltas — 2026 Q3

Full text of every contract delta approved under CLAUDE.md §3, moved out of its task card so the
card stays inside the ~120-line cap (§13). A card carries a one-line pointer here; the
implementing agent reads the entry its card names, not this whole file.

Each entry records the delta AS APPROVED. If implementation diverges, that is drift and belongs
in `.agent/drift/`, not an edit here. Append-only.

---

## TASK-0003 — Authentication and session management

**Approved 2026-09-05 by orchestrator.** Drafted by backend-dev over two dispatches; amended
after review for the `me`/`mustChangePassword` defect, the lockout-disclosure ruling and the
refresh semantics. Two human rulings are embedded and binding: Super Admin is a flag bypass
(4.2.2 vs 6.1.7 resolved in favour of 6.1.7), and `423` is returned only when the submitted
password is correct.

Classification: **BREAKING** — removes `GET /api/v1/reference/whoami`. Everything else additive.

## Contract delta — DRAFT, dispatch 1 (2026-09-05), awaiting orchestrator approval

BREAKING. Status codes use RFC 9457 `ProblemDetails`/`HttpValidationProblemDetails` throughout,
matching the existing `sample_record`/`authorization.forbidden` pattern (`errorCode`, `traceId`,
`type: urn:schoolmanagement:error:<code>`). A response is noted "framework-produced" where it
has no `errorCode`, per the existing schema convention.

### 0. Shared response shape — `AuthSessionResponse`

Returned (200) by sign-in, `me`, `refresh` and `password`, so the frontend never needs a second
round trip to learn its own state after any of the four.

```jsonc
{
  "accountId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",   // uuid
  "email": "admin@example.com",
  "staffName": "Chisom Maxwell",
  "isSuperAdmin": true,
  "mustChangePassword": false,
  "effectivePrivileges": [
    { "privilege": "admin.view", "scope": "SchoolWide", "armIds": [] }
  ],
  "sessionExpiresAt": "2026-09-05T14:30:00+00:00",         // sooner of idle/absolute, recomputed each response
  "sessionAbsoluteExpiresAt": "2026-09-05T18:00:00+00:00"  // fixed at sign-in, spec 6.1.11's 8h cap
}
```

`effectivePrivileges` mirrors `PrivilegeGrant` (`scope`: `SchoolWide`|`ArmList`, `armIds` empty
for `SchoolWide`) so the shape survives once TASK-0019+ gives non-super-admin accounts real
assignments. **For this card**, only the `is_super_admin` case is populated (see §1 below) — no
`role_assignment` persistence is built here.

### 1. `IEffectivePrivilegeProvider` — real implementation replaces `NullEffectivePrivilegeProvider`

Not a wire shape, but load-bearing for §0 above. **Decided (human ruling, 2026-09-05): `is_super_admin`
is a flag bypass**, not a `role_assignment` row — 6.1.7 rule 4 ("not part of any role's privilege
list") governs over 4.2.2's looser "Super Admin role, sessionless and permanent" wording. No
`role`/`role_assignment` persistence enters this card; **TASK-0019 owns real role assignment**
for non-super-admin accounts. The implementation: look up `admin_account` by `userId`; if
`is_super_admin`, return one `PrivilegeGrant` per privilege in the existing immutable register
(`SchoolWide`, `SessionId: null`); otherwise return `[]` (unchanged from today — no non-super
account exists until TASK-0019). This needs only `admin_account`, which this card builds anyway
for sign-in. **Now an explicit acceptance criterion** (see below), scoped to the super-admin
flag path only.

### 2. Endpoints

| Method | Path | Auth | CSRF | Success |
|---|---|---|---|---|
| GET | `/api/v1/auth/csrf` | anonymous | n/a (issues it) | 200 |
| POST | `/api/v1/auth/sign-in` | anonymous | required | 200 `AuthSessionResponse` |
| POST | `/api/v1/auth/sign-out` | optional (works with or without a session — see rationale) | required | 204 |
| GET | `/api/v1/auth/me` | required | n/a (GET) | 200 `AuthSessionResponse` |
| POST | `/api/v1/auth/refresh` | required | required | 200 `AuthSessionResponse` |
| POST | `/api/v1/auth/password` | required | required | 200 `AuthSessionResponse` |

**`GET /api/v1/auth/csrf`** — no request. Sets the CSRF cookie (§5) and returns
`{ "csrfToken": "<opaque string, echo verbatim in X-CSRF-Token>" }`. Exists so an anonymous
visitor can obtain a token *before* sign-in — sign-in is itself CSRF-protected, so something
must bootstrap the pair first. Called once by the frontend on app load.

**`POST /api/v1/auth/sign-in`** — request `{ "email": "string", "password": "string" }`
(both required, `email` format-validated). Success sets the session cookie (rotates the CSRF
cookie to one bound to the new session) and returns `AuthSessionResponse`. Failures:
- `422` — malformed request (`request.validation_failed`, standard field-keyed body).
- `401` — wrong password, unknown email, **or a locked account whose submitted password does
  NOT match** — **identical body for all three** (`auth.invalid_credentials`, detail "Login
  details are not correct.", verbatim from spec 6.1.11).
- `423 Locked` — **only when the submitted password is correct** and the account is within its
  15-minute lockout window (`auth.account_locked`; body adds a `lockedUntil` timestamp
  extension). **Decided (human ruling, 2026-09-05): `423` is returned in this one case, not
  "whenever locked."** An enumerator who doesn't have the real password gets the same generic
  `401` a locked account always gave before; only the legitimate account holder who eventually
  types the password correctly learns "locked until 14:35." **Implementation consequence: the
  Argon2id verify must run unconditionally, before branching on lock state** — skipping it for
  locked accounts (an obvious-looking optimisation) would turn response timing into the very
  oracle this design closes.
- `403` — `csrf.missing` / `csrf.invalid`.
- `429` — sensitive rate-limit policy (framework-produced, no `errorCode`).

**`POST /api/v1/auth/sign-out`** — no request. Revokes the session if one exists; **204 either
way**, including when called with no session or an already-dead one. Made deliberately
tolerant of that case — the card's own framing calls a repeat sign-out "naturally idempotent,"
and a client that thinks it might still have a session must always be able to clear it
successfully rather than getting a 401 loop. Still requires a valid CSRF token (mutations get
the CSRF filter uniformly, never per-endpoint, even where the forgery risk is low). Failures:
`403` `csrf.missing`/`csrf.invalid`, `429`.

**`GET /api/v1/auth/me`** — no request. **Replaces** `/api/v1/reference/whoami` (see §4). Always
`200` `AuthSessionResponse` once authenticated — **including while `mustChangePassword` is true**
(§2a: `me` is exempt from the must-change-password gate by design). Failures: `401`
`authentication.required` / `authentication.session_expired` / `authentication.session_revoked`
(see §3); `429`.

**`POST /api/v1/auth/refresh`** — no request. Extends the idle window and returns the refreshed
`AuthSessionResponse`; does **not** rotate the session token (spec 9.1 rotates only on privilege
change and password change, not on every refresh). Named to match the frontend's existing
"queue behind one refresh attempt" vocabulary (CLAUDE.md §5), but there is no separate refresh
credential in a cookie-session model — it cannot revive an already-dead session, only extend a
live one; see §3a for what that means for the frontend. Failures: `401` (same three variants as
`me`), plus `403` `auth.password_change_required` while the gate applies (§2a) — `refresh` is
**not** one of the three endpoints exempted from it.

**`POST /api/v1/auth/password`** — request
`{ "currentPassword": "string", "newPassword": "string" }`. Rotates the session token (spec
9.1) and revokes every other session for the account (spec 6.1.11) except this one; returns
`200` `AuthSessionResponse` with `mustChangePassword: false`. Failures:
- `422` — new password fails length/composition (`request.validation_failed`) or matches one of
  the last five hashes (`auth.password_reused`, field-keyed to `newPassword`).
- `401` — `currentPassword` does not match (`auth.current_password_incorrect`). **Decided (human
  ruling, 2026-09-05): keep 401** — treated as a failed re-authentication for this sensitive
  action, not an RBAC matter. TASK-0019 should follow the same convention for its own
  "re-prove your credential" checks.
- `403` — `csrf.missing`/`csrf.invalid`.
- `429` — sensitive rate-limit policy (framework-produced, no `errorCode`). **Approved and folded
  into the acceptance criteria** alongside `sign-in` — `currentPassword` is an online guessing
  surface too.

### 2a. Cross-cutting: the must-change-password gate

Spec 6.1.6: while `mustChangePassword` is true, server middleware "rejects every request except
the password change endpoint and logout." For this card that means exactly **three** exceptions:

- `POST /api/v1/auth/password` — the endpoint that clears the flag.
- `POST /api/v1/auth/sign-out` — spec's literal "logout" exception.
- `GET /api/v1/auth/me` — **added exception, fixing a defect in the first draft.** `me` is how
  the frontend learns `mustChangePassword` in the first place on a hard page reload (it already
  has the flag from `AuthSessionResponse` right after sign-in, but loses that in-memory state on
  reload and has only the cookie to rehydrate from). A `403` here would withhold the very flag
  that tells the app to render the forced change-password screen, with no way back in. `me`
  therefore always returns `200`, `mustChangePassword` included, regardless of the gate.

Every *other* protected endpoint — `POST /auth/refresh` in this card, and every future product
endpoint from TASK-0005 onward — returns `403 auth.password_change_required` while the flag is
true. This is a distinct, non-generic 403 variant from `authorization.forbidden` (TASK-0002): it
reveals only the caller's own account state back to itself, never a privilege or another
entity's existence, so it doesn't conflict with that rule's genericity requirement.

### 3. The `401`/`403` split and the three 401 variants

Per the card: 401 = not authenticated, 403 = authenticated but unprivileged/blocked, generic
body per TASK-0002. `authorization.forbidden` (TASK-0002) is unchanged and untouched by this
card — none of the six endpoints above run an RBAC/`RequirePrivilege` check, so this card never
emits it itself; it's listed for completeness since the card asks for the split.

This card **does** touch the 401 challenge path for the first time (TASK-0002 explicitly left it
as the framework default — empty body). Introducing a real scheme means a mirror of
`ProblemDetailsAuthorizationMiddlewareResultHandler` for authentication challenges, distinguishing:

| errorCode | Meaning |
|---|---|
| `authentication.required` | No session cookie presented, or it doesn't parse. Never signed in. |
| `authentication.session_expired` | Cookie present; idle (30 min) or absolute (8h) timeout exceeded. |
| `authentication.session_revoked` | Cookie present; session was explicitly revoked — logout elsewhere, suspension/deactivation, or a password/privilege change elsewhere. Maps to spec 6.1.13's exact case: "the interface routes to login with the message Your access has been withdrawn." |

All three are `401`, same `ProblemDetails` shape, different `errorCode`/`detail`. This requires
the session-lookup to report *why* validation failed (an enum, not a bool) — a design
implication for the implementing dispatch, not a new endpoint.

### 3a. Refresh semantics — which 401s are worth queuing behind a refresh attempt

CLAUDE.md §5 mandates one single-flight refresh path: concurrent 401s queue behind one refresh
attempt. That was written with a bearer-refresh-token model in mind and needs restating for a
cookie session with no separate refresh credential.

**All three 401 variants above are terminal for `/refresh` too.** Calling `/refresh` after a
session has already expired or been revoked returns the same 401 `/refresh` itself would give —
there is no second credential that could still be alive when the session isn't. So: **a reactive
401, from any endpoint, of any variant, should route straight to the sign-in screen — never
trigger `/refresh`.** The only place `/refresh` earns its keep is a **proactive** call, made
before expiry (on a timer, or on user activity) while the session is still known-good, to extend
the idle window ahead of time. I agree with the orchestrator's reading here rather than arguing
against it.

This changes what "single-flight" protects against: it is no longer "dedupe concurrent retries
behind one refresh" (refresh isn't a retry path), it's **dedupe concurrent 401s into one
sign-out-and-redirect**, so five requests failing at once produce one "you've been signed out"
transition, not five. TASK-0021 should replace the shape of the existing (superseded)
bearer-retry interceptor in `frontend/src/lib/http/http-client.ts`, not adapt it — that
interceptor's whole premise (401 → refresh → retry the same request) doesn't hold here.

### 4. The `whoami` decision — **replaces**, does not absorb

`GET /api/v1/auth/me` **replaces** `/api/v1/reference/whoami`. Reasons:
- `whoami`'s own docstring already anticipates this ("useful once TASK-0003 wires real
  authentication... see TASK-0003") and it lives in `ReferenceEndpoints.cs`, explicitly scaffold
  ("delete this file with the `SampleRecord` slice").
- The shapes aren't compatible enough to "absorb": `whoami` is anonymous-tolerant (`200` always,
  `null`/`false` when nobody's signed in); `me` is spec 6.1.14's real product endpoint and 401s
  when anonymous. Keeping both would mean two different-shaped "who am I" answers live at once.

**Breaking**: removing `/api/v1/reference/whoami` from the document. A client of the old surface
calling it after this ships gets `404`, not a compatible degraded response. I did not find a real
caller of it (frontend's known-wrong bearer scaffold doesn't call it either), so the practical
blast radius looks like zero, but flagging the removal explicitly per the card's own instruction.

### 5. CSRF mechanism — custom double-submit cookie, not ASP.NET Core's built-in `IAntiforgery`

**Chosen: a literal double-submit cookie** — server mints an opaque random token, sets it in a
non-`HttpOnly`, `Secure`, `SameSite=Lax`, `__Host-XSRF-TOKEN` cookie, and the frontend echoes the
same value in an `X-CSRF-Token` header on every mutating request; the server accepts the request
only if the header value matches the cookie value.

**Why not the built-in `IAntiforgery`** (the more idiomatic ASP.NET Core answer): its cookie
value is an encrypted blob, not the literal token the caller must echo — the real token is handed
back separately (traditionally a hidden form field). A JS client has to explicitly fetch and
thread that value through app state. A literal double-submit cookie, by contrast, is exactly what
axios (the frontend's transport, already in `frontend/src/lib/http/`) auto-handles via its
`xsrfCookieName`/`xsrfHeaderName` config — it reads the readable cookie and sets the header
itself, zero interceptor code needed in TASK-0021. Given the mechanism is genuinely open per this
card, I'm picking the option that costs nothing on the other side of a boundary I don't own.

**Validation**: stateless — an HMAC over a rotating server-side key, the session id (once one
exists) or a short-lived anonymous nonce, and an expiry, so verifying a CSRF header on every
mutating call doesn't add a second database round trip alongside the session lookup. This is
deliberately **not** the same construction as the session token (spec 9.1 fixes that one as
opaque-and-hashed-server-side) — CSRF's job is proving same-origin JS involvement, not identity,
so it doesn't need to be revocable or persisted the way a session does.

**Known limitation, shared by every double-submit variant**: it does not defend against a
sibling subdomain that can plant cookies on the shared registrable domain. The `__Host-` prefix
(§6) closes that specific hole by making the cookie host-locked rather than domain-scoped.

### 6. Cookie attribute set, CORS and `credentials`

| Cookie | Name | HttpOnly | Secure | SameSite | Domain | Path | Max-Age |
|---|---|---|---|---|---|---|---|
| Session | `__Host-Session` | yes | yes | Lax | *(none — required by `__Host-`)* | `/` | 28800s (8h, spec 6.1.11 absolute cap; idle 30 min enforced server-side, not via cookie expiry) |
| CSRF | `__Host-XSRF-TOKEN` | no | yes | Lax | *(none)* | `/` | 3600s pre-auth (anonymous, from `GET /auth/csrf`); reissued at 28800s bound to the session on sign-in, rotated alongside the session token |

`SameSite=Lax` assumes the deployed frontend and API share a registrable domain (e.g.
`app.example.com` / `api.example.com` — subdomains are same-*site* even though CORS still treats
them as different origins). **This is an assumption, not a decision on record** — Open question 5
(production target) is silent on topology. If the real deployment is cross-site, both cookies
need `SameSite=None`, which is a bigger CSRF/cookie posture change, not a one-line tweak — flagged
below, not decided here.

**CORS**: `CorsOptions.AllowCredentials` (already defaults `true`) must stay `true`, and
`AllowedOrigins` must list the exact frontend origin(s) — already enforced at startup by
`CorsOptionsValidator` (wildcard-with-credentials rejected). **Frontend**: every request must use
`credentials: 'include'` (axios: `withCredentials: true`) — TASK-0021's problem, noted here per
§5's "state both halves together."

### 7. Security scheme declaration

```jsonc
"securitySchemes": {
  "CookieSession": {
    "type": "apiKey",
    "in": "cookie",
    "name": "__Host-Session",
    "description": "Opaque session token set by POST /api/v1/auth/sign-in. HttpOnly — the browser attaches it automatically; it is never readable or settable from JavaScript."
  }
}
```

Applied as a `security` requirement on `me`, `refresh`, `password` (not `sign-in`, `sign-out`,
`csrf`, which are anonymous/optional). CSRF is documented as a required `X-CSRF-Token` header
parameter on each mutating operation, **not** a second `securityScheme` — OpenAPI's `security`
concept models "who is calling," and CSRF proves request provenance, not identity.

### 8. Breaking vs. additive, per change

| Change | Class | What an old client sees |
|---|---|---|
| `POST /sign-in`, `/sign-out`, `/refresh`, `/password`, `GET /csrf` | Additive | New paths; nothing to see, nothing existed before. |
| `GET /api/v1/auth/me` (new path) | Additive | — |
| Removal of `GET /api/v1/reference/whoami` | **BREAKING** | `404` where it previously got `200`. |
| `CookieSession` security scheme + `Authentication:Mode` moving off `Placeholder` | Foundational, not a wire-shape break | No prior caller could ever succeed against a protected route (Placeholder never authenticates), so there's no working caller to break — but this is the change the card's own header calls "the authentication scheme every later endpoint sits behind." |
| `/reference/arms/{armId}/secure` becomes reachable by a real privileged caller for the first time | No shape change | Same document entry; only now actually exercisable — worth a manual check, not a contract diff. |

## Open questions — orchestrator/human ruling received 2026-09-05

1. ~~Spec tension, 4.2.2 vs 6.1.7 rule 4.~~ **RESOLVED (human ruling, binding).** Flag-bypass
   reading ratified — see §1, now recorded there as a decision, not reopened here. TASK-0019
   owns real `role`/`role_assignment` persistence for non-super-admin accounts.
2. ~~Lockout disclosure (423).~~ **RESOLVED (human ruling, binding).** `423` only when the
   submitted password is actually correct; a locked account given a wrong password still gets
   the generic `401 auth.invalid_credentials`. See §2 (sign-in) for the updated shape and the
   timing-oracle implementation note.
3. ~~`auth.current_password_incorrect` as 401, not 403.~~ **DECIDED: keep 401** — recorded in §2
   (password). TASK-0019 follows the same convention for its own re-authentication checks.
4. **Audit events for auth itself — still open, moved out of this card.** Spec 6.1.12's
   recorded-actions list never names sign-in, sign-out, lockout or failed-login, though a
   lockout write touches `admin_account`, a governed entity, by the general rule. This belongs
   to whichever card wires the audit transaction TASK-0002 seamed and never populated — noted
   here so it isn't silently dropped, not a TASK-0003 blocker.
5. ~~Rate-limiting `password` under the sensitive policy.~~ **APPROVED** — folded into the
   acceptance criteria alongside `sign-in` (see below).
6. **`SameSite=Lax` assumes a same-registrable-domain deployment** (§6) — stays as a stated
   assumption in the delta, not resolved here. Orchestrator is recording it as `## Known drift`
   triggered by Open question 5 (production deployment target), so it cannot ship to production
   unexamined.
7. ~~HTTPS/HSTS enforcement~~ — checked, not open: `Program.cs` already calls `UseHsts()` /
   `UseHttpsRedirection()`, so the `__Host-`/`Secure` cookies in §6 have a real floor to stand on.

---

## TASK-0019 / TASK-0027 — Idempotency substrate and admin account management

**Part 1 (idempotency) APPROVED 2026-09-06 by orchestrator. Part 2 (accounts) AMENDED and held for
human sign-off under §5.** Drafted by backend-dev in one dispatch. The card was split three ways on
approval: TASK-0019 substrate (contract-neutral), TASK-0027 accounts, TASK-0028 roles/assignments.

Classification: Part 1 **none** as shipped by TASK-0019 (no route declares it); Part 2 **additive**.

### Part 1 — `Idempotency-Key` — APPROVED

**Format** `^[\x21-\x7E]{1,255}$`, visible ASCII, no whitespace, client-generated, UUID v4
recommended and not enforced.

**Fingerprint** = method + path + **caller** + normalized body hash. Caller is part of the key
identity, so the same string from two accounts is two requests.

**Declared per operation as an OpenAPI header parameter**, the way `X-CSRF-Token` is — never a
blanket rule applied invisibly.

| Route | Header |
|---|---|
| `POST /api/v1/admins` | REQUIRED |
| `PATCH /api/v1/admins/{id}` | accepted |
| `POST /api/v1/admins/{id}/status` | accepted |
| `POST /api/v1/admins/{id}/password-reset` | accepted (orchestrator addition, see B1) |
| all four mutating `/api/v1/auth/*` routes | **unchanged, no header** |

The auth exclusion is ratified: a repeated sign-in, sign-out, refresh or password change converges
to the same session state, and is not the double-creation harm class the mechanism exists for.
`PATCH` and `status` accept the header not because they are unsafe but because a retry would
otherwise write a duplicate `audit_event` (6.1.12).

| Case | Status | `errorCode` |
|---|---|---|
| absent on a REQUIRED route | 400 | `idempotency.key_missing` |
| present, fails the format | 400 | `idempotency.key_malformed` |
| same key, different fingerprint | 409 | `idempotency.key_conflict` |
| same key, same fingerprint, first call still in flight | 409 | `idempotency.request_in_progress` (retriable) |
| same key, same fingerprint, first call finished | original status and body replayed, plus `Idempotency-Replay: true` | — |

400 rather than 422 for a malformed key, consistent with `ASSUMPTIONS.md` §2.3's
malformed-versus-rejected-by-rule split: this is a header-shape defect, not a validated-body rule.

**Mechanism**: one filter/extension mirroring `RequireCsrfToken()` — centralised storage,
fingerprinting and replay in the API layer, declared per route in the contract.

**Retention: 24 hours.** Scheduled purge, same family as §9.9's existing purge jobs. Each run writes
one `audit_event` (`actor_admin_id: null`, `action: system.idempotency_purge`). The stored row — key
hash, fingerprint hash, caller, response body — is never exposed by any endpoint. §9.9 makes an
indefinite table a defect, not a deferral.

**Orchestrator amendment A1 — `Idempotency-Replay` is a DECLARED response header, not prose.** The
draft had it as an informational header outside the schema. §3 makes the contract law for every byte
crossing the boundary, and a response header is such a byte. TASK-0003 was reopened for exactly this
defect with `X-CSRF-Token`, and the fix that was accepted there is the fix required here: a marker
attached by the same extension method that wires the filter, read by an operation transformer
(`CsrfHeaderOperationTransformer` is the working precedent). Declaration by construction, never
hand-annotation. It lands with TASK-0027, since TASK-0019 declares nothing.

**Orchestrator amendment A2 — resolves the agent's open Q6: REDACT.** The draft stored the
`POST /admins` response verbatim, which embeds `temporaryPassword`, and asked whether a 24-hour
plaintext window was acceptable. It is not, and the spec settles it rather than the risk appetite
doing so: 6.1.9 says the system "displays it once on screen with a copy button, and never displays
it again", and 6.1.14 repeats "Returns the temporary password once in the response body and never
again." A replay that returns it is a second display. **The stored copy is redacted; a replay
returns the same shape with `temporaryPassword` null, distinguished by the `Idempotency-Replay`
header.**

The objection this would normally attract — that a client which lost the original response is now
holding an account whose password nobody knows — does not apply, because the spec already provides
the recovery path as a first-class operation: `POST /admins/{id}/password-reset` (6.1.14, 6.1.11),
whose privilege `admin.password.reset` is already in the register. Losing a create response costs
one forced reset, not an orphaned account. **The redaction hook is generic and built in TASK-0019;
its first caller is TASK-0027.**

### Part 2 — `/api/v1/admins` — AMENDED, HELD FOR HUMAN SIGN-OFF

Held under §5: `password-reset` and `DELETE /sessions` are session/credential operations. The
mechanism is TASK-0003's and unchanged; what is new is who may operate it on whose behalf.

Accepted from the draft as-is: the `CursorPagedResultOfAdminAccountSummaryDto` envelope and the
refusal to reuse the offset-based `PagedResultOfSampleRecordDto` (§9.5 forbids offset); page size 25
with max 100 rejected at 422 rather than silently clamped; default sort status-ascending then
staff-name-ascending with deactivated excluded unless named (6.1.8); email uniqueness across active
and suspended only, case-insensitive, a deactivated account's email reusable (6.1.3, 6.1.9); the
full active/suspended/deactivated state machine rather than one-way deactivation, which the
already-registered `Privileges.Admin.*` had in effect already committed this card to; per-user
rate-limit partitioning with Sensitive on mutations and Default on reads; the last-active-Super-Admin
invariant enforced transactionally under a row lock rather than by pre-flight read.

**B1 — two endpoints were missing.** Spec 6.1.14 enumerates the endpoint list, and the draft's five
omitted `POST /admins/{id}/password-reset` (`admin.password.reset`) and `DELETE /admins/{id}/sessions`
(`admin.session.revoke`). Both privileges are already in the register; both are account lifecycle,
not role management, so neither belongs in the TASK-0028 split. Added. Per 6.1.11, the forced reset
sets a new temporary password, displays it once, sets `must_change_password`, and revokes every
active session for the account.

**B2 — resolves the agent's open Q5 against its own draft.** The draft gated `PATCH` on
`admin.update` alone and flagged the self-edit carve-out as unresolved. 6.1.2 is explicit: "Edit an
account's own details — `admin.update`, **or the account itself for name, phone and password**."
The carve-out is required, and it is narrower than it first appears: it covers `staffName` and
`phone` only. **Email is not in it**, and changing your own email — the login identifier (6.1.3) —
still requires `admin.update`.

**B3 — session revocation on state change was absent from the draft.** 6.1.10: suspension revokes
existing sessions immediately; deactivation revokes sessions and all active assignments; a
`deactivated -> active` move needs `admin.deactivate` held by a Super Admin and does NOT restore
assignments. The assignment half has nothing to act on until TASK-0028 — that card wires it, and
TASK-0027 must leave the seam visible rather than silently completing it.

**B4 — approves the agent's open Q1 split.** 6.1.7's rules 1-3 govern `role_assignment` mutation and
role privilege lists; only rule 4 (`is_super_admin`) has a surface in an accounts-only card. Roles,
assignments, `GET /privileges` and rules 1-3 move to TASK-0028. The omissions this forces —
`rolesHeld`/`scopeSummary` on the list item, assignments/effective-privileges/last-ten-audit-events
on the detail view (6.1.8), and the role/scope/session filters — are all additive when TASK-0028
restores them, so the split costs no breaking change. It is safe for a second reason the agent did
not cite: 6.1.9 states outright that an account with zero assignments can exist, can log in, and
sees a "no access has been granted" page. A roleless account is a supported product state, not a
gap the split invents.

**B5 — approves the agent's open Q4 with its caveat recorded.** Blocking a caller from changing
their own status returns `403 admin.self_status_change_forbidden`. The agent correctly noted no spec
line requires it. Approved anyway: it is strictly safer, it is always reversible by another Super
Admin, and 4.1's at-least-one-active-Super-Admin invariant already establishes that the system is
expected to prevent administrative self-lockout. **Recorded as an assumption in
`backend/docs/ASSUMPTIONS.md`, not as a spec derivation** — this is an added rule and must be
visible as one.

**B6 — confirms the agent's open Q2 reading.** Audit event WRITES are in scope and transactional
(6.1.12 names `admin_account` explicitly; 6.1.7's preamble additionally requires an event on every
rejected escalation attempt). The audit READ surface (`GET /audit`, `GET /audit/export`) stays out —
it is the Phase 1 roadmap row, not this card's.

**B7 — the agent's open Q3 defers with B4.** 6.1.8's "last ten audit events by this account" needs
an audit query; it goes to TASK-0028 with the rest of the detail view.

**Q7 needed no ruling.** The `UseRateLimiter()`/`UseAuthentication()` ordering drift
(`Program.cs:298` vs `:300`) is correctly identified as implementation, not contract. Its trigger
moves from TASK-0019 to TASK-0027, which is where a per-user-partitioned route first ships. Noted
there as an acceptance criterion with the trap stated: a 429 test that would still pass under IP
partitioning does not prove the fix.
