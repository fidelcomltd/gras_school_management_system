import { describe, expect, it } from 'vitest';
import { apiPatch } from './client';

const PUPIL_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50';

/**
 * TASK-0062: `UpdateAdmissionRecord` (`PATCH /api/v1/admissions/{id}`), NEW
 * this move. `id` is the pupil id — the same one `POST /pupils`
 * (`client-pupils.test.ts`) returns and `GET /pupils/{id}`
 * (`client-pupils-detail.test.ts`) reads by. Reached through the same
 * generic `apiPatch` surface every prior client-*.test.ts file established —
 * zero new lines needed in `client.ts`/`client-types.ts`. This is the card's
 * own instance of the omitted-required-path-parameter direction.
 */
describe('apiPatch — PATCH /api/v1/admissions/{id} (UpdateAdmissionRecord)', () => {
  const body = {
    id: PUPIL_ID,
    sessionId: null,
    dateApplicationReceived: null,
    dateAdmitted: null,
    classAdmittedInto: null,
    admissionType: null,
    admissionTypeNote: null,
    assessmentRequired: null,
    assessmentResultRemarks: null,
    assignedClassTeacher: null,
    declarationName: 'Chinwe Okafor',
    declarationSigned: true,
    declarationDate: '2026-09-08',
    headOfSchoolConfirmed: null,
    headOfSchoolName: null,
  };

  it('threads the required path parameter and accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch('/api/v1/admissions/{id}', body, { pathParams: { id: PUPIL_ID } });
    expect(withoutKey.declarationSigned).toBe(true);

    const withKey = await apiPatch('/api/v1/admissions/{id}', body, {
      pathParams: { id: PUPIL_ID },
      idempotencyKey: 'a-client-generated-key',
    });
    expect(withKey.declarationSigned).toBe(true);
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/admissions/{id}` requires `pathParams`.
    void apiPatch('/api/v1/admissions/{id}', body);
  });
});
