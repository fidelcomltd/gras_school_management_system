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

**Part 1 (idempotency) APPROVED 2026-09-06 by orchestrator. Part 2 (accounts) AMENDED, then
APPROVED 2026-09-06 by human sign-off under §5 — see the Part 2 heading for the ruling and its
scope.** Drafted by backend-dev in one dispatch. The card was split three ways on
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

### Part 2 — `/api/v1/admins` — AMENDED, then APPROVED BY HUMAN SIGN-OFF (2026-09-06)

Held under §5: `password-reset` and `DELETE /sessions` are session/credential operations. The
mechanism is TASK-0003's and unchanged; what is new is who may operate it on whose behalf.

**§5 SIGN-OFF, 2026-09-06, human (chisom.maxwell@securedrecords.com): APPROVED AS DRAFTED.** The
question put was who may operate another account's sessions and credentials, with four options
offered: (1) approve as drafted, (2) approve with a step-up re-authentication requirement on
`password-reset` and `DELETE /sessions`, (3) approve but restrict those two to `is_super_admin`
regardless of the privilege grant, (4) defer the two endpoints (which would not have removed the
sign-off, since suspend and deactivate revoke the target's sessions too). **Option 1 chosen.**

What that authorises, precisely: an account **holding the privilege** may exercise it against
another account — `admin.password.reset` mints a new temporary password, sets
`must_change_password` and revokes every session the target holds (6.1.11); `admin.session.revoke`
signs the target out everywhere; suspension and deactivation revoke the target's sessions
immediately (6.1.10). **The privilege grant is the whole gate.** No step-up re-authentication is
required of the acting admin, and `is_super_admin` is NOT an additional requirement on these two
endpoints — the spec's privilege model stands unmodified. The guards that remain are the ones
already in the delta and nothing further: the transactionally-enforced at-least-one-active-Super-Admin
invariant (4.1), the self-status-change block (B5), the Super-Admin-only reactivation path (6.1.10),
and 6.1.7 rule 4's is-super-admin-sets-is-super-admin restriction.

No shape in this delta changed as a result of the sign-off — the ruling is on authority, not on the
wire. TASK-0027 is unblocked and dispatchable as written.

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

---

# TASK-0005 — School settings delta: APPROVED WITH FOUR AMENDMENTS (2026-09-06)

Proposed by `backend-dev` dispatch 1 (delta-only, no code — full text in the card's `## Log`).
Additive throughout; no existing path changes shape, so no human sign-off is required (§3). The
card is **SPLIT THREE WAYS** as proposed: TASK-0005a (identity + `config_version` substrate),
TASK-0005b (uploads), TASK-0005c (registration numbers).

**Verified mechanically before approving**: all five privilege strings exist verbatim in
TASK-0002's shipped register (`Privileges.cs:76,86,92,95,98`) — `settings.view`,
`settings.identity.update`, `settings.abbreviation.update`, `settings.regnumber.update`,
`audit.view`. None invented.

**Verified against the spec, not against the delta**: the preview reading is correct and I had
expected it to be wrong. Spec 6.2.4 says the preview is built "from the **current** abbreviation,
separator and serial width" — so taking `separator`/`serialWidth` as unsaved query parameters
while composing the abbreviation from saved state is exactly right, not an omission.

## Amendment 1 — `serial_reset: continuous` has no counter to read (BLOCKER for 0005c)

`registration_number_counter` is proposed as `admission_year` (PK) + `last_serial_issued`. That
shape can only express `per_year`. Spec 6.2.4 makes `serial_reset` an enum of **`per_year` OR
`continuous`**, and under `continuous` the serial does not restart in January — 2027's first
pupil follows 2026's last. A counter keyed on year alone cannot represent that sequence, so
`continuous` would silently behave as `per_year`: the setting would appear in the API, be
accepted, be stored, and do nothing. That is the defect family this project keeps removing —
a control that reports success and changes no behaviour.

Fix in 0005c: the counter must carry both modes (a sentinel/global partition alongside the
per-year rows, or an explicit partition key), the preview must compose from whichever partition
the **currently saved** `serial_reset` selects, and the width-reduction rejection must scan the
partition that mode actually uses. **A test must set `continuous`, cross a year boundary in
seeded counter state, and assert the serial does not restart** — a test that only exercises
`per_year` leaves this unproven.

## Amendment 2 — `abbreviation.issuedCount` cannot be computed by this card, and must not pretend

The delta returns the 6.2.4 dialogue's count ("**412 pupils** already hold registration numbers
beginning GRAS") as a plain field on `GET /settings`. Correct placement — it must reach the UI
before the admin types `CHANGE`, so it cannot ride on an error body. But **no pupil or
registration register exists yet**; nothing in TASK-0005 can count issued numbers. Left as
drafted, the field ships as a permanent `0` and the dialogue renders "0 pupils already hold…"
as though it were a fact.

Fix in 0005c: the field is **nullable**, `null` meaning "no register exists to count yet" — never
`0`, which is a claim. The contract documents it as nullable-until-the-register-exists, and the
frontend card that renders the dialogue must handle `null` by suppressing the count sentence
rather than printing a zero. Owner of the wiring: the pupil-registration card, recorded as drift
with that trigger.

## Amendment 3 — the stale-save message is grading-scale copy; do not reuse it verbatim

6.2.11's optimistic-concurrency sentence names the grading scale specifically ("The grading scale
was changed by another administrator while you were editing. Reload and make your change again.").
This card touches identity, abbreviation and registration number — none of them the grading
scale. Substituting the group name follows the spec's pattern but the resulting sentence is
**our copy, not the school's spec**. Write it per group, and record it in
`backend/docs/ASSUMPTIONS.md` as authored copy awaiting confirmation. Do not present invented
user-facing text as though the spec supplied it.

## Amendment 4 — "logo deleted with no replacement" gets a domain invariant, not an invented route

Open question 4 answered: **do not invent a `DELETE` route.** Spec 6.2.12's own endpoint
enumeration has no removal route, and 6.2.2 confirms the logo is *not seeded* — so the real rule
is a set→null transition guard, not a route. Implement it as a domain invariant carrying 6.2.11's
verbatim message, unit-tested at the domain level. An acceptance criterion whose only proof is an
HTTP route that does not exist is a vacuous criterion; this makes it testable without inventing
product surface. Record in `STATE.md ## Known drift` that the removal affordance is currently
unrouted, triggered by the settings-screen frontend card.

## Confirmed as proposed (no change needed)

- **Concurrency**: client-echoed integer `expectedVersion` per group, compared inside the same
  transaction as the `config_version` insert, `409` before any write. Two int pointer columns on
  `school_profile` (identity + abbreviation) rather than a second singleton table for one string
  field. Loser gets the audit-seam call and no version row — which is what 6.2.11's "both attempts
  appear in the audit log" actually says.
- **`config_version`**: one global monotonic ledger, `snapshot` holding the WHOLE serialised
  configuration on every write (6.2.9's rationale, card note 1). Not a reference. Never updated.
- **Idempotency**: `Idempotency-Key` **accepted, not required**, on all five mutating routes —
  consistent with TASK-0019 Part 2's placement of `PATCH /admins/{id}`. The multipart-fingerprint
  gap found while answering (the fingerprint builder JSON-serialises the bound command and so
  cannot see uploaded bytes) is real and is **0005b's to resolve by folding in a content hash** —
  not to rediscover.
- **`ProblemDetails additionalProperties: false` does not bite this card.** Answered explicitly,
  with the count moved to `200` data rather than stuffed into `detail`. The `lockedUntil`
  inaccuracy therefore stays TASK-0027's (`STATE.md ## Known drift`, 2026-09-06).
- **The two serving endpoints** (`GET /settings/identity/logo/{size}`, `.../signature`) are
  APPROVED as additions. §9.6 requires uploads be served through a privilege-checked endpoint and
  spec 6.2.12 lists none — the delta was right to add them rather than assume the silence meant
  "don't build one".
- **Abbreviation reason**: non-empty (trimmed), no 10-character floor. 6.2.9's floor is scoped by
  its own prose to grading/assessment/traits/trait-scale/result-rules, and 6.2.10 confirms
  abbreviation is never locked and never triggers that warning. Cap the length and record both
  choices in `ASSUMPTIONS.md`.
- **Seeding**: 6.2.2 seeds the abbreviation as `GRAS`; the migration must ship that row. School
  name, address, phone, email, motto, logo and signature are explicitly NOT seeded.

---

## TASK-0028 — Roles and the privilege register — APPROVED (orchestrator, 2026-09-06)

Additive throughout. No breaking change, so no human sign-off under §3. No session/auth mechanism
change, so no §5 sign-off. Three new paths take the document from 18 to 21.

Assignments are **not** in this delta — see TASK-0030 and the card's "Why assignments are NOT in
this card".

### 1. `GET /api/v1/privileges` — the register (dispatch 1)

Authenticated, **no privilege required** (spec 6.1.14 says so explicitly). No CSRF (read). Not
paged: the register is a fixed 93-row compile-time constant, not a growing list, so §9.5's cursor
rule does not apply. Deterministic ordering — groups 4.4.1→4.4.6, rows in spec table order.

```
200 PrivilegeRegisterResponse
  groups: PrivilegeGroup[]
      key        string enum — administration | settings | academic_structure |
                 pupils_and_subjects | results | pins_and_reports
      title      string — verbatim spec 4.4.x heading, e.g. "Administration and access control"
      privileges: PrivilegeDescriptor[]
          code      string — canonical register code, e.g. "pupil.safeguarding.view"
          permits   string — verbatim spec 4.4 "Permits" cell
          scopable  boolean
```

- Legacy `guardian.*` aliases (`PrivilegeAliases`) are **not** in the response. The register is
  canonical codes only; aliasing is an input concern.
- `key` crosses as a string (§8) and the client must tolerate unknown members — a seventh module
  group is an additive change.
- No `401`-only special casing beyond the standard authenticated-endpoint behaviour.

### 2. `/api/v1/roles` and `/api/v1/roles/{id}` (dispatch 2)

| Op | Privilege | Idempotency-Key | CSRF | Success |
| --- | --- | --- | --- | --- |
| `GET /roles` | `role.view` | — | — | 200 cursor page |
| `POST /roles` | `role.create` | **required** | yes | 201 + `Location` |
| `GET /roles/{id}` | `role.view` | — | — | 200 |
| `PATCH /roles/{id}` | `role.update` | accepted | yes | 200 |
| `DELETE /roles/{id}` | `role.delete` | accepted | yes | 204 |

`POST` requires the key on TASK-0027's `POST /admins` precedent: it creates an independently
addressable entity, so a retried create must not make two roles.

```
Role
  id           string (opaque, §8)
  name         string 1..60
  description  string 0..300, nullable
  isSystem     boolean
  privileges   string[] — canonical codes, min 1, sorted deterministically
  status       string enum — active | archived

CreateRoleRequest   name, description?, privileges[]
UpdateRoleRequest   name?, description?, privileges?, status?   (all optional; absent = unchanged)
RoleListResponse    items: Role[], nextCursor: string|null      (§9.5, page size 25)
  query: status? (active|archived; ARCHIVED EXCLUDED BY DEFAULT per §9.4), search?, sort?
         (whitelist: name | status), direction?, cursor?, pageSize?
```

### 3. Error codes — all `ProblemDetails` with `errorCode`, closed schema (TASK-0012)

| `errorCode` | Status | When | Message |
| --- | --- | --- | --- |
| `role.name_reserved` | 422 | create/rename to `Super Admin`, case-insensitive | 6.1.4 |
| `role.name_duplicate` | 409 | case-insensitive name collision | 6.1.4 |
| `role.privileges_empty` | 422 | empty privilege list | 6.1.4 "At least one" |
| `role.unknown_privilege` | 422 | any code not in the register | **names the offender** (6.1.4) |
| `role.system_immutable` | **409** | `PATCH`/`DELETE` on an `is_system` role | 6.1.14 fixes 409 |
| `role.privilege_escalation` | 403 | 6.1.7 rule 2 | verbatim, below |

Rule 2's message is fixed by spec 6.1.7 and must not be rephrased. One offender:
`You do not hold settings.grading.update and cannot add it to a role.` Several, comma-joined in
register order: `You do not hold a, b and cannot add them to a role.` Every rejection writes an
audit event through `ISystemAuditSink` (6.1.7 preamble; log-only per the 2026-09-06 drift entry).

Rule 2 is evaluated against the **added** privileges only — a `PATCH` that leaves an existing
privilege untouched is not an "add" and is not rejected (spec: "add a privilege to a role that it
does not itself currently hold"). Removal is unrestricted except on a system role.

### 4. `DELETE` semantics, and the branch this card cannot write

§9.4: a role may be hard-deleted "when no assignment has ever used it", and archived otherwise. No
assignment table exists, so nothing can ever have referenced a role and `DELETE` hard-deletes
unconditionally (system roles excepted, 409). Archiving is reachable today only through
`PATCH { status: "archived" }`. **TASK-0030 must add the has-ever-been-assigned branch** — recorded
as live drift, not left to be rediscovered.

### 5. Breaking vs additive

Every item above is a new path or a new schema. Nothing existing changes shape. Additive.


## TASK-0070 — Subjects, mappings and per-arm exceptions — APPROVED WITH FIVE AMENDMENTS (orchestrator, 2026-09-16)

Proposed by `backend-dev` on dispatch 1 of 2, which produced the delta and no code — the delta-first
split this card was given precisely because TASK-0069 shipped 5,791 lines alongside its own proposal.
The proposal is sound and is approved subject to the amendments below. **Classification confirmed
additive**: eight new paths (seven proposed, plus Amendment 5's), ~20 new schemas, no existing path or schema changed, no new required
field on an existing request. No human sign-off needed on the classification itself.

The agent found **no privilege gap** — `Privileges.Subject.{View,Create,Update,Deactivate,Delete,
Map,MapArm,Unmap}` already cover §6.6.9, and `SeededRoles.cs` grants School Administrator all but
`Delete`. Nothing was invented. Verified independently by the orchestrator against
`Domain/Security/Privileges.cs:280-306`.

### Amendment 1 — `code` is nullable and unseeded (supersedes the proposal's required `Code`)

The proposal has `Code` required on `CreateSubjectCommand`, validated `^[A-Z0-9]+$`, with a
`409 subject.code_duplicate`. **Human ruling 2026-09-16 reverses this.** `code` is nullable, seeded
NULL, and optional on `POST /subjects`. Uniqueness still applies where a value IS supplied, so
`subject.code_duplicate` survives as a 409 on a supplied duplicate; it can never fire on the seed.
Full reasoning in `drift/2026-Q3.md` — the short form is that nothing prints a code today, the arm
broadsheet that would is unbuilt, the school's own two sheets carry no code, and
`18-appendix-c-result-sheet-contract.md:47` declares `subject_code` **optional** while its
`subject_name` neighbour does not. §6.6.2's `Req: Yes` is the outlier and is in the same section
already proven to carry superseded text. `SubjectDto.Code`, `SubjectMappingGridSubjectRowDto
.SubjectCode` and `ArmSubjectDto.SubjectCode` all become nullable.

### Amendment 2 — the privilege split is wrong; `Unmap` gates endings, `MapArm` gates exception delete

The proposal gates the whole `PUT /subject-mappings` grid save under `Subject.Map` and puts
`Subject.Unmap` on `DELETE /subject-exceptions/{id}`. The agent flagged this as inference from the
doc comments rather than a spec statement, which was the right call — and the doc comments actually
say the opposite:

```
Map     = "Map a subject to a level for a session and term."
Unmap   = "End a mapping."
MapArm  = "Create a per-arm exception to a level mapping."
```

`Unmap` means ending a level mapping, which is exactly what the grid save's **endings** are. So:

- `PUT /subject-mappings?term_id=` requires `Subject.Map` when the computed diff contains additions
  and `Subject.Unmap` when it contains endings — **both when it contains both**. This is the
  data-dependent privilege shape the proposal itself cites from `UpdateArmHandler`, applied to the
  diff rather than to the request body. A dry run is evaluated against the same rule, so a caller
  cannot discover the ending set without holding the privilege to perform it.
- `DELETE /subject-exceptions/{id}` requires `Subject.MapArm`, the same privilege as its create.
  Exception create and delete are one pair; deleting an exception is not "ending a mapping".

As proposed, a holder of `Map` alone could end every mapping in a term. That is the shape of
escalation `6.1.7` rule 2 exists to prevent, and it would have shipped inside an otherwise clean
delta.

### Amendment 3 — `GET /subjects` does not guess a term; absent `termId` yields null counts

The proposal resolves an omitted `termId` against "the session's currently active term" and flags
that it needs an `ICurrentTermLookup`-shaped seam. **No such seam exists** — verified, nothing in
`backend/src` matches `ICurrentTerm`/`ActiveTerm`/`CurrentTerm`. Rather than build one inside a card
that does not need it, `termId` stays optional and the three term-scoped counts on `SubjectDto`
(`MappedLevelCount`, `ArmExceptionCount`, `PupilsTakingCount`) are **nullable, and null when
`termId` is absent**. A null count reads as "not asked", which is true; a count silently resolved
against a guessed term is a wrong number that looks right. §6.6.7 lists term as a filter, not as a
required parameter, so this matches the spec rather than bending it.

### Amendment 4 — the two invented rejection messages are provisional and recorded as such

`subject_exception.arm_session_closed` and `subject_exception.redundant_exclude` have no
spec-supplied wording; §6.6.8 says only "Rejected" and "rejected the same way". The proposal's
mirrored sentences are reasonable and are accepted **as provisional copy**, on the same footing as
any other product string the school has not seen. They are not to be quoted anywhere as spec text.
Every other message in the delta IS spec-quoted and must stay verbatim, including the §6.6.6
marks-recorded sentence and the §6.6.4 redundant-include sentence.

### Amendment 5 — an eighth path, `POST /subject-mappings/prefill` (human ruling 2026-09-16)

Not in the proposal, and not in §6.6.9 — added because the card's seed criterion turned out to be
impossible as written. `subject_mapping` requires a `term_id`; **no session and no term is seeded**
(verified: neither `AcademicSessionConfiguration` nor `TermConfiguration` carries `HasData`), and
both are administrator-created, so there is no term at migration time to map against. The 14/19
split therefore cannot ship with the migration. A full grid is 156 rows (14 nursery x 3 levels + 19
primary x 6), which the human declined to impose by hand; applying it automatically on term creation
was also declined, as it puts §6.6 behaviour inside §6.3 and creates 156 rows with no click.

- `POST /subject-mappings/prefill` — `Privileges.Subject.Map` ONLY. It can never end a mapping, so
  `Unmap` is not required and must not be demanded. **`Idempotency-Key` REQUIRED** (retry-duplicable
  creation, same as `copy`).
- Request `PrefillSubjectMappingsCommand(TermId, DryRun)`.
- Response `200 SaveSubjectMappingGridResponse` — the same envelope as the grid save and copy.
  `Endings` is ALWAYS empty: prefill is additive only.
- **Never duplicates.** A prefill against a term that already holds some of the mappings adds only
  the missing ones; a second prefill of the same term adds nothing. Same semantics as `copy`'s
  "shows 11 additions and 0 endings, and does not duplicate the 3".
- Errors: `422` (validation), `404 term.not_found`, `409 subject_mapping.term_closed`, `401`, `403`,
  `429`. The closed-term refusal applies to `DryRun` too, as on the grid save.
- The standard list is a **backend constant**, not a new table and not stored configuration. It is a
  starting point the administrator then edits through the ordinary grid. Subjects resolve to levels
  by the level's `SectionId`.
- **`display_order` is written from the per-section SHEET order**, which is neither the seed table's
  grouped order nor a single global order — both ordered lists are in the card. `display_order`
  belongs to the mapping precisely because one subject can sit at a different row on each section's
  sheet. The five shared subjects happen to occupy identical positions in both of today's lists
  (4, 5, 6, 11, 12); that is a property of these lists, not a rule, and must not be used as a
  shortcut.

Classification unchanged: a new path and one new schema. Additive.

### Confirmed as proposed, no change

- **`Idempotency-Key` on `POST /arms/{id}/subject-exceptions`**, beyond the two the card names. The
  standing obligation is explicit that 9.8.2's four operations are examples, not the list. The agent
  gap-filled and flagged it instead of adding it silently, which is the behaviour the obligation
  wants.
- **Active-uniqueness on `(arm_id, subject_id, term_id)` regardless of mode.** One constraint
  satisfies the card's AC 4 (include + exclude on one triple is rejected, not order-resolved) and
  the same-mode duplicate case together, mirroring `subject_mapping`'s partial unique index and the
  `enrolment` precedent from TASK-0059.
- **`DisplayOrder` on `ArmSubjectDto`**, additive beyond §6.6.9's literal text. §6.6.1 makes this
  endpoint's output the rows of the result sheet, and TASK-0071 needs a deterministic order without
  a second query. Justified.
- **Unwrapped array on `GET /arms/{id}/subjects`**, per the `ReorderLevels` precedent — bounded list,
  no pagination.
- **PascalCase wire enums** (`Active`/`Inactive`, `LevelInherited`/`ArmException`,
  `Include`/`Exclude`) matching `ArmStatus`, and `<entity>.<reason>` snake_case error codes matching
  `level.name_duplicate` / `arm.label_duplicate`. Both verified against existing code by the agent.
- **No closed-session 409 on exception delete.** The agent declined to extend a creation-only rule to
  deletion and noted the asymmetry rather than resolving it silently. Correct; §6.6.8 states the rule
  for creation only.

### Correction 2026-09-17 — `subject_mapping.copy_same_term` does not exist, and this record did not transcribe the proposal

Two findings from dispatch 4, recorded together because the second is what let the first survive.

**1. The error code was never built, and should not be.** The dispatch-1 proposal listed
`422 subject_mapping.copy_same_term` for source-equals-destination on `POST /subject-mappings/copy`,
and the orchestrator approved it. **No source file names it and the promoted contract names it zero
times.** The rejection is enforced by `CopySubjectMappingsCommandValidator` (FluentValidation),
which `ValidationBehavior` turns into the house-wide generic `request.validation_failed`
(`Error.ValidationErrorCode`) before the handler runs — the same mechanism every other endpoint in
this solution uses for a malformed request. So the document and the implementation agree with each
other; **it was the approved delta that was wrong.** `backend-dev` tested the real mechanism and
flagged the discrepancy rather than inventing a code to match the paperwork, which is exactly the
call `rules/contract.md` §5 asks for. No code change — this record is the fix. A bespoke 422 here
would be a behaviour change and needs its own card.

**2. This archive entry never contained the delta it approved.** It recorded five amendments and a
"confirmed as proposed" list, and pointed at a proposal that exists only in the dispatch
conversation. `STATE.md`'s own index describes this file as the place to read "an approved contract
delta verbatim" — so a reader following the index would not have found the error code at all, in
either direction. That is why the phantom code went unnoticed at approval: the orchestrator approved
a list of endpoint error codes by reference and never wrote them down. **Fix for the next delta:
transcribe the proposal into the archive, then amend it in place — never approve by reference to a
message.**

### Breaking vs additive

Additive. Amendment 1 makes a proposed-required field optional before it ever ships, which is not a
narrowing of anything live. Amendment 3 makes three proposed fields nullable, likewise pre-ship.
Amendment 2 changes authorisation, not shape.
