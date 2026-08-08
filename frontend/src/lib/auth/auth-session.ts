import { AUTH_EXPIRY_LEEWAY_MS } from '@/config/env-values';

/**
 * The ONE place that knows how this app authenticates.
 *
 * Decision (2026-08-03): bearer access token, held in memory only.
 *
 * Memory-only is deliberate — a token in `localStorage` is readable by any XSS
 * payload and survives the tab. The cost is that a refresh signs the user out
 * until a refresh handler is wired up.
 *
 * If the project later moves to HttpOnly cookie sessions, this file and
 * `attachAuthInterceptors` are the only things that change. Nothing else in
 * the tree may read or store a token.
 */

export interface AuthSession {
  accessToken: string;
  /** Absolute expiry as epoch milliseconds. Backend-reported — never computed locally. */
  expiresAt: number;
}

/** Resolves to a fresh session, or null if the session cannot be renewed. */
export type RefreshHandler = () => Promise<AuthSession | null>;
export type ExpiryListener = () => void;

let current: AuthSession | null = null;
let expiryTimer: ReturnType<typeof setTimeout> | undefined;
let refreshHandler: RefreshHandler | null = null;
let inFlightRefresh: Promise<AuthSession | null> | null = null;
const expiryListeners = new Set<ExpiryListener>();

function clearExpiryTimer(): void {
  if (expiryTimer !== undefined) {
    clearTimeout(expiryTimer);
    expiryTimer = undefined;
  }
}

/** True once we are inside the leeway window before the real expiry. */
export function isExpired(session: AuthSession | null = current): boolean {
  if (!session) return true;
  return Date.now() >= session.expiresAt - AUTH_EXPIRY_LEEWAY_MS;
}

/**
 * Ends the session and tells every listener to log out. Called by the expiry
 * timer, and by the http layer when the server rejects a credential we still
 * believed was valid.
 */
export function expireSession(): void {
  clearSession();
  for (const listener of expiryListeners) listener();
}

/**
 * Schedules the lapse. When the token reaches its leeway window we try the
 * refresh handler once; if there is none, or it declines, listeners fire and
 * the app logs out. This is what stops a user sitting on a dead session.
 */
function scheduleExpiry(session: AuthSession): void {
  clearExpiryTimer();
  const fireIn = session.expiresAt - AUTH_EXPIRY_LEEWAY_MS - Date.now();

  if (fireIn <= 0) {
    expireSession();
    return;
  }

  expiryTimer = setTimeout(() => {
    void (async () => {
      const renewed = await refreshSession();
      if (!renewed) expireSession();
    })();
  }, fireIn);
}

export function setSession(session: AuthSession): void {
  current = session;
  scheduleExpiry(session);
}

export function getSession(): AuthSession | null {
  return current;
}

/** The access token, or null when absent or lapsed. Never returns a dead token. */
export function getAccessToken(): string | null {
  if (!current || isExpired(current)) return null;
  return current.accessToken;
}

export function clearSession(): void {
  clearExpiryTimer();
  current = null;
  inFlightRefresh = null;
}

/**
 * Registers how a lapsed session is renewed. Called once at app wiring, after
 * the auth endpoints exist in the contract. Until then expiry means logout.
 */
export function setRefreshHandler(handler: RefreshHandler | null): void {
  refreshHandler = handler;
}

/**
 * Single-flight renewal: concurrent callers queue behind one attempt rather
 * than firing N refreshes and racing to overwrite each other's token.
 */
export function refreshSession(): Promise<AuthSession | null> {
  if (!refreshHandler) return Promise.resolve(null);
  if (inFlightRefresh) return inFlightRefresh;

  inFlightRefresh = refreshHandler()
    .then((session) => {
      if (session) setSession(session);
      else clearSession();
      return session;
    })
    .catch(() => {
      clearSession();
      return null;
    })
    .finally(() => {
      inFlightRefresh = null;
    });

  return inFlightRefresh;
}

/** Subscribe to session lapse. Returns an unsubscribe function. */
export function onSessionExpired(listener: ExpiryListener): () => void {
  expiryListeners.add(listener);
  return () => {
    expiryListeners.delete(listener);
  };
}

/** Test-only: drop all registered state so cases cannot leak into each other. */
export function __resetAuthSession(): void {
  clearSession();
  refreshHandler = null;
  expiryListeners.clear();
}
