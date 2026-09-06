import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AUTH_EXPIRY_LEEWAY_MS } from '@/config/env-values';
import {
  __resetAuthSession,
  __triggerKeepaliveForTest,
  getSession,
  onSessionEnded,
  registerKeepaliveCaller,
  setSession,
  type AuthSession,
} from './auth-session';

/**
 * The proactive keepalive schedule — ruling 4 (derived from the backend-reported
 * deadlines, re-armed from every response, clamped at the absolute cap) and
 * amended AC-5's second half (the keepalive is itself single-flight). Split from
 * `auth-session.test.ts` to keep each file focused.
 */

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

describe('proactive keepalive scheduling', () => {
  it('calls the registered keepalive caller ahead of the reported expiry, by the configured leeway', async () => {
    const renewed = sessionEndingIn(4 * HOUR);
    const caller = vi.fn().mockResolvedValue(renewed);
    registerKeepaliveCaller(caller);

    setSession(sessionEndingIn(HOUR));
    expect(caller).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(HOUR - AUTH_EXPIRY_LEEWAY_MS);
    expect(caller).toHaveBeenCalledTimes(1);
  });

  it('re-arms the schedule from every renewed AuthSessionResponse', async () => {
    const caller = vi.fn().mockResolvedValue(sessionEndingIn(4 * HOUR));
    registerKeepaliveCaller(caller);

    setSession(sessionEndingIn(HOUR));
    await vi.advanceTimersByTimeAsync(HOUR - AUTH_EXPIRY_LEEWAY_MS);
    expect(caller).toHaveBeenCalledTimes(1);

    // The renewed session pushes expiry out by another 4h — nothing should fire
    // again until that new deadline, not the original one.
    await vi.advanceTimersByTimeAsync(HOUR);
    expect(caller).toHaveBeenCalledTimes(1);
  });

  it('never schedules a call while mustChangePassword is set', async () => {
    const caller = vi.fn().mockResolvedValue(sessionEndingIn(4 * HOUR));
    registerKeepaliveCaller(caller);

    setSession({ ...sessionEndingIn(HOUR), mustChangePassword: true });
    await vi.advanceTimersByTimeAsync(4 * HOUR);

    expect(caller).not.toHaveBeenCalled();
  });

  it('goes straight to ending the session at the absolute cap instead of calling refresh', async () => {
    const caller = vi.fn();
    registerKeepaliveCaller(caller);
    const onEnded = vi.fn();
    onSessionEnded(onEnded);

    // Idle window would next fire past the absolute cap — refreshing would not
    // actually extend anything, so the schedule should end the session instead.
    setSession(sessionEndingIn(8 * HOUR, 8 * HOUR));
    await vi.advanceTimersByTimeAsync(8 * HOUR);

    expect(caller).not.toHaveBeenCalled();
    expect(onEnded).toHaveBeenCalledTimes(1);
    expect(getSession()).toBeNull();
  });

  it('resolves to null when no caller is registered', async () => {
    registerKeepaliveCaller(null);
    await expect(__triggerKeepaliveForTest()).resolves.toBeNull();
  });

  it('is itself single-flight: two overlapping triggers produce one refresh call', async () => {
    let resolveCall!: (session: AuthSession) => void;
    const caller = vi.fn().mockReturnValue(
      new Promise<AuthSession>((resolve) => {
        resolveCall = resolve;
      }),
    );
    registerKeepaliveCaller(caller);
    setSession(sessionEndingIn(HOUR));

    const first = __triggerKeepaliveForTest();
    const second = __triggerKeepaliveForTest();

    resolveCall(sessionEndingIn(4 * HOUR));
    await Promise.all([first, second]);

    expect(caller).toHaveBeenCalledTimes(1);
  });
});
