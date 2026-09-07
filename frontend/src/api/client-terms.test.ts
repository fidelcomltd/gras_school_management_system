import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiPatch, apiPost } from './client';

const TERM_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40';

/**
 * TASK-0037: the contract's term operations (`UpdateTerm`, `OpenTerm`,
 * `CloseTerm`, `ReopenTerm`) reached through the same generic `apiPost`/
 * `apiPatch` surface `client-roles.test.ts`/`client-sessions.test.ts`
 * established — zero new lines needed in `client.ts`/`client-types.ts`. Split
 * from `client-sessions.test.ts` to stay under CONVENTIONS.md §3's 180-line
 * cap.
 */
describe('apiPatch — PATCH /api/v1/terms/{id} (UpdateTerm)', () => {
  const body = {
    id: TERM_ID,
    name: null,
    startDate: null,
    endDate: null,
    timesSchoolOpened: null,
    nextResumptionDate: null,
  };

  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch('/api/v1/terms/{id}', body, { pathParams: { id: TERM_ID } });
    expect(typeof withoutKey.id).toBe('string');

    const withKey = await apiPatch('/api/v1/terms/{id}', body, {
      pathParams: { id: TERM_ID },
      idempotencyKey: 'a-client-generated-key',
    });
    expect(typeof withKey.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/terms/{id}` requires `pathParams`.
    void apiPatch('/api/v1/terms/{id}', body);
  });
});

describe('apiPost — POST /api/v1/terms/{id}/open (OpenTerm)', () => {
  it('threads the path parameter with no request body', async () => {
    const result = await apiPost('/api/v1/terms/{id}/open', undefined, { pathParams: { id: TERM_ID } });

    expect(typeof result.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/terms/{id}/open` requires `pathParams`.
    void apiPost('/api/v1/terms/{id}/open', undefined);
  });

  it('tolerates a term `state` value the client-side union does not name, without crashing (§8)', async () => {
    // `TermState` is generated as `"Upcoming" | "Active" | "Closed"` — a
    // compile-time union only, the sibling proof to `client-sessions.test.ts`'s
    // `SessionState` one.
    server.use(
      // MSW matches path params as `:id`, not the contract's `{id}` — see
      // `toMswRoute` in `openapi-handlers.ts`.
      http.post(apiUrl('/api/v1/terms/:id/open'), () =>
        HttpResponse.json({
          id: TERM_ID,
          sessionId: TERM_ID,
          ordinal: 1,
          name: 'First Term',
          startDate: '2026-09-14',
          endDate: '2026-12-18',
          timesSchoolOpened: null,
          nextResumptionDate: null,
          state: 'SomeFutureTermState',
          closedAtUtc: null,
          closedBy: null,
        }),
      ),
    );

    const result = await apiPost('/api/v1/terms/{id}/open', undefined, { pathParams: { id: TERM_ID } });

    expect(result.state).toBe('SomeFutureTermState');
  });
});

describe('apiPost — POST /api/v1/terms/{id}/close (CloseTerm)', () => {
  it('threads the path parameter with no request body', async () => {
    const result = await apiPost('/api/v1/terms/{id}/close', undefined, { pathParams: { id: TERM_ID } });

    expect(typeof result.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/terms/{id}/close` requires `pathParams`.
    void apiPost('/api/v1/terms/{id}/close', undefined);
  });
});

describe('apiPost — POST /api/v1/terms/{id}/reopen (ReopenTerm)', () => {
  const body = {
    id: TERM_ID,
    reason: 'A mark was entered against the wrong subject and discovered after publication.',
  };

  it('requires a body and accepts Idempotency-Key as optional', async () => {
    const result = await apiPost('/api/v1/terms/{id}/reopen', body, {
      pathParams: { id: TERM_ID },
      idempotencyKey: 'a-client-generated-key',
    });

    expect(typeof result.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/terms/{id}/reopen` requires `pathParams`.
    void apiPost('/api/v1/terms/{id}/reopen', body);
  });
});
