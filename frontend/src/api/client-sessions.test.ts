import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiGet, apiPatch, apiPost } from './client';

const SESSION_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41';

/**
 * TASK-0037: the contract's session operations (`CreateSession`,
 * `ListSessions`, `GetSession`, `UpdateSession`) reached through the same
 * generic `apiGet`/`apiPost`/`apiPatch` surface `client-roles.test.ts`
 * established — zero new lines needed in `client.ts`/`client-types.ts`. Term
 * operations are in the sibling `client-terms.test.ts`, split the same way
 * TASK-0033 split roles/privileges out of `client.test.ts`, to stay under
 * CONVENTIONS.md §3's 180-line cap.
 */
describe('apiPost — POST /api/v1/sessions (CreateSession)', () => {
  const body = {
    name: '2027/2028',
    startDate: '2027-09-13',
    endDate: '2028-07-23',
    term1: { startDate: '2027-09-13', endDate: '2027-12-17', nextResumptionDate: '2028-01-04' },
    term2: { startDate: '2028-01-04', endDate: '2028-04-01', nextResumptionDate: '2028-04-19' },
    term3: { startDate: '2028-04-19', endDate: '2028-07-23', nextResumptionDate: null },
  };

  it('requires Idempotency-Key and threads it through', async () => {
    const result = await apiPost('/api/v1/sessions', body, { idempotencyKey: 'a-client-generated-key' });

    expect(typeof result.id).toBe('string');
    expect(result.terms).toHaveLength(3);
  });

  it('omitting Idempotency-Key fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on `POST /sessions`
    // (spec 6.3.5), same shape as `POST /roles`; omitting it must not compile.
    void apiPost('/api/v1/sessions', body);
  });
});

describe('apiGet — GET /api/v1/sessions (ListSessions)', () => {
  it('lists sessions with an optional state filter', async () => {
    const result = await apiGet('/api/v1/sessions', { state: 'Active' });

    expect(Array.isArray(result.items)).toBe(true);
    expect('nextCursor' in result).toBe(true);
  });

  it('resolves with an empty query object — every ListSessions filter is independently optional', async () => {
    const result = await apiGet('/api/v1/sessions', {});

    expect(Array.isArray(result.items)).toBe(true);
  });

  it('tolerates a session `state` value the client-side union does not name, without crashing (§8)', async () => {
    // `SessionState` is generated as `"Upcoming" | "Active" | "Closed"` — a
    // compile-time union only. Nothing in `client.ts`/`client-types.ts`
    // validates or narrows a response body at runtime, so a server that has
    // additively grown a fourth state must still pass straight through.
    server.use(
      http.get(apiUrl('/api/v1/sessions'), () =>
        HttpResponse.json({
          items: [
            {
              id: SESSION_ID,
              name: '2028/2029',
              startDate: '2028-09-11',
              endDate: '2029-07-21',
              state: 'SomeFutureState',
            },
          ],
          nextCursor: null,
        }),
      ),
    );

    const result = await apiGet('/api/v1/sessions', {});

    expect(result.items[0]?.state).toBe('SomeFutureState');
  });

  it('passing an idempotencyKey fails typecheck — a GET declares no Idempotency-Key header', () => {
    // @ts-expect-error — same excess-property rejection `client-roles.test.ts`
    // proves for `GET /roles`: `ListSessions` declares no `Idempotency-Key`
    // header, so `RequestExtras` contributes nothing and this fresh object
    // literal is rejected outright.
    void apiGet('/api/v1/sessions', {}, { idempotencyKey: 'a-client-generated-key' });
  });
});

describe('apiGet — GET /api/v1/sessions/{id} (GetSession)', () => {
  it('threads the required path parameter', async () => {
    const result = await apiGet('/api/v1/sessions/{id}', undefined, { pathParams: { id: SESSION_ID } });

    expect(typeof result.id).toBe('string');
    expect(result.terms).toHaveLength(3);
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/sessions/{id}` declares `id` required.
    void apiGet('/api/v1/sessions/{id}', undefined);
  });
});

describe('apiPatch — PATCH /api/v1/sessions/{id} (UpdateSession)', () => {
  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch(
      '/api/v1/sessions/{id}',
      { id: SESSION_ID, name: null, startDate: null, endDate: null },
      { pathParams: { id: SESSION_ID } },
    );
    expect(typeof withoutKey.id).toBe('string');

    const withKey = await apiPatch(
      '/api/v1/sessions/{id}',
      { id: SESSION_ID, name: null, startDate: null, endDate: null },
      { pathParams: { id: SESSION_ID }, idempotencyKey: 'a-client-generated-key' },
    );
    expect(typeof withKey.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/sessions/{id}` requires `pathParams`.
    void apiPatch('/api/v1/sessions/{id}', { id: SESSION_ID, name: null, startDate: null, endDate: null });
  });
});
