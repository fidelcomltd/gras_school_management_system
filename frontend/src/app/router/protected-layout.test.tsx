import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { paths } from './paths';
import { ProtectedLayout } from './protected-layout';

/**
 * The route guard (TASK-0041 AC-3, first half): unauthenticated → sign-in.
 * Built as a small standalone router with the real `ProtectedLayout`, rather
 * than importing the full app router, so this stays a unit test of the guard
 * itself.
 */
function renderGuardedApp(initialEntry: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: paths.signIn, element: <p>sign-in reached</p> },
      {
        element: <ProtectedLayout />,
        children: [{ index: true, element: <p>protected content</p> }],
      },
    ],
    { initialEntries: [initialEntry] },
  );
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
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

describe('ProtectedLayout', () => {
  it('redirects an unauthenticated visitor to sign-in, not a blank protected page', async () => {
    server.use(
      http.get(apiUrl('/api/v1/auth/me'), () =>
        problemResponse(401, { errorCode: 'authentication.required' }),
      ),
    );

    renderGuardedApp(paths.root);

    expect(await screen.findByText('sign-in reached')).toBeInTheDocument();
  });

  it('renders the shell and the protected content for an authenticated visitor', async () => {
    server.use(http.get(apiUrl('/api/v1/auth/me'), () => HttpResponse.json(SESSION)));

    renderGuardedApp(paths.root);

    expect(await screen.findByText('protected content')).toBeInTheDocument();
    // The shell chrome — nav + identity + sign-out — is there too, not just the child route.
    expect(screen.getByText('Chisom Maxwell')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
  });
});
