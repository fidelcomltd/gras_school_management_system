import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { AdminsListScreen } from './admins-list-screen';

const TEMP_PASSWORD = 'not-a-real-password-fixture';

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

function renderScreen() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AdminsListScreen />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { queryClient, ...utils };
}

describe('AdminsListScreen — four required states', () => {
  it('loading: shows a status region before /admins resolves', () => {
    mockMe('admin.view');
    server.use(http.get(apiUrl('/api/v1/admins'), () => new Promise(() => undefined)));

    renderScreen();

    expect(screen.getByText('Loading admin accounts…')).toBeInTheDocument();
  });

  it('empty: renders a message rather than a blank list', async () => {
    mockMe('admin.view');
    server.use(http.get(apiUrl('/api/v1/admins'), () => HttpResponse.json({ items: [], nextCursor: null })));

    renderScreen();

    expect(await screen.findByText('No admin accounts found.')).toBeInTheDocument();
  });

  it('unauthorized: renders nothing — ProtectedLayout is already navigating away', async () => {
    mockMe('admin.view');
    server.use(
      http.get(apiUrl('/api/v1/admins'), () => problemResponse(401, { errorCode: 'authentication.required' })),
    );

    const { container } = renderScreen();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('error: a server failure shows a retry, not a crash', async () => {
    mockMe('admin.view');
    server.use(http.get(apiUrl('/api/v1/admins'), () => problemResponse(500)));

    renderScreen();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});

describe('AdminsListScreen — create admin reveals the temporary password exactly once', () => {
  it('shows the password once, then it is gone from the DOM and every cache once the dialog closes', async () => {
    mockMe('admin.view', 'admin.create');
    let created: Record<string, unknown> | null = null;
    server.use(
      http.get(apiUrl('/api/v1/admins'), () =>
        HttpResponse.json({ items: created ? [created] : [], nextCursor: null }),
      ),
    );
    server.use(
      http.post(apiUrl('/api/v1/admins'), async ({ request }) => {
        const body = (await request.json()) as { staffName: string; email: string; phone: string };
        created = {
          id: 'admin-new',
          staffName: body.staffName,
          email: body.email,
          phone: body.phone,
          status: 'Active',
          isSuperAdmin: false,
          mustChangePassword: true,
          lastLoginAtUtc: null,
          createdAtUtc: '2026-09-08T00:00:00+00:00',
        };
        return HttpResponse.json(
          { ...created, temporaryPassword: TEMP_PASSWORD },
          { status: 201 },
        );
      }),
    );

    const user = userEvent.setup();
    const { queryClient } = renderScreen();

    await user.click(await screen.findByRole('button', { name: 'New admin' }));
    await user.type(screen.getByLabelText('Staff name'), 'Ngozi Adeyemi');
    await user.type(screen.getByLabelText('Email'), 'ngozi.adeyemi@example.com');
    await user.type(screen.getByLabelText('Phone'), '08012345678');
    await user.click(screen.getByRole('button', { name: 'Create admin' }));

    // Revealed exactly once, with the "never again" affordance (AC).
    expect(await screen.findByText(TEMP_PASSWORD)).toBeInTheDocument();
    expect(screen.getByText(/will not be shown again/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Done' }));

    // Gone from the DOM, and the list now reflects the new account without it.
    expect(screen.queryByText(TEMP_PASSWORD)).not.toBeInTheDocument();
    expect(await screen.findByText('Ngozi Adeyemi')).toBeInTheDocument();

    // Gone from every TanStack Query cache too — never a query cache a later
    // render could re-display it from, and `gcTime: 0` plus the dialog's own
    // `reset()` evicts the mutation's cached result once it has no observer.
    const queryData = queryClient.getQueryCache().getAll().map((query) => query.state.data);
    expect(JSON.stringify(queryData)).not.toContain(TEMP_PASSWORD);
    await waitFor(() => {
      const mutationData = queryClient.getMutationCache().getAll().map((mutation) => mutation.state.data);
      expect(JSON.stringify(mutationData)).not.toContain(TEMP_PASSWORD);
    });
  });
});
