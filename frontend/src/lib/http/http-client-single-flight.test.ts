import { describe, expect, it, vi } from 'vitest';
import { __triggerKeepaliveForTest, onSessionEnded, setSession } from '@/lib/auth/auth-session';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { getRequest } from './request';

/**
 * TASK-0021 amended AC-5: "one session-end path, single-flight" no longer means
 * queuing behind a refresh (delta §3a — a cookie session has no second credential
 * to retry with). It means several requests failing at once produce ONE sign-out
 * transition, never `/refresh` reactively; and the proactive keepalive is itself
 * single-flight. Split from `http-client.test.ts` to keep each file focused.
 */

function liveSession() {
  return {
    accountId: 'acc-1',
    email: 'admin@example.com',
    staffName: 'Chisom Maxwell',
    isSuperAdmin: true,
    mustChangePassword: false,
    effectivePrivileges: [],
    sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
  };
}

describe('concurrent 401s collapse into one sign-out, zero refresh calls', () => {
  it('two requests failing at the same time produce exactly one session-end notification', async () => {
    let refreshCalls = 0;
    server.use(
      http.get(apiUrl('/secure-a'), () => problemResponse(401)),
      http.get(apiUrl('/secure-b'), () => problemResponse(401)),
      http.post(apiUrl('/api/v1/auth/refresh'), () => {
        refreshCalls += 1;
        return HttpResponse.json(liveSession());
      }),
    );

    setSession(liveSession());
    const onEnded = vi.fn();
    onSessionEnded(onEnded);

    const results = await Promise.allSettled([getRequest('/secure-a'), getRequest('/secure-b')]);

    expect(results.every((result) => result.status === 'rejected')).toBe(true);
    expect(onEnded).toHaveBeenCalledTimes(1);
    expect(refreshCalls).toBe(0);
  });
});

describe('proactive keepalive is single-flight end-to-end', () => {
  it('two overlapping triggers produce exactly one POST /auth/refresh call', async () => {
    let refreshCalls = 0;
    server.use(
      http.post(apiUrl('/api/v1/auth/refresh'), () => {
        refreshCalls += 1;
        return HttpResponse.json(liveSession());
      }),
    );

    setSession(liveSession());

    const [first, second] = await Promise.all([
      __triggerKeepaliveForTest(),
      __triggerKeepaliveForTest(),
    ]);

    expect(refreshCalls).toBe(1);
    expect(first).not.toBeNull();
    expect(second).not.toBeNull();
  });
});
