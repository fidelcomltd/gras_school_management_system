import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiDelete, apiGet, apiPatch, apiPost } from './client';

const LEVEL_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d46';
const SECTION_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40';

/**
 * TASK-0040: the contract's class-level operations (`ListLevels`,
 * `CreateLevel`, `GetLevel`, `UpdateLevel`, `DeleteLevel`, `ReorderLevels`)
 * reached through the same generic `apiGet`/`apiPost`/`apiPatch`/`apiDelete`
 * surface `client-roles.test.ts` established — zero new lines needed in
 * `client.ts`/`client-types.ts`. Section operations are in the sibling
 * `client-sections.test.ts`, split to stay under CONVENTIONS.md §3's
 * 180-line cap.
 */
describe('apiGet — GET /api/v1/levels (ListLevels)', () => {
  it('lists levels with optional cursor/pageSize/status filters', async () => {
    const result = await apiGet('/api/v1/levels', { status: 'Active' });

    expect(Array.isArray(result.items)).toBe(true);
    expect('nextCursor' in result).toBe(true);
  });

  it('resolves with an empty query object — every ListLevels filter is independently optional', async () => {
    const result = await apiGet('/api/v1/levels', {});

    expect(Array.isArray(result.items)).toBe(true);
  });

  it('tolerates a level `status` value the client-side union does not name, without crashing (§8)', async () => {
    // `LevelStatus` is generated as `"Active" | "Inactive"` — a compile-time
    // union only, the sibling proof to `client-roles.test.ts`'s `RoleStatus`
    // one. MSW matches path params as `:id`, not the contract's `{id}` — see
    // `toMswRoute` in `openapi-handlers.ts` — though this route has none.
    server.use(
      http.get(apiUrl('/api/v1/levels'), () =>
        HttpResponse.json({
          items: [
            {
              id: LEVEL_ID,
              name: 'Primary 1',
              sectionId: SECTION_ID,
              section: 'Primary',
              progressionOrder: 4,
              nextLevelId: null,
              isEntryLevel: false,
              isGraduatingLevel: true,
              status: 'SomeFutureLevelStatus',
            },
          ],
          nextCursor: null,
        }),
      ),
    );

    const result = await apiGet('/api/v1/levels', {});

    expect(result.items[0]?.status).toBe('SomeFutureLevelStatus');
  });

  it('passing an idempotencyKey fails typecheck — options is not trivially empty (still carries signal/timeout)', () => {
    // @ts-expect-error — same excess-property rejection `client-sections.test.ts`
    // proves for `ListSections`, proven here on an operation whose query type
    // is NOT trivially empty, so this isn't just "an empty type rejects
    // everything".
    void apiGet('/api/v1/levels', {}, { idempotencyKey: 'a-client-generated-key' });
  });
});

describe('apiPost — POST /api/v1/levels (CreateLevel)', () => {
  const body = { name: 'Reception', sectionId: SECTION_ID, progressionOrder: null, nextLevelId: null, insertAfterLevelId: LEVEL_ID };

  it('requires Idempotency-Key and threads it through', async () => {
    const result = await apiPost('/api/v1/levels', body, { idempotencyKey: 'a-client-generated-key' });

    expect(typeof result.id).toBe('string');
  });

  it('omitting Idempotency-Key fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on `POST /levels`
    // (spec 6.4.9), same shape as `POST /sections`/`POST /roles`.
    void apiPost('/api/v1/levels', body);
  });
});

describe('apiGet — GET /api/v1/levels/{id} (GetLevel)', () => {
  it('threads the required path parameter', async () => {
    const result = await apiGet('/api/v1/levels/{id}', undefined, { pathParams: { id: LEVEL_ID } });

    expect(typeof result.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/levels/{id}` declares `id` required.
    void apiGet('/api/v1/levels/{id}', undefined);
  });
});

describe('apiPatch — PATCH /api/v1/levels/{id} (UpdateLevel)', () => {
  const body = { id: LEVEL_ID, name: null, sectionId: null, nextLevelId: null, progressionOrder: null, status: null };

  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch('/api/v1/levels/{id}', body, { pathParams: { id: LEVEL_ID } });
    expect(typeof withoutKey.id).toBe('string');

    const withKey = await apiPatch('/api/v1/levels/{id}', body, {
      pathParams: { id: LEVEL_ID },
      idempotencyKey: 'a-client-generated-key',
    });
    expect(typeof withKey.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/levels/{id}` requires `pathParams`.
    void apiPatch('/api/v1/levels/{id}', body);
  });
});

describe('apiDelete — DELETE /api/v1/levels/{id} (DeleteLevel)', () => {
  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiDelete('/api/v1/levels/{id}', { pathParams: { id: LEVEL_ID } });
    expect(withoutKey).toBeUndefined();

    const withKey = await apiDelete('/api/v1/levels/{id}', {
      pathParams: { id: LEVEL_ID },
      idempotencyKey: 'a-client-generated-key',
    });
    expect(withKey).toBeUndefined();
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/levels/{id}` requires `pathParams`.
    void apiDelete('/api/v1/levels/{id}');
  });
});

describe('apiPost — POST /api/v1/levels/reorder (ReorderLevels)', () => {
  // The 200 response schema is a bare `LevelDto[]` with no top-level
  // `example` (unlike every other operation here), so `openapi-handlers.ts`'s
  // `exampleFor` finds nothing and the contract-derived default handler
  // answers with an empty body. Overridden here rather than loosening the
  // assertion — same category of catch as `client-terms.test.ts`'s `:id` fix.
  const reorderedResponse = [
    { id: LEVEL_ID, name: 'Primary 1', sectionId: SECTION_ID, section: 'Primary', progressionOrder: 1, nextLevelId: SECTION_ID, isEntryLevel: true, isGraduatingLevel: false, status: 'Active' },
  ];

  it('takes the whole ordered array with no path parameter and Idempotency-Key optional', async () => {
    server.use(http.post(apiUrl('/api/v1/levels/reorder'), () => HttpResponse.json(reorderedResponse)));

    const result = await apiPost(
      '/api/v1/levels/reorder',
      { orderedLevelIds: [LEVEL_ID, SECTION_ID] },
      { idempotencyKey: 'a-client-generated-key' },
    );
    expect(Array.isArray(result)).toBe(true);

    const withoutKey = await apiPost('/api/v1/levels/reorder', { orderedLevelIds: [LEVEL_ID, SECTION_ID] });
    expect(Array.isArray(withoutKey)).toBe(true);
  });
});
