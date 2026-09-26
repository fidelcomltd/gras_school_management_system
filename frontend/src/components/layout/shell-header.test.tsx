import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';
import type { AuthSession } from '@/lib/auth/auth-session';
import { useSidebarStore } from '@/stores/sidebar-store';
import { render, screen, userEvent, within } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { pupil } from '@/features/pupils/test-fixtures';
import { AuthenticatedShell } from './authenticated-shell';

function session(roleNames: string[], ...privileges: string[]): AuthSession {
  return {
    accountId: 'acc-1',
    email: 'admin@example.com',
    staffName: 'Chisom Maxwell',
    isSuperAdmin: false,
    mustChangePassword: false,
    effectivePrivileges: privileges.map((privilege) => ({ privilege, scope: 'SchoolWide', armIds: [] })),
    roleNames,
    sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
  };
}

function renderShell(value: AuthSession) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const user = userEvent.setup();
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route
            path="*"
            element={
              <AuthenticatedShell session={value}>
                <p>screen content</p>
              </AuthenticatedShell>
            }
          />
        </Routes>
        <Routes>
          <Route path="/pupils/:id" element={<p>pupil record opened</p>} />
          <Route path="/settings" element={<p>settings opened</p>} />
          <Route path="*" element={null} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return user;
}

beforeEach(() => {
  useSidebarStore.setState({ collapsed: false });
});

describe('shell header', () => {
  it("shows the signed-in account's name and role, with several roles summarised", () => {
    renderShell(session(['Class Teacher', 'Head Teacher']));

    const account = screen.getByRole('button', { name: 'Account: Chisom Maxwell, Class Teacher +1' });
    expect(within(account).getByText('Class Teacher +1')).toBeInTheDocument();
  });

  it('finds a pupil by name and opens the record', async () => {
    let searched: string | null = null;
    server.use(
      http.get(apiUrl('/api/v1/pupils'), ({ request }) => {
        searched = new URL(request.url).searchParams.get('search');
        return HttpResponse.json({ items: [pupil({ id: 'p-9', registrationNumber: 'GRA/2026/0009' })], nextCursor: null });
      }),
    );
    const user = renderShell(session([], 'pupil.view'));

    await user.type(screen.getByRole('combobox', { name: 'Search pupils and pages' }), 'Okafor');
    await user.click(await screen.findByRole('option', { name: /OKAFOR Chidera/ }));

    expect(await screen.findByText('pupil record opened')).toBeInTheDocument();
    expect(searched).toBe('Okafor');
  });

  it('says so when the pupil search fails, rather than claiming there is no such pupil', async () => {
    server.use(http.get(apiUrl('/api/v1/pupils'), () => new HttpResponse(null, { status: 500 })));
    const user = renderShell(session([], 'pupil.view'));

    await user.type(screen.getByRole('combobox', { name: 'Search pupils and pages' }), 'Okafor');

    expect(await screen.findByText('Pupil search failed. Check the connection and try again.')).toBeInTheDocument();
    expect(screen.queryByText('No matches.')).not.toBeInTheDocument();
  });

  it('finds a page the caller can open, and never one it cannot', async () => {
    const user = renderShell(session([], 'settings.view'));

    await user.type(screen.getByRole('combobox', { name: 'Search pupils and pages' }), 'sett');
    expect(await screen.findByRole('option', { name: /Settings/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Audit log/ })).not.toBeInTheDocument();

    await user.keyboard('{Enter}');
    expect(await screen.findByText('settings opened')).toBeInTheDocument();
  });
});

describe('sidebar collapse', () => {
  it('collapses to icons, keeping each label as the accessible name, and remembers the choice', async () => {
    const user = renderShell(session([], 'settings.view'));

    await user.click(screen.getAllByRole('button', { name: 'Collapse sidebar' })[0] as HTMLElement);

    expect(useSidebarStore.getState().collapsed).toBe(true);
    expect(screen.getAllByRole('button', { name: 'Expand sidebar' }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('link', { name: 'Settings' }).length).toBeGreaterThan(0);
  });
});
