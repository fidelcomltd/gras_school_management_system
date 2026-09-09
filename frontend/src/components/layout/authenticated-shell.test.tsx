import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { __resetAuthSession, getSession, setSession, type AuthSession } from '@/lib/auth/auth-session';
import { AuthenticatedShell } from './authenticated-shell';

/**
 * Nav visibility (AC-2) and sign-out order (AC-4). Both need real routing
 * context (`NavLink`) and real server state (`@/lib/auth/auth-session`'s
 * module-level session), not a mocked hook.
 */
function renderShell(session: AuthSession) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/']}>
        <AuthenticatedShell session={session}>
          <p>screen content</p>
        </AuthenticatedShell>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function sessionWith(...privileges: string[]): AuthSession {
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

beforeEach(() => {
  __resetAuthSession();
});

afterEach(() => {
  __resetAuthSession();
});

describe('AuthenticatedShell — nav visibility (AC-2)', () => {
  it('shows Settings for a caller holding settings.view', () => {
    renderShell(sessionWith('settings.view'));
    expect(screen.getByRole('link', { name: 'Settings' })).toBeInTheDocument();
  });

  it('omits Settings for a caller without it — absence, not a disabled link', () => {
    renderShell(sessionWith());
    expect(screen.queryByRole('link', { name: 'Settings' })).not.toBeInTheDocument();
  });

  it('always shows Home, which needs no privilege', () => {
    renderShell(sessionWith());
    expect(screen.getByRole('link', { name: 'Home' })).toBeInTheDocument();
  });

  it('shows Sessions only for a caller holding session.view (TASK-0042)', () => {
    renderShell(sessionWith('session.view'));
    expect(screen.getByRole('link', { name: 'Sessions' })).toBeInTheDocument();
  });

  it('omits Sessions for a caller without it', () => {
    renderShell(sessionWith());
    expect(screen.queryByRole('link', { name: 'Sessions' })).not.toBeInTheDocument();
  });

  it('shows Classes only for a caller holding level.view (TASK-0042)', () => {
    renderShell(sessionWith('level.view'));
    expect(screen.getByRole('link', { name: 'Classes' })).toBeInTheDocument();
  });

  it('omits Classes for a caller without it', () => {
    renderShell(sessionWith());
    expect(screen.queryByRole('link', { name: 'Classes' })).not.toBeInTheDocument();
  });

  it('shows Arms only for a caller holding arm.view (TASK-0045)', () => {
    renderShell(sessionWith('arm.view'));
    expect(screen.getByRole('link', { name: 'Arms' })).toBeInTheDocument();
  });

  it('omits Arms for a caller without it', () => {
    renderShell(sessionWith());
    expect(screen.queryByRole('link', { name: 'Arms' })).not.toBeInTheDocument();
  });
});

describe('AuthenticatedShell — sign-out order (AC-4)', () => {
  it('fires the server call before clearing client session state', async () => {
    const session = sessionWith();
    setSession(session);
    let requestReceived = false;
    let resolveSignOut = (): void => undefined;
    server.use(
      http.post(
        apiUrl('/api/v1/auth/sign-out'),
        () =>
          new Promise((resolve) => {
            requestReceived = true;
            resolveSignOut = () => resolve(new HttpResponse(null, { status: 204 }));
          }),
      ),
    );

    const user = userEvent.setup();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={['/']}>
          <AuthenticatedShell session={session}>
            <p>screen content</p>
          </AuthenticatedShell>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await user.click(screen.getByRole('button', { name: 'Sign out' }));

    // The request has fired but the response has not resolved yet — client
    // state must still show the session as active (§5: revoke THEN clear).
    await waitFor(() => expect(requestReceived).toBe(true));
    expect(getSession()).not.toBeNull();

    resolveSignOut();

    await waitFor(() => expect(getSession()).toBeNull());
  });
});
