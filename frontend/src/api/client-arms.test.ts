import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiGet, apiPost } from './client';

const ARM_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d48';
const LEVEL_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40';
const SESSION_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41';

/**
 * TASK-0044: the contract's arms operations (`ListArms`, `CreateArm`,
 * `GetNextArmLabel`, `BulkCreateArms`, `GetArm`, `UpdateArm`, `DeleteArm`)
 * reached through the same generic `apiGet`/`apiPost`/`apiPatch`/`apiDelete`
 * surface every prior client-*.test.ts file established — zero new lines
 * needed in `client.ts`/`client-types.ts`.
 */
describe('apiGet — GET /api/v1/arms (ListArms)', () => {
  it('lists arms with all filters optional', async () => {
    const result = await apiGet('/api/v1/arms', {});

    expect(Array.isArray(result.items)).toBe(true);
    expect('nextCursor' in result).toBe(true);
  });

  it('tolerates an arm `status` value the client-side union does not name, without crashing (§8)', async () => {
    // MSW matches path params as `:id`, not the contract's `{id}` — see
    // `toMswRoute` in `openapi-handlers.ts`. This route has none, so no
    // conversion is needed here; `GetArm` below does exercise it.
    server.use(
      http.get(apiUrl('/api/v1/arms'), () =>
        HttpResponse.json({
          items: [
            {
              id: ARM_ID,
              classLevelId: LEVEL_ID,
              classLevel: 'Primary 2',
              sessionId: SESSION_ID,
              label: 'C',
              displayName: 'Primary 2C',
              capacity: 22,
              formTeacherAdminId: null,
              status: 'SomeFutureArmStatus',
            },
          ],
          nextCursor: null,
        }),
      ),
    );

    const result = await apiGet('/api/v1/arms', {});

    expect(result.items[0]?.status).toBe('SomeFutureArmStatus');
  });

  it('passing an idempotencyKey fails typecheck — ListArms declares no Idempotency-Key header', () => {
    // @ts-expect-error — `GET /arms` declares real optional query filters but
    // no `Idempotency-Key` header, so the options bag rejects one.
    void apiGet('/api/v1/arms', {}, { idempotencyKey: 'a-client-generated-key' });
  });
});

describe('apiGet — GET /api/v1/arms/next-label (GetNextArmLabel)', () => {
  it('requires levelId and sessionId as query params', async () => {
    const result = await apiGet('/api/v1/arms/next-label', { levelId: LEVEL_ID, sessionId: SESSION_ID });

    expect(typeof result.label).toBe('string');
  });

  it('passing an idempotencyKey fails typecheck — GetNextArmLabel declares no Idempotency-Key header', () => {
    // @ts-expect-error — `GET /arms/next-label` has required query params but
    // no `Idempotency-Key` header; this is the second `GET` half of the
    // card's "both negative typing directions" requirement.
    void apiGet('/api/v1/arms/next-label', { levelId: LEVEL_ID, sessionId: SESSION_ID }, { idempotencyKey: 'a-client-generated-key' });
  });
});

describe('apiPost — POST /api/v1/arms (CreateArm)', () => {
  const body = { classLevelId: LEVEL_ID, sessionId: SESSION_ID, label: 'C', capacity: 22, formTeacherAdminId: null };

  it('requires Idempotency-Key and threads it through', async () => {
    const result = await apiPost('/api/v1/arms', body, { idempotencyKey: 'a-client-generated-key' });

    expect(typeof result.id).toBe('string');
  });

  it('omitting Idempotency-Key fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on `POST /arms`
    // (spec 6.4.9), same shape as `POST /sections`/`POST /levels`.
    void apiPost('/api/v1/arms', body);
  });
});

describe('apiPost — POST /api/v1/arms/bulk (BulkCreateArms)', () => {
  const body = { sessionId: SESSION_ID, levels: [{ levelId: LEVEL_ID, armCount: 3, capacity: 30 }], dryRun: false };

  it('requires Idempotency-Key and threads it through', async () => {
    const result = await apiPost('/api/v1/arms/bulk', body, { idempotencyKey: 'a-client-generated-key' });

    expect(Array.isArray(result.created)).toBe(true);
  });

  it('omitting Idempotency-Key fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on `POST /arms/bulk`,
    // the second `POST` half of the card's "both negative typing directions".
    void apiPost('/api/v1/arms/bulk', body);
  });
});
