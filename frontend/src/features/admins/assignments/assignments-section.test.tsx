import { describe, expect, it, vi } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { AssignmentsSection } from './assignments-section';

const assignment = {
  id: 'as-1', adminAccountId: 'admin-1', roleId: 'role-ct', sessionId: 's-1', scopeType: 'ArmList', armIds: ['arm-1'],
  grantedBy: 'acc-1', status: 'Active', createdAtUtc: '2026-09-20T09:00:00Z',
};

function mockReads(assignments: unknown[]) {
  server.use(
    http.get(apiUrl('/api/v1/admins/:id/assignments'), () => HttpResponse.json(assignments)),
    http.get(apiUrl('/api/v1/roles'), ({ request }) =>
      new URL(request.url).searchParams.get('status') === 'Archived'
        ? HttpResponse.json({ items: [], nextCursor: null })
        : HttpResponse.json({
            items: [
              { id: 'role-sa', name: 'Super Admin', description: null, isSystem: true, status: 'Active', privileges: [] },
              { id: 'role-ct', name: 'Class Teacher', description: null, isSystem: false, status: 'Active', privileges: [] },
            ],
            nextCursor: null,
          }),
    ),
    http.get(apiUrl('/api/v1/sessions'), () =>
      HttpResponse.json({ items: [{ id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active' }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/arms'), () =>
      HttpResponse.json({ items: [{ id: 'arm-1', displayName: 'Primary 4A', label: 'A', classLevelId: 'p4', classLevel: 'Primary 4', sessionId: 's-1', capacity: null, formTeacherAdminId: null, status: 'Active' }], nextCursor: null }),
    ),
  );
}

describe('AssignmentsSection', () => {
  it('lists the active roles by name with their classes, and revokes one after confirming', async () => {
    mockMe('admin.view', 'role.assign', 'role.view');
    mockReads([assignment, { ...assignment, id: 'as-0', status: 'Revoked' }]);
    let revoked = '';
    server.use(
      http.delete(apiUrl('/api/v1/assignments/:id'), ({ params }) => {
        revoked = String(params['id']);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const { user } = renderWithProviders(<AssignmentsSection adminId="admin-1" staffName="Ngozi Adeyemi" isSelf={false} canAssign canScopeAssign />);

    expect(await screen.findByText('Class Teacher')).toBeInTheDocument();
    expect(await screen.findByText(/2026\/2027 · Primary 4A/)).toBeInTheDocument();
    expect(screen.getByText(/1 revoked assignment is kept/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Revoke Class Teacher' }));
    await waitFor(() => expect(revoked).toBe('as-1'));
  });

  it('assigns a role over chosen classes, never offering Super Admin', async () => {
    mockMe('admin.view', 'role.scope.assign', 'role.view');
    mockReads([]);
    let sent: Record<string, unknown> | null = null;
    server.use(
      http.post(apiUrl('/api/v1/admins/:id/assignments'), async ({ request }) => {
        sent = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ ...assignment, id: 'as-2' }, { status: 201 });
      }),
    );

    const { user } = renderWithProviders(
      <AssignmentsSection adminId="admin-1" staffName="Ngozi Adeyemi" isSelf={false} canAssign={false} canScopeAssign />,
    );

    expect(await screen.findByText(/No roles yet/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Assign role' }));
    await user.click(await screen.findByRole('combobox', { name: 'Role' }));
    expect(screen.queryByRole('option', { name: 'Super Admin' })).not.toBeInTheDocument();
    await user.click(await screen.findByRole('option', { name: 'Class Teacher' }));
    // Only the arm-scoped choice is offered to a role.scope.assign holder, so it is already selected.
    expect(screen.queryByLabelText('The whole school')).not.toBeInTheDocument();
    await user.click(await screen.findByLabelText('Primary 4A'));
    await user.click(screen.getByRole('button', { name: 'Assign role' }));

    await waitFor(() =>
      expect(sent).toEqual({ adminAccountId: 'admin-1', roleId: 'role-ct', sessionId: 's-1', scopeType: 'ArmList', armIds: ['arm-1'] }),
    );
  }, 15_000);

  it('offers no changes on your own account', async () => {
    mockMe('admin.view', 'role.assign', 'role.view');
    mockReads([assignment]);

    renderWithProviders(<AssignmentsSection adminId="acc-1" staffName="Chisom Maxwell" isSelf canAssign canScopeAssign />);

    expect(await screen.findByText('Class Teacher')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Assign role' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Revoke/ })).not.toBeInTheDocument();
  });
});
