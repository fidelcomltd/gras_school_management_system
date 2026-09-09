import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { SessionDetailScreen } from './session-detail-screen';

const SESSION_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41';

const DETAIL = {
  id: SESSION_ID,
  name: '2026/2027',
  startDate: '2026-09-14',
  endDate: '2027-07-25',
  state: 'Active',
  terms: [
    {
      id: 't-1',
      sessionId: SESSION_ID,
      ordinal: 1,
      name: 'First Term',
      startDate: '2026-09-14',
      endDate: '2026-12-18',
      timesSchoolOpened: 62,
      nextResumptionDate: '2027-01-05',
      state: 'Closed',
      closedAtUtc: '2026-08-03T09:30:00+00:00',
      closedBy: 'admin-1',
    },
    {
      id: 't-2',
      sessionId: SESSION_ID,
      ordinal: 2,
      name: 'Second Term',
      startDate: '2027-01-05',
      endDate: '2027-04-02',
      timesSchoolOpened: null,
      nextResumptionDate: '2027-04-20',
      state: 'Upcoming',
      closedAtUtc: null,
      closedBy: null,
    },
    {
      id: 't-3',
      sessionId: SESSION_ID,
      ordinal: 3,
      name: 'Third Term',
      startDate: '2027-04-20',
      endDate: '2027-07-25',
      timesSchoolOpened: null,
      nextResumptionDate: null,
      state: 'Upcoming',
      closedAtUtc: null,
      closedBy: null,
    },
  ],
};

