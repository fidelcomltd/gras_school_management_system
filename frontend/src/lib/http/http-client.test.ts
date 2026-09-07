import { describe, expect, it, vi } from 'vitest';
import { getCsrfToken, onSessionEnded, setCsrfToken, setSession } from '@/lib/auth/auth-session';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { getRequest, postRequest } from './request';

/**
 * `http-client.ts`'s interceptors: CSRF attachment, the single CSRF retry, and
 * the terminal-401 path (delta §3a — a cookie session has no second credential,
 * so a 401 ends the session and never calls `/refresh`). The single-flight
 * session-end behaviour (amended AC-5) is `./http-client-single-flight.test.ts`.
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

describe('CSRF header attachment', () => {
  it('attaches no bearer/Authorization header — the session lives in an HttpOnly cookie', async () => {
    const seen = vi.fn();
    server.use(
      http.get(apiUrl('/terms'), ({ request }) => {
        seen(request.headers.get('Authorization'));
        return HttpResponse.json([]);
      }),
    );

    setSession(liveSession());
    await getRequest('/terms');

    expect(seen).toHaveBeenCalledWith(null);
  });

  it('attaches the CSRF header on a mutating request when a token is held', async () => {
    const seen = vi.fn();
    server.use(
      http.post(apiUrl('/terms'), ({ request }) => {
        seen(request.headers.get('X-CSRF-Token'));
        return HttpResponse.json({});
      }),
    );

    setCsrfToken('csrf-abc');
    await postRequest('/terms', {});

    expect(seen).toHaveBeenCalledWith('csrf-abc');
  });

  it('does not attach a CSRF header to a GET request', async () => {
    const seen = vi.fn();
    server.use(
      http.get(apiUrl('/terms'), ({ request }) => {
        seen(request.headers.get('X-CSRF-Token'));
        return HttpResponse.json([]);
      }),
    );

    setCsrfToken('csrf-abc');
    await getRequest('/terms');

    expect(seen).toHaveBeenCalledWith(null);
  });
});

describe('the single CSRF retry', () => {
  it('re-fetches once and retries on a csrf.invalid 403, never a second time', async () => {
    let attempts = 0;
    server.use(
      http.get(apiUrl('/api/v1/auth/csrf'), () => HttpResponse.json({ csrfToken: 'fresh-token' })),
      http.post(apiUrl('/mutate'), ({ request }) => {
        attempts += 1;
        if (request.headers.get('X-CSRF-Token') !== 'fresh-token') {
          return problemResponse(403, { errorCode: 'csrf.invalid' });
        }
        return HttpResponse.json({ ok: true });
      }),
    );

    setCsrfToken('stale-token');
    await expect(postRequest('/mutate', {})).resolves.toEqual({ ok: true });

    expect(attempts).toBe(2);
    expect(getCsrfToken()).toBe('fresh-token');
  });
});

describe('a single 401 — terminal, never retried', () => {
  it('ends the session and notifies listeners, without retrying', async () => {
    const onEnded = vi.fn();
    onSessionEnded(onEnded);
    let calls = 0;
    server.use(
      http.get(apiUrl('/secure'), () => {
        calls += 1;
        return problemResponse(401, { errorCode: 'authentication.session_expired' });
      }),
    );

    setSession(liveSession());
    await expect(getRequest('/secure')).rejects.toThrow();

    expect(onEnded).toHaveBeenCalledTimes(1);
    expect(calls).toBe(1);
  });

  it('never calls POST /auth/refresh (delta §3a: reactive 401s are terminal)', async () => {
    let refreshCalls = 0;
    server.use(
      http.get(apiUrl('/secure'), () => problemResponse(401)),
      http.post(apiUrl('/api/v1/auth/refresh'), () => {
        refreshCalls += 1;
        return HttpResponse.json(liveSession());
      }),
    );

    setSession(liveSession());
    await expect(getRequest('/secure')).rejects.toThrow();

    expect(refreshCalls).toBe(0);
  });
});
