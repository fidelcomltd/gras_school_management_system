import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { SessionsListScreen } from './sessions-list-screen';

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
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <SessionsListScreen />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('SessionsListScreen — four required states', () => {
  it('loading: shows a status region before /sessions resolves', () => {
    mockMe('session.view');
    server.use(http.get(apiUrl('/api/v1/sessions'), () => new Promise(() => undefined)));

    renderScreen();

    expect(screen.getByText('Loading sessions…')).toBeInTheDocument();
  });

  it('empty: renders a message rather than a blank list', async () => {
    mockMe('session.view');
    server.use(http.get(apiUrl('/api/v1/sessions'), () => HttpResponse.json({ items: [], nextCursor: null })));

    renderScreen();

    expect(await screen.findByText('No sessions yet.')).toBeInTheDocument();
  });

  it('unauthorized: renders nothing — ProtectedLayout is already navigating away', async () => {
    mockMe('session.view');
    server.use(
      http.get(apiUrl('/api/v1/sessions'), () => problemResponse(401, { errorCode: 'authentication.required' })),
    );

    const { container } = renderScreen();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('error: a server failure shows a retry, not a crash', async () => {
    mockMe('session.view');
    server.use(http.get(apiUrl('/api/v1/sessions'), () => problemResponse(500)));

    renderScreen();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('success: lists sessions with their state, newest first as the server returns them', async () => {
    mockMe('session.view');
    server.use(
      http.get(apiUrl('/api/v1/sessions'), () =>
        HttpResponse.json({
          items: [{ id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active' }],
          nextCursor: null,
        }),
      ),
    );

    renderScreen();

    expect(await screen.findByRole('link', { name: /2026\/2027/ })).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
  });
});

describe('SessionsListScreen — create session', () => {
  it('sends a fresh Idempotency-Key per submit and surfaces a business-rule 422 verbatim', async () => {
    mockMe('session.view', 'session.create');
    server.use(http.get(apiUrl('/api/v1/sessions'), () => HttpResponse.json({ items: [], nextCursor: null })));

    const keysSeen: string[] = [];
    server.use(
      http.post(apiUrl('/api/v1/sessions'), ({ request }) => {
        const key = request.headers.get('Idempotency-Key');
        if (key) keysSeen.push(key);
        return problemResponse(422, {
          detail: 'A session already covers part of this date range.',
          errors: {},
        });
      }),
    );

    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'New session' }));
    await user.type(screen.getByLabelText('Session name'), '2027/2028');
    fireEvent.change(screen.getByLabelText('Session start date'), { target: { value: '2027-09-13' } });
    fireEvent.change(screen.getByLabelText('Session end date'), { target: { value: '2028-07-23' } });
    for (const ordinal of ['First', 'Second', 'Third']) {
      fireEvent.change(screen.getByLabelText(`${ordinal} term start date`), { target: { value: '2027-09-13' } });
      fireEvent.change(screen.getByLabelText(`${ordinal} term end date`), { target: { value: '2027-12-17' } });
    }
    await user.click(screen.getByRole('button', { name: 'Create session' }));

    expect(await screen.findByText('A session already covers part of this date range.')).toBeInTheDocument();

    // A second distinct submit must not reuse the same key.
    await user.click(screen.getByRole('button', { name: 'Create session' }));
    await waitFor(() => expect(keysSeen.length).toBe(2));
    expect(new Set(keysSeen).size).toBe(2);
  }, 15000);
});
