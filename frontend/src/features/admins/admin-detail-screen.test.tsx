import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { AdminDetailScreen } from './admin-detail-screen';
import { AdminsKeys } from './types';

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

function account(overrides: Record<string, unknown> = {}) {
  return {
    id: 'admin-1',
    staffName: 'Ngozi Adeyemi',
    email: 'ngozi.adeyemi@example.com',
    phone: '+2348012345678',
    status: 'Active',
    isSuperAdmin: false,
    mustChangePassword: false,
    lastLoginAtUtc: null,
    createdAtUtc: '2026-08-03T09:30:00+00:00',
    ...overrides,
  };
}

function renderScreen(queryClient: QueryClient) {
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/admins/admin-1']}>
        <Routes>
          <Route path="/admins/:id" element={<AdminDetailScreen />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('AdminDetailScreen — status change patches caches instead of a full refetch', () => {
  it('suspend then reactivate: the LIST cache (never fetched here) reflects each status from the mutation response alone', async () => {
    mockMe('admin.view', 'admin.suspend');
    let current = account();
    server.use(http.get(apiUrl('/api/v1/admins/:id'), () => HttpResponse.json(current)));
    server.use(
      http.post(apiUrl('/api/v1/admins/:id/status'), async ({ request }) => {
        const body = (await request.json()) as { status: string };
        current = { ...current, status: body.status };
        return HttpResponse.json(current);
      }),
    );

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    // Seeds the list cache exactly as `AdminsListScreen` would have left it
    // from an earlier visit this session — this test never renders that
    // screen or calls `GET /admins`, so any change appearing here can only
    // have come from `useChangeAdminStatus`'s own `setQueriesData` patch.
    queryClient.setQueryData([AdminsKeys.List, undefined, undefined], {
      pageParams: [undefined],
      pages: [{ items: [account()], nextCursor: null }],
    });

    const user = userEvent.setup();
    renderScreen(queryClient);

    await screen.findByRole('heading', { name: 'Ngozi Adeyemi' });

    async function changeStatusTo(label: string): Promise<void> {
      await user.click(screen.getByRole('button', { name: 'Change status' }));
      await user.click(screen.getByRole('combobox', { name: 'New status' }));
      await user.click(await screen.findByRole('option', { name: label }));
      await user.click(screen.getByRole('button', { name: 'Change status' }));
      await waitFor(() => expect(screen.getByText(label)).toBeInTheDocument());
    }

    await changeStatusTo('Suspended');
    let listPage = queryClient.getQueryData([AdminsKeys.List, undefined, undefined]) as {
      pages: { items: { status: string }[] }[];
    };
    expect(listPage.pages[0]?.items[0]?.status).toBe('Suspended');

    await changeStatusTo('Active');
    listPage = queryClient.getQueryData([AdminsKeys.List, undefined, undefined]) as {
      pages: { items: { status: string }[] }[];
    };
    expect(listPage.pages[0]?.items[0]?.status).toBe('Active');
  });
});

describe('AdminDetailScreen — reset password reveals exactly once', () => {
  it('shows the new temporary password once, then it is gone', async () => {
    mockMe('admin.view', 'admin.password.reset');
    server.use(http.get(apiUrl('/api/v1/admins/:id'), () => HttpResponse.json(account())));
    server.use(
      http.post(apiUrl('/api/v1/admins/:id/password-reset'), () =>
        HttpResponse.json({ id: 'admin-1', temporaryPassword: TEMP_PASSWORD }),
      ),
    );

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    const user = userEvent.setup();
    renderScreen(queryClient);

    await screen.findByRole('heading', { name: 'Ngozi Adeyemi' });
    await user.click(screen.getByRole('button', { name: 'Reset password' }));
    await user.click(screen.getByRole('button', { name: 'Reset password' }));

    expect(await screen.findByText(TEMP_PASSWORD)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Done' }));

    expect(screen.queryByText(TEMP_PASSWORD)).not.toBeInTheDocument();
    await waitFor(() => {
      const mutationData = queryClient.getMutationCache().getAll().map((mutation) => mutation.state.data);
      expect(JSON.stringify(mutationData)).not.toContain(TEMP_PASSWORD);
    });
  });
});

describe('AdminDetailScreen — revoke sessions', () => {
  it('asks for confirmation naming the account before calling the endpoint', async () => {
    mockMe('admin.view', 'admin.session.revoke');
    server.use(http.get(apiUrl('/api/v1/admins/:id'), () => HttpResponse.json(account())));
    let revoked = false;
    server.use(
      http.delete(apiUrl('/api/v1/admins/:id/sessions'), () => {
        revoked = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    const user = userEvent.setup();
    renderScreen(queryClient);

    await screen.findByRole('heading', { name: 'Ngozi Adeyemi' });
    await user.click(screen.getByRole('button', { name: 'Revoke sessions' }));

    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('Ngozi Adeyemi'));
    await waitFor(() => expect(revoked).toBe(true));
  });

  it('declining the confirmation never calls the endpoint', async () => {
    mockMe('admin.view', 'admin.session.revoke');
    server.use(http.get(apiUrl('/api/v1/admins/:id'), () => HttpResponse.json(account())));
    let revoked = false;
    server.use(
      http.delete(apiUrl('/api/v1/admins/:id/sessions'), () => {
        revoked = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    const user = userEvent.setup();
    renderScreen(queryClient);

    await screen.findByRole('heading', { name: 'Ngozi Adeyemi' });
    await user.click(screen.getByRole('button', { name: 'Revoke sessions' }));

    expect(revoked).toBe(false);
  });
});
