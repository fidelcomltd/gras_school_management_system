import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { LandingScreen } from './landing-screen';

/** `LandingScreen` renders `<Navigate>` on 401, so it needs real routing context. */
function renderLanding() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/" element={<LandingScreen />} />
          <Route path="/sign-in" element={<p>sign-in reached</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const SESSION = {
  accountId: 'acc-1',
  email: 'admin@example.com',
  staffName: 'Chisom Maxwell',
  isSuperAdmin: true,
  mustChangePassword: false,
  effectivePrivileges: [],
  sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
  sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
};

describe('LandingScreen — four required states', () => {
  it('loading: shows a status region before /me resolves', () => {
    server.use(http.get(apiUrl('/api/v1/auth/me'), async () => new Promise(() => undefined)));
    renderLanding();
    expect(screen.getByText('Loading your account…')).toBeInTheDocument();
  });

  it('unauthorized: redirects to sign-in on a 401', async () => {
    server.use(
      http.get(apiUrl('/api/v1/auth/me'), () =>
        problemResponse(401, { errorCode: 'authentication.required' }),
      ),
    );
    renderLanding();
    expect(await screen.findByText('sign-in reached')).toBeInTheDocument();
  });

  it('error: a network/server failure shows a retry, not a redirect', async () => {
    server.use(http.get(apiUrl('/api/v1/auth/me'), () => problemResponse(500)));
    renderLanding();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('success: greets the signed-in staff member by name', async () => {
    server.use(http.get(apiUrl('/api/v1/auth/me'), () => HttpResponse.json(SESSION)));
    renderLanding();

    expect(await screen.findByRole('heading', { name: 'Welcome, Chisom Maxwell' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
  });

  it('mustChangePassword: shows the forced-change message instead of the dashboard', async () => {
    server.use(
      http.get(apiUrl('/api/v1/auth/me'), () =>
        HttpResponse.json({ ...SESSION, mustChangePassword: true }),
      ),
    );
    renderLanding();

    expect(
      await screen.findByText('You must change your password before continuing.'),
    ).toBeInTheDocument();
    // Sign-out stays available — it's one of the three endpoints exempt from
    // the must-change-password gate (delta §2a).
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
  });
});
