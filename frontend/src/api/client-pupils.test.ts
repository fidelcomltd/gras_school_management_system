import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiGet, apiPost } from './client';

const PUPIL_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50';

/**
 * TASK-0052 (pupils half, no-path-parameter operations): `ListPupils`,
 * `CreatePupil`, `FindPupilDuplicates`, `ListAdmissionsQueue`, reached
 * through the same generic `apiGet`/`apiPost` surface every prior
 * client-*.test.ts file established — zero new lines needed in
 * `client.ts`/`client-types.ts`. `GetPupil`/`UpdatePupilBiographical` (the
 * two operations that DO have a path parameter, and this move's own instance
 * of the omitted-required-path-parameter proof) are in the sibling
 * `client-pupils-detail.test.ts`, split to stay under CONVENTIONS.md §3's
 * 180-line cap.
 */
describe('apiGet — GET /api/v1/pupils (ListPupils)', () => {
  it('lists pupils with status optional, registrationNumber null passing through unchanged', async () => {
    const result = await apiGet('/api/v1/pupils', {});

    expect(Array.isArray(result.items)).toBe(true);
    // Every pupil this backend can currently create is `pending`, so the
    // contract's own example already carries `registrationNumber: null` —
    // asserted here directly, never coerced to `""`.
    expect(result.items[0]?.registrationNumber).toBeNull();
  });

  it('accepts the status filter explicitly', async () => {
    const result = await apiGet('/api/v1/pupils', { status: 'Pending' });
    expect(Array.isArray(result.items)).toBe(true);
  });

  it('an unknown query key is rejected', () => {
    // @ts-expect-error — `ListPupils` declares only cursor/pageSize/status/search.
    void apiGet('/api/v1/pupils', { madeUpFilter: 'nope' });
  });
});

describe('apiPost — POST /api/v1/pupils (CreatePupil)', () => {
  const body = {
    surname: 'Okafor',
    firstName: 'Chidera',
    middleName: 'Ngozi',
    sex: 'Female' as const,
    dateOfBirth: '2020-05-03',
    nationality: 'Nigerian',
    stateOfOrigin: 'Anambra',
    lga: 'Awka South',
    homeAddress: '14 Zik Avenue, Awka',
    previousSchool: null,
    previousClass: null,
    otherInformation: null,
    // TASK-0062: `admission` (section A) is now required on `CreatePupilCommand`.
    admission: {
      sessionId: null,
      dateApplicationReceived: '2026-08-01',
      dateAdmitted: null,
      classAdmittedInto: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d30',
      admissionType: 'New' as const,
      admissionTypeNote: null,
      assessmentRequired: false,
    },
  };

  it('requires Idempotency-Key and threads it through — this move\'s own instance of the omitted-required-header proof', async () => {
    // `Idempotency-Key` is optional on both of this move's reg-number
    // PATCHes (see `client-reg-number.test.ts`), so THIS is where the
    // omitted-required-header direction actually lives in TASK-0052 — no
    // need to reuse `CreateLevel`/`POST /arms`, this move supplies its own.
    const result = await apiPost('/api/v1/pupils', body, { idempotencyKey: 'a-client-generated-key' });

    expect(typeof result.id).toBe('string');
    expect(result.registrationNumber).toBeNull();
  });

  it('omitting Idempotency-Key fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on `POST /pupils`
    // (spec 6.5.4), same shape as `POST /arms`/`POST /admins/{id}/assignments`.
    void apiPost('/api/v1/pupils', body);
  });
});

describe('apiGet — GET /api/v1/pupils/duplicates (FindPupilDuplicates)', () => {
  // The 200 response schema is an inline `array` of `$ref PupilDto` with no
  // top-level `example` — the same inline-array/no-example trap
  // TASK-0040/0044/0047 hit on `ReorderLevels`/`ListRoleAssignments`. Fixed
  // with an explicit override here, `openapi-handlers.ts` untouched.
  const candidates = [
    { id: PUPIL_ID, registrationNumber: null, surname: 'Okafor', firstName: 'Chidera', middleName: null, sex: 'Female', dateOfBirth: '2020-05-03', ageYears: 6, nationality: 'Nigerian', stateOfOrigin: 'Anambra', lga: 'Awka South', homeAddress: '14 Zik Avenue, Awka', previousSchool: null, previousClass: null, status: 'Pending', otherInformation: null, matchedField: 'Surname', createdAtUtc: '2026-08-03T09:30:00+00:00', createdBy: null },
  ];

  it('takes exactly the three query params the contract declares — surname, firstName, dateOfBirth (not four, and not "dob")', async () => {
    // The card's text names a fourth param, `contactPhone`, and calls the
    // date one `dob`. The committed contract declares only three, all
    // required: `surname`, `firstName`, `dateOfBirth`. `contactPhone`
    // matching is explicitly out of scope of this contract move per the
    // operation's own description ("the NEXT card's, once pupil_contact
    // exists"). Flagged in the session report.
    server.use(http.get(apiUrl('/api/v1/pupils/duplicates'), () => HttpResponse.json(candidates)));

    const result = await apiGet('/api/v1/pupils/duplicates', {
      surname: 'Okafor',
      firstName: 'Chidera',
      dateOfBirth: '2020-05-03',
    });

    expect(Array.isArray(result)).toBe(true);
    expect(result[0]?.registrationNumber).toBeNull();
  });

  it('an unknown query key is rejected — proves contactPhone is not (yet) part of this operation', () => {
    // @ts-expect-error — `contactPhone` is not a declared query param on
    // `FindPupilDuplicates`; only surname/firstName/dateOfBirth are.
    void apiGet('/api/v1/pupils/duplicates', { surname: 'Okafor', firstName: 'Chidera', dateOfBirth: '2020-05-03', contactPhone: '0800' });
  });

  it('omitting a required query param fails typecheck', () => {
    // @ts-expect-error — `dateOfBirth` is required; this object omits it.
    void apiGet('/api/v1/pupils/duplicates', { surname: 'Okafor', firstName: 'Chidera' });
  });
});

describe('apiGet — GET /api/v1/admissions (ListAdmissionsQueue)', () => {
  it('lists the queue with cursor/pageSize both optional', async () => {
    const result = await apiGet('/api/v1/admissions', {});
    expect(Array.isArray(result.items)).toBe(true);
    expect('nextCursor' in result).toBe(true);
  });

  it('an unknown query key is rejected', () => {
    // @ts-expect-error — `ListAdmissionsQueue` declares only cursor/pageSize.
    void apiGet('/api/v1/admissions', { status: 'Pending' });
  });
});
