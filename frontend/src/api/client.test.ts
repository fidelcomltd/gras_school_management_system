import { describe, expect, it } from 'vitest';
import { apiGet, apiPost } from './client';

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
});

describe('apiPost', () => {
  it('creates a sample record, request and response both typed from the schema', async () => {
    const result = await apiPost('/api/v1/reference/records', {
      label: 'Term 1 timetable draft',
      note: null,
    });

    expect(typeof result.id).toBe('string');
  });
});
