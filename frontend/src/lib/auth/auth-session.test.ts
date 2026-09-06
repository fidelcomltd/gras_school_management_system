import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  __resetAuthSession,
  getCsrfToken,
  getSession,
  onSessionEnded,
  registerKeepaliveCaller,
  setCsrfToken,
  setSession,
  terminateSession,
  type AuthSession,
} from './auth-session';

/** The keepalive SCHEDULE itself is `./auth-session-keepalive.test.ts`. */

const HOUR = 60 * 60 * 1000;
const NOW = Date.parse('2026-09-06T09:00:00+00:00');

function sessionEndingIn(idleMs: number, absoluteMs = 8 * HOUR): AuthSession {
  return {
    accountId: 'acc-1',
    email: 'admin@example.com',
    staffName: 'Chisom Maxwell',
    isSuperAdmin: true,
    mustChangePassword: false,
    effectivePrivileges: [],
    sessionExpiresAt: new Date(NOW + idleMs).toISOString(),
    sessionAbsoluteExpiresAt: new Date(NOW + absoluteMs).toISOString(),
  };
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(NOW);
  __resetAuthSession();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('session state', () => {
  it('has no session before one is set', () => {
    expect(getSession()).toBeNull();
  });

  it('records the latest session', () => {
    const session = sessionEndingIn(HOUR);
    setSession(session);
    expect(getSession()).toEqual(session);
  });
});

describe('CSRF token', () => {
  it('starts unset', () => {
    expect(getCsrfToken()).toBeNull();
  });

  it('stores whatever is set, verbatim', () => {
    setCsrfToken('opaque-token-value');
    expect(getCsrfToken()).toBe('opaque-token-value');
  });
});

describe('terminateSession — the one session-end path', () => {
  it('clears the session and notifies listeners', () => {
    setSession(sessionEndingIn(HOUR));
    const onEnded = vi.fn();
    onSessionEnded(onEnded);

    terminateSession();

    expect(getSession()).toBeNull();
    expect(onEnded).toHaveBeenCalledTimes(1);
  });

  it('is idempotent: calling it again before a new session starts is a no-op', () => {
    setSession(sessionEndingIn(HOUR));
    const onEnded = vi.fn();
    onSessionEnded(onEnded);

    terminateSession();
    terminateSession();
    terminateSession();

    expect(onEnded).toHaveBeenCalledTimes(1);
  });

  it('collapses several terminations in the same tick into one notification', () => {
    setSession(sessionEndingIn(HOUR));
    const onEnded = vi.fn();
    onSessionEnded(onEnded);

    // Models several concurrent 401s each reacting in the same microtask turn.
    for (let i = 0; i < 5; i += 1) terminateSession();

    expect(onEnded).toHaveBeenCalledTimes(1);
  });

  it('is a no-op when there is no active session', () => {
    const onEnded = vi.fn();
    onSessionEnded(onEnded);

    terminateSession();

    expect(onEnded).not.toHaveBeenCalled();
  });

  it('stops notifying after unsubscribe', () => {
    setSession(sessionEndingIn(HOUR));
    const onEnded = vi.fn();
    const unsubscribe = onSessionEnded(onEnded);
    unsubscribe();

    terminateSession();

    expect(onEnded).not.toHaveBeenCalled();
  });

  it('cancels the pending keepalive timer', async () => {
    const caller = vi.fn().mockResolvedValue(sessionEndingIn(4 * HOUR));
    registerKeepaliveCaller(caller);

    setSession(sessionEndingIn(HOUR));
    terminateSession();
    await vi.advanceTimersByTimeAsync(HOUR);

    expect(caller).not.toHaveBeenCalled();
  });
});
