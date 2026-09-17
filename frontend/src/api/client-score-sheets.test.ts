import { describe, expect, it } from 'vitest';
import { setCsrfToken } from '@/lib/auth/auth-session';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiPatch, apiPut } from './client';

const ARM_ID = '0192f0c4-c072-7e5f-d361-7f2c9e0a5184';
const SUBJECT_ID = '0192f0c4-e294-7061-f583-9141c02d7306';
const TERM_ID = '0192f0c4-15c7-7364-2816-c474f3608639';
const ADMIN_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40';

const saveScoreSheetBody = {
  armId: ARM_ID,
  subjectId: SUBJECT_ID,
  termId: TERM_ID,
  version: null,
  rows: [],
};

/**
 * TASK-0079: the score-sheet `PUT` (`SaveScoreSheet`) is the contract's
 * first `PUT` operation, so it needed `apiPut` added to `client.ts` before
 * it was reachable at all (drift entry 2026-09-16 — `UpdateAssessment`,
 * `UpdateGrading` and `ResetGrading` are still typed-but-uncallable; this
 * card does not consume them, only proves the verb helper itself).
 */
describe('apiPut — PUT /api/v1/arms/{armId}/score-sheets (SaveScoreSheet)', () => {
  it('PUTs a path with a required path parameter and an optional Idempotency-Key', async () => {
    const result = await apiPut('/api/v1/arms/{armId}/score-sheets', saveScoreSheetBody, {
      pathParams: { armId: ARM_ID },
    });

    expect(typeof result.armId).toBe('string');
  });

  it('accepts Idempotency-Key as optional', async () => {
    const withKey = await apiPut('/api/v1/arms/{armId}/score-sheets', saveScoreSheetBody, {
      pathParams: { armId: ARM_ID },
      idempotencyKey: 'a-client-generated-key',
    });

    expect(typeof withKey.armId).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/arms/{armId}/score-sheets` requires `pathParams`.
    void apiPut('/api/v1/arms/{armId}/score-sheets', saveScoreSheetBody);
  });

  /**
   * The behavioural proof the card asks for: not a snapshot of `apiPut`
   * against itself, but a live comparison against `apiPatch` hitting a real
   * route of its own (`PATCH /admins/{id}`) with the SAME Idempotency-Key.
   * `apiPut` is a copy-paste of `apiPatch` with the verb swapped, so if
   * either the CSRF header, the Idempotency-Key threading, or the JSON
   * content type diverged between the two — e.g. a forgotten
   * `splitOptions`/`buildPath` call, or `idempotencyKey` silently dropped —
   * this fails; a hand test only asserting `apiPut`'s own output could not
   * catch that, since it has nothing independent to disagree with.
   */
  it('sends what apiPatch sends — method aside', async () => {
    const seenPatch: { method?: string; csrf?: string | null; idempotencyKey?: string | null; contentType?: string | null } = {};
    const seenPut: { method?: string; csrf?: string | null; idempotencyKey?: string | null; contentType?: string | null } = {};

    server.use(
      http.patch(apiUrl('/api/v1/admins/:id'), ({ request }) => {
        seenPatch.method = request.method;
        seenPatch.csrf = request.headers.get('X-CSRF-Token');
        seenPatch.idempotencyKey = request.headers.get('Idempotency-Key');
        seenPatch.contentType = request.headers.get('Content-Type');
        return HttpResponse.json({
          id: ADMIN_ID,
          staffName: 'Ngozi Adeyemi-Bello',
          email: 'ngozi.adeyemi@example.com',
          phone: '08012345678',
          isSuperAdmin: false,
        });
      }),
      http.put(apiUrl('/api/v1/arms/:armId/score-sheets'), ({ request }) => {
        seenPut.method = request.method;
        seenPut.csrf = request.headers.get('X-CSRF-Token');
        seenPut.idempotencyKey = request.headers.get('Idempotency-Key');
        seenPut.contentType = request.headers.get('Content-Type');
        return HttpResponse.json({
          armId: ARM_ID,
          subjectId: SUBJECT_ID,
          termId: TERM_ID,
          version: '5f3759df',
          resultSet: null,
          components: [],
          examination: { id: 'exam', label: 'Exam', maxMark: 60 },
          rows: [],
        });
      }),
    );

    setCsrfToken('csrf-shared-token');

    await apiPatch(
      '/api/v1/admins/{id}',
      { id: ADMIN_ID, staffName: 'Ngozi Adeyemi-Bello', email: 'ngozi.adeyemi@example.com', phone: '08012345678', isSuperAdmin: null },
      { pathParams: { id: ADMIN_ID }, idempotencyKey: 'shared-idempotency-key' },
    );
    await apiPut('/api/v1/arms/{armId}/score-sheets', saveScoreSheetBody, {
      pathParams: { armId: ARM_ID },
      idempotencyKey: 'shared-idempotency-key',
    });

    expect(seenPatch.method).toBe('PATCH');
    expect(seenPut.method).toBe('PUT');

    // Everything but the verb itself must line up: same CSRF attachment,
    // same Idempotency-Key threading, same JSON content type — all owned by
    // the shared transport (`@/lib/http`), not by the verb helper.
    expect(seenPut.csrf).toBe(seenPatch.csrf);
    expect(seenPut.csrf).toBe('csrf-shared-token');
    expect(seenPut.idempotencyKey).toBe(seenPatch.idempotencyKey);
    expect(seenPut.idempotencyKey).toBe('shared-idempotency-key');
    expect(seenPut.contentType).toBe(seenPatch.contentType);
  });
});
