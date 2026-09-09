import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { RequirePrivilege } from './require-privilege';

/**
 * The second half of TASK-0041 AC-3: an authenticated visit to a route the
 * caller's privileges do not permit renders a 403, distinct from the 404
 * fallback — proven here by asserting the "Access denied" heading appears
 * (never "Page not found"), not merely that the children are absent.
 */
function renderGuarded(privilege: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <RequirePrivilege privilege={privilege}>
        <p>protected settings content</p>
      </RequirePrivilege>
    </QueryClientProvider>,
  );
}

function sessionWith(...privileges: string[]) {
  return {
    accountId: 'acc-1',
    email: 'admin@example.com',
    staffName: 'Chisom Maxwell',
    isSuperAdmin: false,
    mustChangePassword: false,
    effectivePrivileges: privileges.map((privilege) => ({ privilege, scope: 'SchoolWide', armIds: [] })),
    sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
  };
}

describe('RequirePrivilege', () => {
  it('renders the children when the caller holds the privilege', async () => {
    server.use(http.get(apiUrl('/api/v1/auth/me'), () => HttpResponse.json(sessionWith('settings.view'))));

    renderGuarded('settings.view');

    expect(await screen.findByText('protected settings content')).toBeInTheDocument();
  });

  it('renders a 403, not a 404, when the caller lacks the privilege', async () => {
    server.use(http.get(apiUrl('/api/v1/auth/me'), () => HttpResponse.json(sessionWith())));

    renderGuarded('settings.view');

    expect(await screen.findByRole('heading', { name: 'Access denied' })).toBeInTheDocument();
    expect(screen.queryByText('protected settings content')).not.toBeInTheDocument();
    expect(screen.queryByText('Page not found')).not.toBeInTheDocument();
  });
});
