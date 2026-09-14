# Runtime session and auth consistency

**This file is CLAUDE.md §5, plus the project's settled auth decision.** Binding in full.

Read it when your card touches sign-in, sessions, cookies, CSRF, refresh, logout, password or
CORS. Otherwise skip it.

**Session/auth changes are ALWAYS treated as a contract change requiring human sign-off.**

---

## 1. The rules (§5)

- **One auth mechanism**, chosen explicitly. Not both, not "cookie in dev, bearer in prod".
- **Clock and expiry are backend-owned.** The frontend must never compute or assume expiry from a
  hardcoded duration; it reacts to `401` and to the expiry the backend reports.
- **One refresh path**, single-flight: concurrent 401s queue behind one refresh attempt.
  Implemented once in the API layer, never per-feature.
- **Logout is server-authoritative**: revoke server-side, THEN clear client state. Clearing client
  state alone is a blocker.
- **CORS/cookie settings must match end to end**: `SameSite`, `Secure`, domain and credentials
  mode are configured as one coherent set. A frontend sending `credentials: 'include'` against a
  backend not configured for it is a blocker.
- **Every protected endpoint is protected by default** (authorization fallback policy on the
  backend), with anonymous access opt-in and explicit.

## 2. The decision this project made

Human sign-off 2026-08-26 per §5. Moved here from `STATE.md` on 2026-09-14.

| | |
|---|---|
| **mechanism** | **HttpOnly cookie session + CSRF token.** Token fixed by spec 9.1: opaque 32-byte CSPRNG, stored hashed, rotated on privilege and password change. Not a JWT. JS never holds it. Implemented by TASK-0003. |
| **cookie** | `HttpOnly`, `Secure`, `SameSite`, domain and the frontend's `credentials` mode are ONE coherent set; a mismatch is a blocker. Attributes, CSRF construction and error codes: the approved delta in `.agent/decisions/2026-Q3-contract-deltas.md`. |
| **csrf** | Required on every mutating request, applied in the API layer. |
| **refresh** | One path, single-flight. All 401 variants are TERMINAL — a reactive 401 goes to sign-in, never to `/refresh`; only a proactive pre-expiry refresh has a job (delta §3a). |
| **logout** | Server-authoritative. Revoke, THEN clear client state; clearing alone is a blocker. |
| **password** | Argon2id, 150–300 ms per hash, cost stored with the hash (spec 9.1). Fixed. |
| **2FA** | None in v1 (spec 9.1, Appendix A 5). Fixed. |
| **portal** | Pin validation only, no accounts (spec 6.8, 6.9). |
