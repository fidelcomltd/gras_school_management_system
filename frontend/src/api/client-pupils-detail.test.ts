import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiGet, apiPatch } from './client';

const PUPIL_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50';

/**
 * TASK-0052 (pupils half, by-id operations): `GetPupil` and
 * `UpdatePupilBiographical`, split from `client-pupils.test.ts` to stay
 * under CONVENTIONS.md §3's 180-line cap. This is the card's own instance of
 * the omitted-required-path-parameter direction (the reg-number half has no
 * path parameter anywhere and structurally cannot supply it), and also
 * carries the §8 enum-tolerance proof on a route that DOES have a path
 * parameter, so the `{id}` → `:id` MSW conversion (`toMswRoute` in
 * `openapi-handlers.ts`) is actually exercised — TASK-0037 lost a run to
 * writing an override route as `{id}` verbatim.
 */
describe('apiGet — GET /api/v1/pupils/{id} (GetPupil)', () => {
  it('threads the required path parameter, registrationNumber null passing through unchanged', async () => {
    const result = await apiGet('/api/v1/pupils/{id}', undefined, { pathParams: { id: PUPIL_ID } });

    expect(typeof result.id).toBe('string');
    expect(result.registrationNumber).toBeNull();
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/pupils/{id}` declares `id` required.
    void apiGet('/api/v1/pupils/{id}', undefined);
  });

  it('tolerates a sex/status value the client-side unions do not name, without crashing (§8)', async () => {
    // Route written as `:id`, never the contract's `{id}` — MSW does not
    // treat `{id}` as a parameter matcher.
    server.use(
      http.get(apiUrl('/api/v1/pupils/:id'), () =>
        HttpResponse.json({
          id: PUPIL_ID,
          registrationNumber: null,
          surname: 'Okafor',
          firstName: 'Chidera',
          middleName: null,
          sex: 'SomeFuturePupilSex',
          dateOfBirth: '2020-05-03',
          ageYears: 6,
          nationality: 'Nigerian',
          stateOfOrigin: 'Anambra',
          lga: 'Awka South',
          homeAddress: '14 Zik Avenue, Awka',
          previousSchool: null,
          previousClass: null,
          status: 'SomeFuturePupilStatus',
          otherInformation: null,
          matchedField: null,
          createdAtUtc: '2026-08-03T09:30:00+00:00',
          createdBy: null,
        }),
      ),
    );

    const result = await apiGet('/api/v1/pupils/{id}', undefined, { pathParams: { id: PUPIL_ID } });

    expect(result.sex).toBe('SomeFuturePupilSex');
    expect(result.status).toBe('SomeFuturePupilStatus');
  });
});

describe('apiPatch — PATCH /api/v1/pupils/{id} (UpdatePupilBiographical)', () => {
  const body = {
    id: PUPIL_ID,
    surname: null,
    firstName: null,
    middleName: null,
    sex: null,
    dateOfBirth: null,
    nationality: null,
    stateOfOrigin: null,
    lga: null,
    homeAddress: '22 Zik Avenue, Awka',
    previousSchool: null,
    previousClass: null,
    otherInformation: null,
    registrationNumber: null,
  };

  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch('/api/v1/pupils/{id}', body, { pathParams: { id: PUPIL_ID } });
    expect(typeof withoutKey.id).toBe('string');

    const withKey = await apiPatch('/api/v1/pupils/{id}', body, {
      pathParams: { id: PUPIL_ID },
      idempotencyKey: 'a-client-generated-key',
    });
    expect(typeof withKey.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/pupils/{id}` requires `pathParams`.
    void apiPatch('/api/v1/pupils/{id}', body);
  });
});