function mockMe(options: { privileges: string[]; isSuperAdmin?: boolean }) {
  server.use(
    http.get(apiUrl('/api/v1/auth/me'), () =>
      HttpResponse.json({
        accountId: 'acc-1',
        email: 'admin@example.com',
        staffName: 'Chisom Maxwell',
        isSuperAdmin: options.isSuperAdmin ?? false,
        mustChangePassword: false,
        effectivePrivileges: options.privileges.map((privilege) => ({
          privilege,
          scope: 'SchoolWide',
          armIds: [],
        })),
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
      <MemoryRouter initialEntries={[`/sessions/${SESSION_ID}`]}>
        <Routes>
          <Route path="/sessions/:id" element={<SessionDetailScreen />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('SessionDetailScreen — term transitions', () => {
  it('opening the second term surfaces the server precondition message verbatim on failure', async () => {
    mockMe({ privileges: ['session.view', 'term.open'] });
    server.use(http.get(apiUrl(`/api/v1/sessions/${SESSION_ID}`), () => HttpResponse.json(DETAIL)));
    server.use(
      http.post(apiUrl('/api/v1/terms/t-2/open'), () =>
        problemResponse(409, { detail: 'The previous term must be closed before this one can open.' }),
      ),
    );

    const user = userEvent.setup();
    renderScreen();

    // Both Second and Third term are `Upcoming`, so both render an Open
    // button; the first in document order is Second Term's.
    const [openSecondTerm] = await screen.findAllByRole('button', { name: 'Open term' });
    if (!openSecondTerm) throw new Error('expected an Open term button');
    await user.click(openSecondTerm);

    expect(
      await screen.findByText('The previous term must be closed before this one can open.'),
    ).toBeInTheDocument();
  });

  it('a caller without term.open sees no Open button', async () => {
    mockMe({ privileges: ['session.view'] });
    server.use(http.get(apiUrl(`/api/v1/sessions/${SESSION_ID}`), () => HttpResponse.json(DETAIL)));

    renderScreen();

    await screen.findByRole('heading', { name: '2026/2027' });
    expect(screen.queryByRole('button', { name: 'Open term' })).not.toBeInTheDocument();
  });

  it('reopen is unavailable to a non-super-admin holder of term.close', async () => {
    mockMe({ privileges: ['session.view', 'term.close'], isSuperAdmin: false });
    server.use(http.get(apiUrl(`/api/v1/sessions/${SESSION_ID}`), () => HttpResponse.json(DETAIL)));

    renderScreen();

    await screen.findByRole('heading', { name: '2026/2027' });
    expect(screen.queryByRole('button', { name: 'Reopen' })).not.toBeInTheDocument();
  });

  it('a super admin can reopen only with a reason of at least 10 characters', async () => {
    mockMe({ privileges: ['session.view', 'term.close'], isSuperAdmin: true });
    server.use(http.get(apiUrl(`/api/v1/sessions/${SESSION_ID}`), () => HttpResponse.json(DETAIL)));

    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Reopen' }));
    await user.type(screen.getByLabelText('Reason'), 'too short');
    await user.click(screen.getByRole('button', { name: 'Reopen term' }));

    expect(await screen.findByText('Give a reason of at least 10 characters.')).toBeInTheDocument();
  });

  it('the following term already opened refusal surfaces verbatim', async () => {
    mockMe({ privileges: ['session.view', 'term.close'], isSuperAdmin: true });
    server.use(http.get(apiUrl(`/api/v1/sessions/${SESSION_ID}`), () => HttpResponse.json(DETAIL)));
    server.use(
      http.post(apiUrl('/api/v1/terms/t-1/reopen'), () =>
        problemResponse(409, { detail: 'The following term has already been opened.' }),
      ),
    );

    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Reopen' }));
    await user.type(screen.getByLabelText('Reason'), 'a genuinely long enough reason');
    await user.click(screen.getByRole('button', { name: 'Reopen term' }));

    await waitFor(() =>
      expect(screen.getByText('The following term has already been opened.')).toBeInTheDocument(),
    );
  });
});

describe('SessionDetailScreen — edit session and term', () => {
  it('editing the session name (UpdateSession) round-trips through the dialog', async () => {
    mockMe({ privileges: ['session.view', 'session.update'] });
    // Stateful, like a real backend: the refetch `useUpdateSession` triggers
    // on success must see the rename too, or the heading would revert.
    let current = DETAIL;
    server.use(http.get(apiUrl(`/api/v1/sessions/${SESSION_ID}`), () => HttpResponse.json(current)));
    server.use(
      http.patch(apiUrl(`/api/v1/sessions/${SESSION_ID}`), () => {
        current = { ...current, name: '2029/2030' };
        return HttpResponse.json(current);
      }),
    );

    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Edit session' }));
    await user.clear(screen.getByLabelText('Session name'));
    await user.type(screen.getByLabelText('Session name'), '2029/2030');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByRole('heading', { name: '2029/2030' })).toBeInTheDocument();
  });

  it('editing a term (UpdateTerm) surfaces a rejection verbatim', async () => {
    mockMe({ privileges: ['session.view', 'session.update'] });
    server.use(http.get(apiUrl(`/api/v1/sessions/${SESSION_ID}`), () => HttpResponse.json(DETAIL)));
    server.use(
      http.patch(apiUrl('/api/v1/terms/t-1'), () =>
        problemResponse(409, { detail: 'Times school opened cannot change once the term is closed.' }),
      ),
    );

    const user = userEvent.setup();
    renderScreen();

    // First Term (t-1, `Closed`) is the first "Edit" button in document order.
    const [editFirstTerm] = await screen.findAllByRole('button', { name: 'Edit' });
    if (!editFirstTerm) throw new Error('expected an Edit button');
    await user.click(editFirstTerm);
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(
      await screen.findByText('Times school opened cannot change once the term is closed.'),
    ).toBeInTheDocument();
  });

  it('closing the active term (CloseTerm) surfaces a rejection verbatim', async () => {
    const activeTermDetail = {
      ...DETAIL,
      terms: [DETAIL.terms[0], { ...DETAIL.terms[1], state: 'Active' }, DETAIL.terms[2]],
    };
    mockMe({ privileges: ['session.view', 'term.close'] });
    server.use(http.get(apiUrl(`/api/v1/sessions/${SESSION_ID}`), () => HttpResponse.json(activeTermDetail)));
    server.use(
      http.post(apiUrl('/api/v1/terms/t-2/close'), () =>
        problemResponse(422, { detail: 'Times school opened must be filled in before closing.' }),
      ),
    );

    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole('button', { name: 'Close term' }));

    expect(
      await screen.findByText('Times school opened must be filled in before closing.'),
    ).toBeInTheDocument();
  });
});
