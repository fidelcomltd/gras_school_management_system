import { describe, expect, it } from 'vitest';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { apiDelete, apiGet, apiPost } from './client';

const ADMIN_ID = '0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f62';
const ROLE_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40';
const SESSION_ID = '0192f0c4-af61-7d4e-c250-6e1b8d9f4073';
const ASSIGNMENT_ID = '0192f0c4-8d4f-7b2c-a03e-4c9f6b7d2e51';
const ARM_ID = '0192f0c4-c072-7e5f-d361-7f2c9e0a5184';

const roleAssignment = {
  id: ASSIGNMENT_ID,
  adminAccountId: ADMIN_ID,
  roleId: ROLE_ID,
  sessionId: SESSION_ID,
  scopeType: 'ArmList',
  armIds: [ARM_ID],
  grantedBy: ADMIN_ID,
  status: 'Active',
  createdAtUtc: '2026-08-03T09:30:00+00:00',
};

/**
 * TASK-0047: the contract's three role-assignment operations
 * (`ListRoleAssignments`, `CreateRoleAssignment`, `RevokeRoleAssignment`)
 * reached through the same generic `apiGet`/`apiPost`/`apiDelete` surface
 * every prior client-*.test.ts file established — zero new lines needed in
 * `client.ts`/`client-types.ts`.
 */
describe('apiGet — GET /api/v1/admins/{id}/assignments (ListRoleAssignments)', () => {
  it('threads the required path parameter and returns a bare array', async () => {
    // ListRoleAssignments's 200 schema is an inline `array` of `$ref
    // RoleAssignmentDto` with no top-level `example`, so the contract-derived
    // default handler would answer with an empty body — same trap TASK-0040
    // hit on `ReorderLevels`. Explicit override, not a touch to
    // `openapi-handlers.ts` or a loosened assertion.
    server.use(
      http.get(apiUrl('/api/v1/admins/:id/assignments'), () => HttpResponse.json([roleAssignment])),
    );

    const result = await apiGet('/api/v1/admins/{id}/assignments', undefined, { pathParams: { id: ADMIN_ID } });

    expect(Array.isArray(result)).toBe(true);
    expect(result[0]?.id).toBe(ASSIGNMENT_ID);
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/admins/{id}/assignments` declares `id` required.
    void apiGet('/api/v1/admins/{id}/assignments', undefined);
  });

  it('passing an idempotencyKey fails typecheck — ListRoleAssignments declares no Idempotency-Key header', () => {
    // @ts-expect-error — this route declares no `Idempotency-Key` header at
    // all (unlike `RevokeRoleAssignment`, which accepts it optionally and so
    // proves neither typing direction).
    void apiGet('/api/v1/admins/{id}/assignments', undefined, { pathParams: { id: ADMIN_ID }, idempotencyKey: 'a-client-generated-key' });
  });

  it('tolerates a scopeType/status value the client-side union does not name, without crashing (§8)', async () => {
    // Route written as `:id`, never the contract's `{id}` — MSW does not
    // treat `{id}` as a parameter matcher.
    server.use(
      http.get(apiUrl('/api/v1/admins/:id/assignments'), () =>
        HttpResponse.json([
          { ...roleAssignment, scopeType: 'SomeFutureScopeType', status: 'SomeFutureAssignmentStatus' },
        ]),
      ),
    );

    const result = await apiGet('/api/v1/admins/{id}/assignments', undefined, { pathParams: { id: ADMIN_ID } });

    expect(result[0]?.scopeType).toBe('SomeFutureScopeType');
    expect(result[0]?.status).toBe('SomeFutureAssignmentStatus');
  });
});

describe('apiPost — POST /api/v1/admins/{id}/assignments (CreateRoleAssignment)', () => {
  const body = {
    adminAccountId: ADMIN_ID,
    roleId: ROLE_ID,
    sessionId: SESSION_ID,
    scopeType: 'ArmList' as const,
    armIds: [ARM_ID],
  };

  it('requires Idempotency-Key and threads it, plus the path parameter, through', async () => {
    server.use(http.post(apiUrl('/api/v1/admins/:id/assignments'), () => HttpResponse.json(roleAssignment, { status: 201 })));

    const result = await apiPost('/api/v1/admins/{id}/assignments', body, {
      pathParams: { id: ADMIN_ID },
      idempotencyKey: 'a-client-generated-key',
    });

    expect(result.id).toBe(ASSIGNMENT_ID);
  });

  it('omitting Idempotency-Key fails typecheck', () => {
    // @ts-expect-error — `Idempotency-Key` is REQUIRED on
    // `POST /admins/{id}/assignments`, same shape as `POST /arms`.
    void apiPost('/api/v1/admins/{id}/assignments', body, { pathParams: { id: ADMIN_ID } });
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/admins/{id}/assignments` requires `pathParams`.
    void apiPost('/api/v1/admins/{id}/assignments', body, { idempotencyKey: 'a-client-generated-key' });
  });
});

describe('apiDelete — DELETE /api/v1/assignments/{id} (RevokeRoleAssignment)', () => {
  it('accepts Idempotency-Key as optional', async () => {
    const withoutKey = await apiDelete('/api/v1/assignments/{id}', { pathParams: { id: ASSIGNMENT_ID } });
    expect(withoutKey).toBeUndefined();

    const withKey = await apiDelete('/api/v1/assignments/{id}', {
      pathParams: { id: ASSIGNMENT_ID },
      idempotencyKey: 'a-client-generated-key',
    });
    expect(withKey).toBeUndefined();
  });

  it('omitting the required path parameter fails typecheck', () => {
    // @ts-expect-error — `/assignments/{id}` requires `pathParams`.
    void apiDelete('/api/v1/assignments/{id}');
  });
});
