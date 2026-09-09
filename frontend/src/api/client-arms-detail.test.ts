import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiDelete, apiGet, apiPatch } from './client';

const ARM_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d48';
const LEVEL_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40';
const SESSION_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41';

/**
 * TASK-0044: the by-id arm operations (`GetArm`, `UpdateArm`, `DeleteArm`),
 * split from `client-arms.test.ts` to stay under CONVENTIONS.md §3's 180-line
 * cap. Also carries the §8 enum-tolerance proof on a route that DOES have a
 * path parameter, so the `{id}` → `:id` MSW conversion (`toMswRoute` in
 * `openapi-handlers.ts`) is actually exercised rather than sidestepped —
 * TASK-0037 lost a run to writing an override route as `{id}` verbatim.
 */
describe('apiGet — GET /api/v1/arms/{id} (GetArm)', () => {
  it('threads the required path parameter', async () => {
    const result = await apiGet('/api/v1/arms/{id}', undefined, { pathParams: { id: ARM_ID } });

    expect(typeof result.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/arms/{id}` declares `id` required.
    void apiGet('/api/v1/arms/{id}', undefined);
  });

  it('tolerates an arm `status` value the client-side union does not name, without crashing (§8)', async () => {
    // Route written as `:id`, never the contract's `{id}` — MSW does not
    // treat `{id}` as a parameter matcher.
    server.use(
      http.get(apiUrl('/api/v1/arms/:id'), () =>
        HttpResponse.json({
          id: ARM_ID,
          classLevelId: LEVEL_ID,
          classLevel: 'Primary 2',
          sessionId: SESSION_ID,
          label: 'C',
          displayName: 'Primary 2C',
          capacity: 22,
          formTeacherAdminId: null,
          status: 'SomeFutureArmStatus',
        }),
      ),
    );

    const result = await apiGet('/api/v1/arms/{id}', undefined, { pathParams: { id: ARM_ID } });

    expect(result.status).toBe('SomeFutureArmStatus');
  });
});

describe('apiPatch — PATCH /api/v1/arms/{id} (UpdateArm)', () => {
  const body = { id: ARM_ID, label: null, capacity: null, formTeacherAdminId: null, status: null };

  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch('/api/v1/arms/{id}', body, { pathParams: { id: ARM_ID } });
    expect(typeof withoutKey.id).toBe('string');

    const withKey = await apiPatch('/api/v1/arms/{id}', body, {
      pathParams: { id: ARM_ID },
      idempotencyKey: 'a-client-generated-key',
    });
    expect(typeof withKey.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/arms/{id}` requires `pathParams`.
    void apiPatch('/api/v1/arms/{id}', body);
  });
});

describe('apiDelete — DELETE /api/v1/arms/{id} (DeleteArm)', () => {
  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiDelete('/api/v1/arms/{id}', { pathParams: { id: ARM_ID } });
    expect(withoutKey).toBeUndefined();

    const withKey = await apiDelete('/api/v1/arms/{id}', {
      pathParams: { id: ARM_ID },
      idempotencyKey: 'a-client-generated-key',
    });
    expect(withKey).toBeUndefined();
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/arms/{id}` requires `pathParams`.
    void apiDelete('/api/v1/arms/{id}');
  });
});
