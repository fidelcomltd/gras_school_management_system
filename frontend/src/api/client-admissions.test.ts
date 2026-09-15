import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiPatch, apiPost } from './client';

const PUPIL_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50';
const ARM_ID = '0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f70';

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

/**
 * TASK-0064: `ApproveAdmission` (`POST /api/v1/admissions/{id}/approve`),
 * NEW this move — same generic `apiPost` surface, zero new lines needed in
 * `client.ts`/`client-types.ts`. `Idempotency-Key` is REQUIRED on this route
 * (unlike `/decline`, tested below), so this is this move's own instance of
 * the omitted-required-header proof. `assessmentResultRemarks` and
 * `headOfSchoolName` are sent as explicit `null` — both are in the command's
 * `required` array despite being nullable, so omitting either key fails
 * server-side validation even though the value is optional in product terms
 * (the card's own most-likely-failure note).
 */
describe('apiPost — POST /api/v1/admissions/{id}/approve (ApproveAdmission)', () => {
  const body = {
    id: PUPIL_ID,
    armId: ARM_ID,
    assessmentResultRemarks: null,
    headOfSchoolConfirmed: true,
    headOfSchoolName: null,
  };

  it('threads the required path parameter and Idempotency-Key, returning the approved pupil', async () => {
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/approve'), () =>
        HttpResponse.json({
          id: PUPIL_ID,
          registrationNumber: 'GRAS/2026/0042',
          surname: 'Okafor',
          firstName: 'Chidera',
          middleName: null,
          sex: 'Female',
          dateOfBirth: '2020-05-03',
          ageYears: 6,
          nationality: 'Nigerian',
          stateOfOrigin: 'Anambra',
          lga: 'Awka South',
          homeAddress: '14 Zik Avenue, Awka',
          previousSchool: null,
          previousClass: null,
          status: 'Active',
          otherInformation: null,
          matchedField: null,
          createdAtUtc: '2026-08-03T09:30:00+00:00',
          createdBy: null,
        }),
      ),
    );

    const result = await apiPost('/api/v1/admissions/{id}/approve', body, {
      pathParams: { id: PUPIL_ID },
      idempotencyKey: 'a-client-generated-key',
    });

    expect(result.registrationNumber).toBe('GRAS/2026/0042');
  });

  it('omitting Idempotency-Key fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on `/approve`.
    void apiPost('/api/v1/admissions/{id}/approve', body, { pathParams: { id: PUPIL_ID } });
  });

  it('surfaces a 409 (already decided) verbatim', async () => {
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/approve'), () =>
        problemResponse(409, { detail: 'This admission has already been decided.' }),
      ),
    );

    await expect(
      apiPost('/api/v1/admissions/{id}/approve', body, {
        pathParams: { id: PUPIL_ID },
        idempotencyKey: 'a-client-generated-key',
      }),
    ).rejects.toMatchObject({ message: 'This admission has already been decided.' });
  });
});

/** TASK-0064: `DeclineAdmission` — `Idempotency-Key` optional per the contract, unlike `/approve`. */
describe('apiPost — POST /api/v1/admissions/{id}/decline (DeclineAdmission)', () => {
  const body = { id: PUPIL_ID, reason: 'Family relocated before the intake began.' };

  it('threads the required path parameter with no Idempotency-Key required', async () => {
    const result = await apiPost('/api/v1/admissions/{id}/decline', body, { pathParams: { id: PUPIL_ID } });
    expect(result.id).toBe(PUPIL_ID);
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/admissions/{id}/decline` requires `pathParams`.
    void apiPost('/api/v1/admissions/{id}/decline', body);
  });
});
