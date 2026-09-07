import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiDelete, apiGet, apiPatch, apiPost } from './client';

const ROLE_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41';

/**
 * TASK-0033: the six operations `contracts/openapi.json` gained via TASK-0028
 * (`GetPrivilegeRegister`, `ListRoles`, `CreateRole`, `GetRole`, `UpdateRole`,
 * `DeleteRole`) reached through the SAME generic `apiGet`/`apiPost`/`apiPatch`/
 * `apiDelete` surface as `client.test.ts` — no second calling convention.
 * Regenerating `schema.d.ts` is what makes these paths exist at all; nothing
 * in `client.ts` itself needed to change for them to be reachable. Split into
 * its own file, colocated with `client.ts`, to stay under CONVENTIONS.md §3's
 * 180-line cap rather than growing `client.test.ts` past it.
 */
describe('apiGet — GET /api/v1/privileges (GetPrivilegeRegister)', () => {
  it('resolves the register with no query params, authenticated only (no CSRF/Idempotency-Key involved)', async () => {
    const result = await apiGet('/api/v1/privileges', undefined);

    expect(Array.isArray(result.groups)).toBe(true);
    expect(typeof result.groups[0]?.key).toBe('string');
  });

  it('passing an idempotencyKey fails typecheck — this operation declares no Idempotency-Key header', () => {
    // @ts-expect-error — `IdempotencyKeyOf` resolves to `undefined` for a GET
    // that declares no `Idempotency-Key` header, so `RequestExtras` contributes
    // nothing and the options type here has no `idempotencyKey` property at
    // all. TS's excess-property check on this fresh object literal is what
    // rejects it — the tight-in-both-directions half TASK-0029 established for
    // the required case, proven here for the "not declared at all" case.
    void apiGet('/api/v1/privileges', undefined, { idempotencyKey: 'a-client-generated-key' });
  });
});

describe('apiGet — GET /api/v1/roles (ListRoles)', () => {
  it('lists roles with optional query filters', async () => {
    const result = await apiGet('/api/v1/roles', { status: 'Active', sort: 'name', direction: 'asc' });

    expect(Array.isArray(result.items)).toBe(true);
    expect('nextCursor' in result).toBe(true);
  });

  it('resolves with an empty query object, since every ListRoles filter is independently optional', async () => {
    // Not literal `undefined`: `QueryOf` resolves an operation's optional
    // `query?:` position to the object's own shape (see `client-types.ts`'s
    // `QueryOf` doc comment), so an empty object is the correct "nothing to
    // filter on" call here, not `undefined`.
    const result = await apiGet('/api/v1/roles', {});

    expect(Array.isArray(result.items)).toBe(true);
  });

  it('`sort` is a plain string field, so an unrecognised value is not a typecheck error', async () => {
    // `sort` crosses the wire as `string`, not a closed enum (the "name" |
    // "status" restriction is a server-side validation rule, not a
    // contract-level union) — this line compiling at all is half of §8's proof.
    const result = await apiGet('/api/v1/roles', { sort: 'someFutureSortKey' });

    expect(Array.isArray(result.items)).toBe(true);
  });

  it('tolerates a role `status` value the client-side union does not name, without crashing (§8)', async () => {
    // `RoleStatus` is generated as `"Active" | "Archived"` — a compile-time
    // union only. Nothing in `client.ts`/`client-types.ts` validates or
    // narrows a response body at runtime, so a server that has additively
    // grown a third status must still pass straight through rather than
    // throwing. Overrides the contract-derived default handler for one case.
    server.use(
      http.get(apiUrl('/api/v1/roles'), () =>
        HttpResponse.json({
          items: [
            {
              id: ROLE_ID,
              name: 'Future Role',
              description: null,
              isSystem: false,
              privileges: ['pupil.view'],
              status: 'SomeFutureStatus',
            },
          ],
          nextCursor: null,
        }),
      ),
    );

    const result = await apiGet('/api/v1/roles', {});

    expect(result.items[0]?.status).toBe('SomeFutureStatus');
  });

  it('passing an idempotencyKey fails typecheck — a GET declares no Idempotency-Key header even with real query fields', () => {
    // @ts-expect-error — same excess-property rejection as GetPrivilegeRegister
    // above, proven here on an operation whose options type is NOT trivially
    // empty (it still carries `signal`/`timeout`), so this isn't just "an
    // empty type rejects everything".
    void apiGet('/api/v1/roles', {}, { idempotencyKey: 'a-client-generated-key' });
  });
});

describe('apiPost — POST /api/v1/roles (CreateRole)', () => {
  it('requires Idempotency-Key and threads it through', async () => {
    const result = await apiPost(
      '/api/v1/roles',
      { name: 'Class Teacher', description: 'Enters marks.', privileges: ['pupil.view'] },
      { idempotencyKey: 'a-client-generated-key' },
    );

    expect(typeof result.id).toBe('string');
  });

  it('omitting Idempotency-Key fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on `POST /roles`
    // (spec 6.1.4/6.1.14), same as `POST /admins`; omitting it must not compile.
    void apiPost('/api/v1/roles', {
      name: 'Class Teacher',
      description: 'Enters marks.',
      privileges: ['pupil.view'],
    });
  });
});

describe('apiGet — GET /api/v1/roles/{id} (GetRole)', () => {
  it('threads the required path parameter and needs no CSRF/Idempotency-Key (a read)', async () => {
    const result = await apiGet('/api/v1/roles/{id}', undefined, { pathParams: { id: ROLE_ID } });

    expect(typeof result.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/roles/{id}` declares `id` required.
    void apiGet('/api/v1/roles/{id}', undefined);
  });
});

describe('apiPatch — PATCH /api/v1/roles/{id} (UpdateRole)', () => {
  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiPatch(
      '/api/v1/roles/{id}',
      { id: ROLE_ID, name: null, description: null, privileges: null, status: null },
      { pathParams: { id: ROLE_ID } },
    );
    expect(typeof withoutKey.id).toBe('string');

    const withKey = await apiPatch(
      '/api/v1/roles/{id}',
      { id: ROLE_ID, name: null, description: null, privileges: null, status: null },
      { pathParams: { id: ROLE_ID }, idempotencyKey: 'a-client-generated-key' },
    );
    expect(typeof withKey.id).toBe('string');
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/roles/{id}` requires `pathParams`.
    void apiPatch('/api/v1/roles/{id}', { id: ROLE_ID, name: null, description: null, privileges: null, status: null });
  });
});

describe('apiDelete — DELETE /api/v1/roles/{id} (DeleteRole)', () => {
  it('accepts Idempotency-Key as optional, unlike DELETE /admins/{id}/sessions which declares none', async () => {
    const withoutKey = await apiDelete('/api/v1/roles/{id}', { pathParams: { id: ROLE_ID } });
    expect(withoutKey).toBeUndefined();

    const withKey = await apiDelete('/api/v1/roles/{id}', {
      pathParams: { id: ROLE_ID },
      idempotencyKey: 'a-client-generated-key',
    });
    expect(withKey).toBeUndefined();
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/roles/{id}` requires `pathParams`.
    void apiDelete('/api/v1/roles/{id}');
  });
});
