import { describe, expect, it } from 'vitest';
import { apiGet, apiPatch, apiPost } from './client';

const SECTION_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40';

/**
 * TASK-0040: the contract's section operations (`ListSections`,
 * `CreateSection`, `UpdateSection`) reached through the same generic
 * `apiGet`/`apiPost`/`apiPatch` surface `client-roles.test.ts`/
 * `client-sessions.test.ts`/`client-terms.test.ts` established — zero new
 * lines needed in `client.ts`/`client-types.ts`. Level operations are in the
 * sibling `client-levels.test.ts`, split the same way TASK-0033 split
 * roles/privileges, to stay under CONVENTIONS.md §3's 180-line cap.
 */
describe('apiGet — GET /api/v1/sections (ListSections)', () => {
  it('resolves with no query params at all — the contract declares none', async () => {
    const result = await apiGet('/api/v1/sections', undefined);

    expect(Array.isArray(result.sections)).toBe(true);
  });

  it('passing an idempotencyKey fails typecheck — options carries nothing beyond signal/timeout', () => {
    // @ts-expect-error — `ListSections` declares no query params and no
    // `Idempotency-Key` header, so `RequestExtras` contributes nothing and
    // the options type here is just `CallerOptions` minus `headers` — the
    // "trivially empty" half of the two-shape proof `TASK-0040` requires
    // (`ListLevels` below is the "not trivially empty" half).
    void apiGet('/api/v1/sections', undefined, { idempotencyKey: 'a-client-generated-key' });
  });
});

describe('apiPost — POST /api/v1/sections (CreateSection)', () => {
  it('requires Idempotency-Key and threads it through', async () => {
    const result = await apiPost('/api/v1/sections', { name: 'Secondary' }, { idempotencyKey: 'a-client-generated-key' });

    expect(typeof result.id).toBe('string');
  });

  it('omitting Idempotency-Key fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on `POST /sections`
    // (spec 6.4.9), same shape as `POST /roles`; omitting it must not compile.
    void apiPost('/api/v1/sections', { name: 'Secondary' });
  });
});

describe('apiPatch — PATCH /api/v1/sections/{id} (UpdateSection)', () => {
  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch(
      '/api/v1/sections/{id}',
      { id: SECTION_ID, name: 'Secondary' },
      { pathParams: { id: SECTION_ID } },
    );
    expect(typeof withoutKey.id).toBe('string');

    const withKey = await apiPatch(
      '/api/v1/sections/{id}',
      { id: SECTION_ID, name: 'Secondary' },
      { pathParams: { id: SECTION_ID }, idempotencyKey: 'a-client-generated-key' },
    );
    expect(typeof withKey.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/sections/{id}` requires `pathParams`.
    void apiPatch('/api/v1/sections/{id}', { id: SECTION_ID, name: 'Secondary' });
  });
});
