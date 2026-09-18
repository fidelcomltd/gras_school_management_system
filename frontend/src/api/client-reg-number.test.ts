import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiGet, apiPatch } from './client';

/**
 * TASK-0052 (reg-number half): the three settings operations TASK-0005c
 * added (`UpdateRegNumber`, `GetRegNumberPreview`, `UpdateAbbreviation`)
 * reached through the same generic `apiGet`/`apiPatch` surface every prior
 * client-*.test.ts file established — zero new lines needed in
 * `client.ts`/`client-types.ts`. Pupils half is in the sibling
 * `client-pupils.test.ts` / `client-pupils-detail.test.ts`, split to stay
 * under CONVENTIONS.md §3's 180-line cap.
 */
describe('apiPatch — PATCH /api/v1/settings/reg-number (UpdateRegNumber)', () => {
  const body = { separator: '/', serialWidth: 4, serialReset: 'PerYear' as const, expectedVersion: 0 };

  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch('/api/v1/settings/reg-number', body);
    expect(typeof withoutKey.separator).toBe('string');

    const withKey = await apiPatch('/api/v1/settings/reg-number', body, {
      idempotencyKey: 'a-client-generated-key',
    });
    expect(typeof withKey.separator).toBe('string');
  });

  it('tolerates a serialReset value the client-side union does not name, without crashing (§8)', async () => {
    // None of this card's three routes has a path parameter, so there is no
    // `{id}` → `:id` MSW conversion to exercise here — contrast
    // `client-pupils-detail.test.ts`'s `GetPupil` override, which does.
    server.use(
      http.patch(apiUrl('/api/v1/settings/reg-number'), () =>
        HttpResponse.json({
          separator: '/',
          serialWidth: 4,
          serialReset: 'SomeFutureSerialReset',
          yearSource: 'AdmissionYear',
          versionNumber: 0,
        }),
      ),
    );

    const result = await apiPatch('/api/v1/settings/reg-number', body);
    expect(result.serialReset).toBe('SomeFutureSerialReset');
  });
});

describe('apiGet — GET /api/v1/settings/reg-number/preview (GetRegNumberPreview)', () => {
  it('requires separator and serialWidth — both are REQUIRED query params, not optional', async () => {
    // The card's own text says these "typecheck as optional"; the committed
    // contract marks both `required: true` (confirmed directly against
    // `contracts/openapi.json`, and reflected in the generated
    // `query: { separator: string; serialWidth: number | string }` — no
    // `?` on either). Flagged in the session report rather than silently
    // followed; this test proves the contract's actual shape.
    const result = await apiGet('/api/v1/settings/reg-number/preview', { separator: '/', serialWidth: 4 });
    expect(typeof result.preview).toBe('string');
  });

  it('omitting a required query param fails typecheck', () => {
    // @ts-expect-error — `serialWidth` is required; this object omits it.
    void apiGet('/api/v1/settings/reg-number/preview', { separator: '/' });
  });

  it('an unknown query key is rejected', () => {
    // @ts-expect-error — `GetRegNumberPreview` declares only separator/serialWidth.
    void apiGet('/api/v1/settings/reg-number/preview', { separator: '/', serialWidth: 4, extra: 'nope' });
  });

  it('passing an idempotencyKey fails typecheck — GetRegNumberPreview declares no Idempotency-Key header', () => {
    // @ts-expect-error — this is the reg-number half's proof of the
    // "idempotencyKey rejected" direction. Neither `UpdateRegNumber` nor
    // `UpdateAbbreviation` can supply the OTHER direction (an omitted
    // REQUIRED Idempotency-Key failing typecheck), since both accept the
    // header only optionally — see `client-pupils.test.ts`'s `CreatePupil`
    // for this move's actual instance of that proof.
    void apiGet('/api/v1/settings/reg-number/preview', { separator: '/', serialWidth: 4 }, { idempotencyKey: 'x' });
  });
});

describe('apiPatch — PATCH /api/v1/settings/abbreviation (UpdateAbbreviation)', () => {
  const body = { abbreviation: 'GRA', confirmationToken: 'CHANGE', reason: 'Reason text', expectedVersion: 0 };

  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch('/api/v1/settings/abbreviation', body);
    expect(typeof withoutKey.abbreviation).toBe('string');

    const withKey = await apiPatch('/api/v1/settings/abbreviation', body, {
      idempotencyKey: 'a-client-generated-key',
    });
    expect(typeof withKey.abbreviation).toBe('string');
  });

  it('issuedCount is nullable and null passes through unchanged — never coerced to 0', async () => {
    const result = await apiPatch('/api/v1/settings/abbreviation', body);
    expect(result.issuedCount).toBeNull();
  });
});
