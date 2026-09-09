import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiGet } from './client';

/**
 * TASK-0054: `ExportAuditEvents` (`GET /api/v1/audit-events/export`) — the
 * contract's first non-JSON response body (`text/csv`, `.paths[…].get
 * .responses."200".content` → `{"text/csv": {"schema": {"type": "string"}}}`).
 *
 * **The proven gap.** Before this card, `SuccessBody<Op>` in
 * `client-types.ts` resolved a `200` response through `JsonOf<C>`, which only
 * matches an `application/json` content key. For this operation `C` is
 * `{ "text/csv": string }`, so `JsonOf<C>` — and therefore `SuccessBody` —
 * resolved to `never`. Confirmed with a throwaway probe file assigning a
 * string literal to `SuccessBody<ExportAuditEventsOp>`:
 * `error TS2322: Type '"..."' is not assignable to type 'never'.` That is a
 * real mistype, not a hypothetical one — `apiGet` would have handed every
 * caller an unreachable type for a payload that exists and is a string at
 * runtime (axios does not JSON-parse a `text/csv` body).
 *
 * The fix (`ResponseBodyOf` in `client-types.ts`) is scoped to exactly that:
 * `SuccessBody`'s `200`/`201` branches now fall through to a response's own
 * single content-type value when it isn't `application/json`, so `text/csv`
 * resolves to `string`. `RequestBodyOf` (request bodies) still calls the
 * original `JsonOf` unchanged — every request body in this contract remains
 * JSON, so widening that side would be an unproven, un-narrow change.
 *
 * **The inline-array/no-example MSW trap, export edition.** The
 * contract-derived default handler
 * (`src/test/msw/openapi-handlers.ts::exampleFor`) only ever reads
 * `response.content["application/json"]`; this response has no such key, so
 * the default handler answers an empty `200` for this route. Every test
 * below that needs a real body supplies its own `server.use(...)` override —
 * `openapi-handlers.ts` itself is untouched.
 */
describe('apiGet — GET /api/v1/audit-events/export (ExportAuditEvents)', () => {
  it('returns the text/csv body as a plain string, not a parsed JSON value', async () => {
    const csv = 'occurredAtUtc,action\n2026-08-03T09:30:00+00:00,settings.grading.update\n';
    server.use(http.get(apiUrl('/api/v1/audit-events/export'), () => HttpResponse.text(csv)));

    const result = await apiGet('/api/v1/audit-events/export', {});

    expect(typeof result).toBe('string');
    expect(result).toBe(csv);
  });

  it('typechecks with all seven shared filters supplied together', async () => {
    // A non-empty body, deliberately: `request.ts`'s `unwrap` treats
    // `response.data === ''` as equivalent to `204 No Content` and returns
    // `undefined` (a pre-existing rule for genuinely empty responses, not
    // this card's gap to fix) — an empty string here would collide with
    // that and assert nothing about this operation's own typing.
    const csv = 'occurredAtUtc,action\n';
    server.use(http.get(apiUrl('/api/v1/audit-events/export'), () => HttpResponse.text(csv)));

    const result = await apiGet('/api/v1/audit-events/export', {
      fromUtc: '2026-08-01T00:00:00+00:00',
      toUtc: '2026-08-31T23:59:59+00:00',
      actorAdminId: '0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f62',
      action: 'settings.grading.update',
      entityType: 'grading_band',
      entityId: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40',
      outcome: 'Success',
    });

    expect(typeof result).toBe('string');
  });

  it('rejects cursor — ExportAuditEvents declares no such parameter, unlike ListAuditEvents', () => {
    // @ts-expect-error — the list/export asymmetry's negative half: the
    // export streams the whole filtered set and declares no `cursor`, while
    // `ListAuditEvents` (see client-audit-events.test.ts) accepts it.
    void apiGet('/api/v1/audit-events/export', { cursor: 'opaque-cursor-token' });
  });

  it('rejects pageSize — ExportAuditEvents declares no such parameter, unlike ListAuditEvents', () => {
    // @ts-expect-error — same asymmetry, the other missing parameter.
    void apiGet('/api/v1/audit-events/export', { pageSize: 20 });
  });

  it('an unknown query key is rejected', () => {
    // @ts-expect-error — `ExportAuditEvents` declares only the seven filters above.
    void apiGet('/api/v1/audit-events/export', { madeUpFilter: 'nope' });
  });
});
