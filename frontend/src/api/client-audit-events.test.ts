import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiGet } from './client';

/**
 * TASK-0054: `ListAuditEvents` (`GET /api/v1/audit-events`), one of the two
 * operations this recurring card's contract move adds. Reached through the
 * same generic `apiGet` surface every prior client-*.test.ts file
 * established — zero new lines needed in `client.ts`. `SuccessBody` in
 * `client-types.ts` DID need a narrow addition, but only for the export's
 * `text/csv` body — see `client-audit-events-export.test.ts`, which carries
 * that proof and the fix's rationale. This operation's own body stays plain
 * `application/json`, unaffected by that change.
 *
 * Neither this operation nor the export declares `X-CSRF-Token`,
 * `Idempotency-Key` or a path parameter (both are reads on a resource with no
 * `{id}` segment) — so this move supplies no new instance of the
 * omitted-required-header or omitted-required-path-parameter directions.
 * Reusing an older operation for those was considered and rejected: every
 * prior card already carries one (e.g. `client-pupils.test.ts`'s
 * `CreatePupil`), and fabricating a case here would misrepresent what this
 * move actually contributes. Stated plainly rather than invented, per the
 * card's own instruction.
 */
describe('apiGet — GET /api/v1/audit-events (ListAuditEvents)', () => {
  it('lists audit events with all nine query parameters optional', async () => {
    const result = await apiGet('/api/v1/audit-events', {});

    expect(Array.isArray(result.items)).toBe(true);
    expect('nextCursor' in result).toBe(true);
  });

  it('typechecks with all nine parameters supplied together, cursor and pageSize included', async () => {
    // The list/export asymmetry's positive half: ListAuditEvents declares
    // `cursor`/`pageSize` (the export does not — see the export test file
    // for the rejection this typechecks against).
    const result = await apiGet('/api/v1/audit-events', {
      cursor: 'opaque-cursor-token',
      pageSize: 20,
      fromUtc: '2026-08-01T00:00:00+00:00',
      toUtc: '2026-08-31T23:59:59+00:00',
      actorAdminId: '0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f62',
      action: 'settings.grading.update',
      entityType: 'grading_band',
      entityId: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40',
      outcome: 'Success',
    });

    expect(Array.isArray(result.items)).toBe(true);
  });

  it('an unknown query key is rejected', () => {
    // @ts-expect-error — `ListAuditEvents` declares only the nine filters above.
    void apiGet('/api/v1/audit-events', { madeUpFilter: 'nope' });
  });

  it('tolerates an outcome value the client-side union does not name, without crashing (§8)', async () => {
    server.use(
      http.get(apiUrl('/api/v1/audit-events'), () =>
        HttpResponse.json({
          items: [
            {
              id: '48213',
              occurredAtUtc: '2026-08-03T09:30:00+00:00',
              actorAdminId: '0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f62',
              actorLabel: 'Chisom Maxwell <chisom.maxwell@example.com>',
              action: 'settings.grading.update',
              entityType: 'grading_band',
              entityId: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40',
              outcome: 'SomeFutureOutcome',
              beforeJson: null,
              afterJson: null,
              reason: null,
              sourceIp: '197.210.64.0/24',
              userAgent: 'Mozilla/5.0',
            },
          ],
          nextCursor: null,
        }),
      ),
    );

    const result = await apiGet('/api/v1/audit-events', {});

    expect(result.items[0]?.outcome).toBe('SomeFutureOutcome');
  });

  it('passes null beforeJson, afterJson and sourceIp through unchanged — no ??, no JSON.parse at this seam', async () => {
    server.use(
      http.get(apiUrl('/api/v1/audit-events'), () =>
        HttpResponse.json({
          items: [
            {
              id: '48214',
              occurredAtUtc: '2026-08-03T09:31:00+00:00',
              actorAdminId: null,
              actorLabel: 'System',
              action: 'session.cron.promote',
              entityType: 'pupil',
              entityId: null,
              outcome: 'Success',
              beforeJson: null,
              afterJson: null,
              reason: null,
              sourceIp: null,
              userAgent: null,
            },
          ],
          nextCursor: null,
        }),
      ),
    );

    const result = await apiGet('/api/v1/audit-events', {});
    const row = result.items[0];

    expect(row?.beforeJson).toBeNull();
    expect(row?.afterJson).toBeNull();
    expect(row?.sourceIp).toBeNull();
    // Not coerced to "" or "{}" anywhere between the wire and this assertion.
    expect(row?.beforeJson).not.toBe('');
    expect(row?.afterJson).not.toBe('{}');
  });
});
