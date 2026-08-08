import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AUTH_EXPIRY_LEEWAY_MS } from '@/config/env-values';
import {
  __resetAuthSession,
  clearSession,
  expireSession,
  getAccessToken,
  getSession,
  isExpired,
  onSessionExpired,
  refreshSession,
  setRefreshHandler,
  setSession,
} from './auth-session';

const HOUR = 60 * 60 * 1000;
const sessionIn = (ms: number) => ({ accessToken: 'token-abc', expiresAt: Date.now() + ms });

beforeEach(() => {
  vi.useFakeTimers();
  __resetAuthSession();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('token access', () => {
  it('returns the token while the session is live', () => {
    setSession(sessionIn(HOUR));
    expect(getAccessToken()).toBe('token-abc');
  });

  it('returns null when there is no session', () => {
    expect(getAccessToken()).toBeNull();
    expect(getSession()).toBeNull();
  });

  it('withholds a token that is inside the leeway window', () => {
    // Still valid by the raw clock, but too close to expiry to send safely.
    setSession(sessionIn(AUTH_EXPIRY_LEEWAY_MS - 1000));
    expect(isExpired()).toBe(true);
    expect(getAccessToken()).toBeNull();
  });

  it('treats an absent session as expired', () => {
    expect(isExpired(null)).toBe(true);
  });

  it('forgets the token after clearSession', () => {
    setSession(sessionIn(HOUR));
    clearSession();
    expect(getAccessToken()).toBeNull();
  });
});

describe('scheduled expiry', () => {
  it('notifies listeners when the token lapses', async () => {
    const onExpired = vi.fn();
    onSessionExpired(onExpired);
    setSession(sessionIn(HOUR));

    expect(onExpired).not.toHaveBeenCalled();
    // Async advance: the timer awaits a refresh attempt before giving up, so
    // the logout lands a microtask after the timer itself fires.
    await vi.advanceTimersByTimeAsync(HOUR - AUTH_EXPIRY_LEEWAY_MS);

    expect(onExpired).toHaveBeenCalledTimes(1);
    expect(getAccessToken()).toBeNull();
  });

  it('fires immediately for a session that is already past its leeway', () => {
    const onExpired = vi.fn();
    onSessionExpired(onExpired);
    setSession(sessionIn(-1000));
    expect(onExpired).toHaveBeenCalledTimes(1);
  });

  it('cancels the previous timer when a new session replaces it', async () => {
    const onExpired = vi.fn();
    onSessionExpired(onExpired);
    setSession(sessionIn(HOUR));
    setSession(sessionIn(4 * HOUR));

    await vi.advanceTimersByTimeAsync(HOUR);
    expect(onExpired).not.toHaveBeenCalled();
  });

  it('stops notifying after unsubscribe', async () => {
    const onExpired = vi.fn();
    const unsubscribe = onSessionExpired(onExpired);
    unsubscribe();

    setSession(sessionIn(HOUR));
    await vi.advanceTimersByTimeAsync(HOUR);
    expect(onExpired).not.toHaveBeenCalled();
  });

  it('renews instead of logging out when a refresh handler succeeds', async () => {
    const onExpired = vi.fn();
    onSessionExpired(onExpired);
    setRefreshHandler(vi.fn().mockResolvedValue(sessionIn(4 * HOUR)));

    setSession(sessionIn(HOUR));
    await vi.advanceTimersByTimeAsync(HOUR - AUTH_EXPIRY_LEEWAY_MS);

    expect(onExpired).not.toHaveBeenCalled();
    expect(getAccessToken()).toBe('token-abc');
  });
});

describe('refresh', () => {
  it('resolves to null when no handler is registered', async () => {
    await expect(refreshSession()).resolves.toBeNull();
  });

  it('runs a single refresh for concurrent callers', async () => {
    const handler = vi.fn().mockResolvedValue(sessionIn(HOUR));
    setRefreshHandler(handler);

    const results = await Promise.all([refreshSession(), refreshSession(), refreshSession()]);

    expect(handler).toHaveBeenCalledTimes(1);
    expect(results.every((session) => session !== null)).toBe(true);
  });

  it('clears the session when the handler rejects', async () => {
    setSession(sessionIn(HOUR));
    setRefreshHandler(vi.fn().mockRejectedValue(new Error('network down')));

    await expect(refreshSession()).resolves.toBeNull();
    expect(getAccessToken()).toBeNull();
  });

  it('clears the session when the handler declines to renew', async () => {
    setSession(sessionIn(HOUR));
    setRefreshHandler(vi.fn().mockResolvedValue(null));

    await expect(refreshSession()).resolves.toBeNull();
    expect(getAccessToken()).toBeNull();
  });

  it('allows a fresh attempt after the in-flight one settles', async () => {
    const handler = vi.fn().mockResolvedValue(sessionIn(HOUR));
    setRefreshHandler(handler);

    await refreshSession();
    await refreshSession();

    expect(handler).toHaveBeenCalledTimes(2);
  });
});

describe('expireSession', () => {
  it('tears down the session and notifies', () => {
    const onExpired = vi.fn();
    onSessionExpired(onExpired);
    setSession(sessionIn(HOUR));

    expireSession();

    expect(onExpired).toHaveBeenCalledTimes(1);
    expect(getSession()).toBeNull();
  });
});
