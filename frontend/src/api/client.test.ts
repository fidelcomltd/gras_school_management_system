import { describe, expect, it } from 'vitest';
import { apiDelete, apiGet, apiPatch, apiPost } from './client';

const ADMIN_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40';

// The privileges/roles operations (TASK-0033) are covered in the colocated
// `client-roles.test.ts`, split out to stay under CONVENTIONS.md §3's 180-line
// file cap rather than growing this file past it.

/**
 * Proves the typed layer end to end against the real transport (`@/lib/http`)
 * and the contract-derived MSW mock (`@/test/msw/handlers.ts`) — not a mock
 * of `apiGet` itself. `result` below is typed from `schema.d.ts` by
 * inference; nothing here casts or asserts a shape onto the response.
 */
describe('apiGet', () => {
  it('resolves the ping response, typed from the generated schema', async () => {
    const result = await apiGet('/api/v1/reference/ping', { name: 'Ada' });

    // No `as`, no `!`: if the contract stopped promising `message`, this
    // would fail to typecheck before it ever ran.
    expect(typeof result.message).toBe('string');
    expect(typeof result.serverTimeUtc).toBe('string');
    expect(typeof result.apiVersion).toBe('string');
  });

  it('resolves the paginated records list, typed from the generated schema', async () => {
    const result = await apiGet('/api/v1/reference/records', { page: 1, pageSize: 20 });

    expect(Array.isArray(result.items)).toBe(true);
    expect(typeof result.totalCount).toBe('number');
  });

  it('threads a required path parameter, type-driven from the schema', async () => {
    const result = await apiGet('/api/v1/admins/{id}', undefined, { pathParams: { id: ADMIN_ID } });

    expect(typeof result.id).toBe('string');
  });

  it('omitting a required path parameter fails typecheck', () => {
    // @ts-expect-error — `/admins/{id}` declares `id` required; the whole
    // options argument becomes required by construction (`OptionsArgs`).
    void apiGet('/api/v1/admins/{id}', undefined);
    // @ts-expect-error — same, but with the options bag present and empty.
    void apiGet('/api/v1/admins/{id}', undefined, {});
  });
});

describe('apiPost', () => {
  it('creates a sample record, request and response both typed from the schema', async () => {
    const result = await apiPost('/api/v1/reference/records', {
      label: 'Term 1 timetable draft',
      note: null,
    });

    expect(typeof result.id).toBe('string');
  });

  it('requires Idempotency-Key on POST /admins and threads it through', async () => {
    const result = await apiPost(
      '/api/v1/admins',
      { staffName: 'Ngozi Adeyemi', email: 'ngozi.adeyemi@example.com', phone: '08012345678' },
      { idempotencyKey: 'a-client-generated-key' },
    );

    expect(typeof result.id).toBe('string');
  });

  it('omitting Idempotency-Key on POST /admins fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on this route (spec
    // 6.1.9/6.1.14); omitting the options argument entirely must not compile.
    void apiPost('/api/v1/admins', {
      staffName: 'Ngozi Adeyemi',
      email: 'ngozi.adeyemi@example.com',
      phone: '08012345678',
    });
  });

  it('accepts Idempotency-Key as optional on POST /admins/{id}/status', async () => {
    const withoutKey = await apiPost(
      '/api/v1/admins/{id}/status',
      { id: ADMIN_ID, status: 'Suspended', reason: null },
      { pathParams: { id: ADMIN_ID } },
    );
    expect(typeof withoutKey.id).toBe('string');

    const withKey = await apiPost(
      '/api/v1/admins/{id}/status',
      { id: ADMIN_ID, status: 'Suspended', reason: null },
      { pathParams: { id: ADMIN_ID }, idempotencyKey: 'a-client-generated-key' },
    );
    expect(typeof withKey.id).toBe('string');
  });
});

describe('apiPatch', () => {
  it('PATCHes a path with a required path parameter and an optional Idempotency-Key', async () => {
    const result = await apiPatch(
      '/api/v1/admins/{id}',
      { id: ADMIN_ID, staffName: 'Ngozi Adeyemi-Bello', email: 'ngozi.adeyemi@example.com', phone: '08012345678', isSuperAdmin: null },
      { pathParams: { id: ADMIN_ID } },
    );

    expect(typeof result.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/admins/{id}` requires `pathParams`.
    void apiPatch('/api/v1/admins/{id}', {
      id: ADMIN_ID,
      staffName: 'Ngozi Adeyemi-Bello',
      email: 'ngozi.adeyemi@example.com',
      phone: '08012345678',
      isSuperAdmin: null,
    });
  });
});

describe('apiDelete', () => {
  it('DELETEs a 204 operation and resolves void, not never', async () => {
    const result = await apiDelete('/api/v1/admins/{id}/sessions', { pathParams: { id: ADMIN_ID } });

    expect(result).toBeUndefined();
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/admins/{id}/sessions` requires `pathParams`.
    void apiDelete('/api/v1/admins/{id}/sessions');
  });
});
