import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { AdmissionQueueList } from './admission-queue-list';
import { pupil } from './test-fixtures';

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

function renderList() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AdmissionQueueList />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('AdmissionQueueList — four required states', () => {
  it('loading, then empty when there is nothing pending', async () => {
    mockMe('pupil.view');
    server.use(http.get(apiUrl('/api/v1/admissions'), () => HttpResponse.json({ items: [], nextCursor: null })));

    renderList();

    expect(screen.getByText('Loading admissions queue…')).toBeInTheDocument();
    expect(await screen.findByText('No pending applications.')).toBeInTheDocument();
  });

  it('unauthorized renders nothing', async () => {
    mockMe('pupil.view');
    server.use(http.get(apiUrl('/api/v1/admissions'), () => problemResponse(401)));

    const { container } = renderList();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('error shows a retry', async () => {
    mockMe('pupil.view');
    server.use(http.get(apiUrl('/api/v1/admissions'), () => problemResponse(500)));

    renderList();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });

  it('renders name, level applied for, date received and missing as prose', async () => {
    mockMe('pupil.view');
    server.use(
      http.get(apiUrl('/api/v1/admissions'), () => HttpResponse.json({ items: [pupil()], nextCursor: null })),
    );

    renderList();

    expect(await screen.findByText('Okafor Chidera')).toBeInTheDocument();
    expect(screen.getByText(/Primary 2/)).toBeInTheDocument();
    expect(screen.getByText(/2026-08-01/)).toBeInTheDocument();
    expect(screen.getByText('Still needed: Declaration (Section I)')).toBeInTheDocument();
  });
});

describe('AdmissionQueueList — privilege gating', () => {
  it('hides Approve/Decline entirely without pupil.admission.approve', async () => {
    mockMe('pupil.view');
    server.use(
      http.get(apiUrl('/api/v1/admissions'), () => HttpResponse.json({ items: [pupil()], nextCursor: null })),
    );

    renderList();

    await screen.findByText('Okafor Chidera');
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Decline' })).not.toBeInTheDocument();
  });

  it('shows both actions with pupil.admission.approve', async () => {
    mockMe('pupil.view', 'pupil.admission.approve');
    server.use(
      http.get(apiUrl('/api/v1/admissions'), () => HttpResponse.json({ items: [pupil()], nextCursor: null })),
    );

    renderList();

    expect(await screen.findByRole('button', { name: 'Approve' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Decline' })).toBeInTheDocument();
  });
});

describe('AdmissionQueueList — opening a decision dialog', () => {
  it('Decline opens the reason dialog; Approve opens the approval form', async () => {
    mockMe('pupil.view', 'pupil.admission.approve');
    server.use(
      http.get(apiUrl('/api/v1/admissions'), () => HttpResponse.json({ items: [pupil()], nextCursor: null })),
    );

    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'Decline' }));
    expect(screen.getByRole('heading', { name: 'Decline Okafor Chidera' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    await user.click(await screen.findByRole('button', { name: 'Approve' }));
    expect(screen.getByRole('heading', { name: 'Approve Okafor Chidera' })).toBeInTheDocument();
  });
});
