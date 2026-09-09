import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { RolesListScreen } from './roles-list-screen';

function mockMe(...privileges: string[]) {
  server.use(
    http.get(apiUrl('/api/v1/auth/me'), () =>
      HttpResponse.json({
        accountId: 'acc-1',
        email: 'admin@example.com',
        staffName: 'Chisom Maxwell',
        isSuperAdmin: false,
        mustChangePassword: false,
        effectivePrivileges: privileges.map((privilege) => ({ privilege, scope: 'SchoolWide', armIds: [] })),
        sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
      }),
    ),
  );
}

const GROUPS = [
  {
    key: 'pupils_and_subjects',
    title: 'Pupils and subjects',
    privileges: [{ code: 'pupil.view', permits: 'View pupil records.', scopable: true }],
  },
  {
    key: 'results',
    title: 'Results',
    privileges: [{ code: 'result.score.enter', permits: 'Enter marks.', scopable: true }],
  },
];

function mockPrivileges() {
  server.use(http.get(apiUrl('/api/v1/privileges'), () => HttpResponse.json({ groups: GROUPS })));
}

function role(overrides: Record<string, unknown> = {}) {
  return {
    id: 'role-1',
    name: 'Class Teacher',
    description: 'Enters marks.',
    isSystem: false,
    privileges: ['pupil.view'],
    status: 'Active',
    ...overrides,
  };
}

function renderScreen() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <RolesListScreen />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('RolesListScreen — four required states', () => {
  it('loading: shows a status region before /roles resolves', () => {
    mockMe('role.view');
    mockPrivileges();
    server.use(http.get(apiUrl('/api/v1/roles'), () => new Promise(() => undefined)));

    renderScreen();

    expect(screen.getByText('Loading roles…')).toBeInTheDocument();
  });

  it('empty: renders a message rather than a blank list', async () => {
    mockMe('role.view');
    mockPrivileges();
    server.use(http.get(apiUrl('/api/v1/roles'), () => HttpResponse.json({ items: [], nextCursor: null })));

    renderScreen();

    expect(await screen.findByText('No roles yet.')).toBeInTheDocument();
  });

  it('unauthorized: renders nothing — ProtectedLayout is already navigating away', async () => {
    mockMe('role.view');
    mockPrivileges();
    server.use(
      http.get(apiUrl('/api/v1/roles'), () => problemResponse(401, { errorCode: 'authentication.required' })),
    );

    const { container } = renderScreen();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('error: a server failure shows a retry, not a crash', async () => {
    mockMe('role.view');
    mockPrivileges();
    server.use(http.get(apiUrl('/api/v1/roles'), () => problemResponse(500)));

    renderScreen();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});

describe('RolesListScreen — create round-trips a privilege selection', () => {
  it('checking a privilege in the picker posts it, grouped by the register module', async () => {
    mockMe('role.view', 'role.create');
    mockPrivileges();
    let roles: Record<string, unknown>[] = [];
    server.use(http.get(apiUrl('/api/v1/roles'), () => HttpResponse.json({ items: roles, nextCursor: null })));
    let postedBody: { name: string; description: string | null; privileges: string[] } | undefined;
    server.use(
      http.post(apiUrl('/api/v1/roles'), async ({ request }) => {
        const body = (await request.json()) as { name: string; description: string | null; privileges: string[] };
        postedBody = body;
        const created = {
          id: 'role-new',
          name: body.name,
          description: body.description,
          isSystem: false,
          privileges: body.privileges,
          status: 'Active',
        };
        roles = [...roles, created];
        return HttpResponse.json(created, { status: 201 });
      }),
    );

    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'New role' }));
    await user.type(screen.getByLabelText('Role name'), 'Class Teacher');
    await user.type(screen.getByLabelText('Description'), 'Enters marks.');

    // Grouped by module (AC): both group titles are visible.
    expect(await screen.findByText('Pupils and subjects')).toBeInTheDocument();
    expect(screen.getByText('Results')).toBeInTheDocument();

    await user.click(screen.getByRole('checkbox', { name: /pupil\.view/ }));
    await user.click(screen.getByRole('button', { name: 'Create role' }));

    await waitFor(() => expect(postedBody?.privileges).toEqual(['pupil.view']));
    expect(await screen.findByText('Class Teacher')).toBeInTheDocument();
  });
});

describe('RolesListScreen — edit tolerates an unrecognised privilege code', () => {
  it('renders an unknown code without crashing, and unchecking it removes it from the PATCH body', async () => {
    mockMe('role.view', 'role.update');
    mockPrivileges();
    const existing = role({ privileges: ['pupil.view', 'legacy.unknown.code'] });
    server.use(http.get(apiUrl('/api/v1/roles'), () => HttpResponse.json({ items: [existing], nextCursor: null })));
    server.use(http.get(apiUrl('/api/v1/roles/:id'), () => HttpResponse.json(existing)));
    let patchedBody: { privileges: string[] } | undefined;
    server.use(
      http.patch(apiUrl('/api/v1/roles/:id'), async ({ request }) => {
        const body = (await request.json()) as { privileges: string[] };
        patchedBody = body;
        return HttpResponse.json({ ...existing, privileges: body.privileges });
      }),
    );

    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Edit' }));
    expect(await screen.findByText('Other (not in the current register)')).toBeInTheDocument();
    expect(screen.getByText('legacy.unknown.code')).toBeInTheDocument();

    await user.click(screen.getByRole('checkbox', { name: 'legacy.unknown.code' }));
    await user.click(screen.getByRole('checkbox', { name: /result\.score\.enter/ }));
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() =>
      expect(patchedBody?.privileges?.sort()).toEqual(['pupil.view', 'result.score.enter'].sort()),
    );
  });
});

describe('RolesListScreen — deleting a system role surfaces the 409 verbatim', () => {
  it('shows the server-supplied message, not a generic conflict message', async () => {
    mockMe('role.view', 'role.delete');
    mockPrivileges();
    const systemRole = role({ id: 'role-super', name: 'Super Admin', isSystem: true, privileges: ['pupil.view'] });
    server.use(http.get(apiUrl('/api/v1/roles'), () => HttpResponse.json({ items: [systemRole], nextCursor: null })));
    server.use(
      http.delete(apiUrl('/api/v1/roles/:id'), () =>
        problemResponse(409, { detail: 'Super Admin is a system role and cannot be deleted.' }),
      ),
    );
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Delete' }));

    expect(await screen.findByText('Super Admin is a system role and cannot be deleted.')).toBeInTheDocument();
  });
});
