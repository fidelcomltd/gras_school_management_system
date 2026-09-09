import { AUTH_EXPIRY_LEEWAY_MS } from '@/config/env-values';
import type { components } from '@/api/schema';

/**
 * The ONE place that knows how this app authenticates.
 *
 * Decision (TASK-0021, superseding the 2026-08-03 bearer scaffold): HttpOnly
 * cookie session + CSRF token, human sign-off 2026-08-26 (root CLAUDE.md §5).
 * The session token is never readable from JavaScript — this module holds no
 * token at all, only the non-secret state an `AuthSessionResponse` reports,
 * plus the CSRF token (deliberately readable JS-side; that's the point of a
 * double-submit cookie). If the mechanism ever changes again, this file and
 * `attachAuthInterceptors` in `../http/http-client.ts` are the only things
 * that change.
 */

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type AuthSession = components['schemas']['AuthSessionResponse'];

export type SessionEndListener = () => void;

/** Makes the proactive `POST /auth/refresh` call. Registered once by http-client.ts. */
export type KeepaliveCaller = () => Promise<AuthSession>;

let current: AuthSession | null = null;
let csrfToken: string | null = null;
let sessionActive = false;

let keepaliveTimer: ReturnType<typeof setTimeout> | undefined;
let keepaliveCaller: KeepaliveCaller | null = null;
let inFlightKeepalive: Promise<AuthSession | null> | null = null;

const sessionEndListeners = new Set<SessionEndListener>();

function clearKeepaliveTimer(): void {
  if (keepaliveTimer !== undefined) {
    clearTimeout(keepaliveTimer);
    keepaliveTimer = undefined;
  }
}

/** Registers the function that performs the proactive refresh call. */
export function registerKeepaliveCaller(caller: KeepaliveCaller | null): void {
  keepaliveCaller = caller;
}

/**
 * Single-flight proactive refresh: overlapping triggers collapse into one
 * `POST /auth/refresh`. A failure here needs no handling of its own — it goes
 * through the http layer's response interceptor like any other request,
 * which is what actually ends the session (delta §3a: this call is terminal
 * too, it just isn't a *reactive* one).
 */
function runKeepalive(): Promise<AuthSession | null> {
  if (!keepaliveCaller) return Promise.resolve(null);
  if (inFlightKeepalive) return inFlightKeepalive;

  inFlightKeepalive = keepaliveCaller()
    .then((session) => {
      setSession(session);
      return session;
    })
    .catch(() => null)
    .finally(() => {
      inFlightKeepalive = null;
    });

  return inFlightKeepalive;
}

/**
 * Schedules the proactive keepalive from the backend-reported deadlines —
 * never a hardcoded duration (ruling 4). "Room to extend" means the reported
 * idle deadline (`sessionExpiresAt`) is still strictly short of the fixed
 * absolute cap (`sessionAbsoluteExpiresAt`, set once at sign-in); once the
 * backend has already clamped the two to be equal, refreshing again would
 * just report the identical deadline back, so this goes straight to ending
 * the session, at the cap, instead of calling `/refresh` (delta §3a).
 *
 * Skipped entirely while `mustChangePassword` is set — `refresh` is not
 * exempt from that gate (unlike `me`) and would just 403, and there is no
 * change-password screen in this card to react to it (ruling 6, follow-up
 * card owns it).
 */
function scheduleKeepalive(session: AuthSession): void {
  clearKeepaliveTimer();
  if (session.mustChangePassword) return;

  const expiresAtMs = Date.parse(session.sessionExpiresAt);
  const absoluteAtMs = Date.parse(session.sessionAbsoluteExpiresAt);
  const canExtend = expiresAtMs < absoluteAtMs;
  const deadlineMs = canExtend ? expiresAtMs : absoluteAtMs;
  const fireAtMs = deadlineMs - AUTH_EXPIRY_LEEWAY_MS;

  const act = (): void => {
    if (canExtend) void runKeepalive();
    else terminateSession();
  };

  const fireIn = fireAtMs - Date.now();
  if (fireIn <= 0) {
    act();
    return;
  }
  keepaliveTimer = setTimeout(act, fireIn);
}

/** The CSRF token last returned by `GET /auth/csrf`, or null before it is fetched. */
export function getCsrfToken(): string | null {
  return csrfToken;
}

/**
 * Stores the CSRF token. Read from the `GET /auth/csrf` response BODY only —
 * never `document.cookie` (ruling 2). The cookie is readable today only
 * because dev shares the host `localhost`; the `__Host-` prefix forbids a
 * `Domain` attribute, so on a real deployment where the API is a different
 * host the frontend could never read it there.
 */
export function setCsrfToken(token: string): void {
  csrfToken = token;
}

export function getSession(): AuthSession | null {
  return current;
}

/**
 * Whether `session` carries `privilege`, either directly in its effective set
 * or via the super-admin flag-bypass path (TASK-0003 §1, spec 9.2). The one
 * place nav visibility and route guards ask this question, so the two can
 * never disagree (TASK-0041: an item/route the caller cannot use is ABSENT,
 * never disabled-and-visible).
 */
export function hasPrivilege(session: AuthSession, privilege: string): boolean {
  return session.isSuperAdmin || session.effectivePrivileges.some((grant) => grant.privilege === privilege);
}

/** Records the latest session state and (re)arms the keepalive from it. */
export function setSession(session: AuthSession): void {
  current = session;
  sessionActive = true;
  scheduleKeepalive(session);
}

/**
 * The one session-end path (amended AC-5). Idempotent and synchronous, so
 * calling it several times in the same tick — several concurrent 401s, or a
 * 401 racing a voluntary sign-out — still notifies listeners exactly once.
 * Used by: the http layer's response interceptor on any terminal 401; a
 * successful sign-out mutation, after the server has revoked the session
 * (never before — clearing client state alone is a blocker, §5); and the
 * keepalive schedule once there is nothing left to extend.
 */
export function terminateSession(): void {
  if (!sessionActive) return;
  sessionActive = false;
  current = null;
  clearKeepaliveTimer();
  for (const listener of sessionEndListeners) listener();
}

/** Subscribe to session end. Returns an unsubscribe function. */
export function onSessionEnded(listener: SessionEndListener): () => void {
  sessionEndListeners.add(listener);
  return () => {
    sessionEndListeners.delete(listener);
  };
}

/**
 * Test-only: drop all PER-SESSION state so cases cannot leak into each other.
 * Deliberately leaves `keepaliveCaller` alone — it is app-lifetime wiring
 * done once by `http-client.ts` at module load, not per-test state; a test
 * that needs a specific (or no) caller registers one explicitly.
 */
export function __resetAuthSession(): void {
  clearKeepaliveTimer();
  current = null;
  sessionActive = false;
  csrfToken = null;
  inFlightKeepalive = null;
  sessionEndListeners.clear();
}

/** Test-only: lets a test drive the scheduled keepalive directly. */
export function __triggerKeepaliveForTest(): Promise<AuthSession | null> {
  return runKeepalive();
}
