import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { renderWithProviders, screen } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import type { AuthSession } from '@/lib/auth/auth-session';
import { Dashboard } from './dashboard';

function session(...privileges: string[]): AuthSession {
  return {
    accountId: 'acc-1',
    email: 'head@example.com',
    staffName: 'Mrs Okonkwo',
    isSuperAdmin: false,
    mustChangePassword: false,
    effectivePrivileges: privileges.map((privilege) => ({ privilege, scope: 'SchoolWide', armIds: [] })),
    sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
  } as unknown as AuthSession;
}

const report = (key: string, rows: { kind: string; cells: (string | null)[] }[]) => ({
  key, title: key, filters: [], orientation: 'Portrait', twoUp: false, columns: [], rows, notes: [], rowCount: rows.length, generatedAtUtc: '2026-12-11T09:30:00Z',
});

function mockReads() {
  server.use(
    http.get(apiUrl('/api/v1/sessions'), () =>
      HttpResponse.json({ items: [{ id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active' }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/sessions/:id'), () =>
      HttpResponse.json({
        id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active', armCount: 2,
        terms: [{ id: 't-1', sessionId: 's-1', ordinal: 1, name: 'First Term', startDate: '2026-09-14', endDate: '2026-12-18', nextResumptionDate: null, timesSchoolOpened: 60, state: 'Active', closedAtUtc: null, closedBy: null }],
      }),
    ),
    http.get(apiUrl('/api/v1/reports/result-entry-progress'), () =>
      HttpResponse.json(report('result-entry-progress', [
        { kind: 'Data', cells: ['Primary 1A', '30', '9', '270 of 270', '', '', '', '', 'Published'] },
        { kind: 'Data', cells: ['Primary 1B', '28', '9', '200 of 252', '', '', '', '', 'Draft'] },
      ])),
    ),
    http.get(apiUrl('/api/v1/reports/enrolment-summary'), () =>
      HttpResponse.json(report('enrolment-summary', [{ kind: 'Total', cells: ['School', '30', '28', '58', '60', '2'] }])),
    ),
    http.get(apiUrl('/api/v1/reports/admissions-pipeline'), () =>
      HttpResponse.json(report('admissions-pipeline', [{ kind: 'Data', cells: ['Primary 1', 'OBI Ada', '01/09/2026', '12', 'Step 3', 'x'] }])),
    ),
    http.get(apiUrl('/api/v1/reports/incomplete-records'), () =>
      HttpResponse.json({ sessionName: '2026/2027', pupilsChecked: 58, counts: [], pupils: [] }),
    ),
    http.get(apiUrl('/api/v1/reports/weekly-illness'), () =>
      HttpResponse.json({ termId: 't-1', items: [{ pupilId: 'p-1', registrationNumber: 'G/1', displayName: 'Eze Chidera', armName: 'Primary 1A', observations: [{ date: '2026-10-01', text: 'x' }, { date: '2026-10-02', text: 'y' }] }] }),
    ),
  );
}

describe('Dashboard', () => {
  it('summarises the term, the roll, admissions and records, and the illness marker for a safeguarding holder', async () => {
    mockReads();

    renderWithProviders(
      <MemoryRouter>
        <Dashboard session={session('report.view', 'pupil.safeguarding.view')} />
      </MemoryRouter>,
    );

    expect(await screen.findByText('1 of 2')).toBeInTheDocument();
    expect(screen.getByText(/Still entering marks: Primary 1B/)).toBeInTheDocument();
    expect(await screen.findByText('58')).toBeInTheDocument();
    expect(screen.getByText(/30 boys, 28 girls; 2 places left/)).toBeInTheDocument();
    expect(await screen.findByText(/The oldest has waited 12 days/)).toBeInTheDocument();
    expect(await screen.findByText(/Eze Chidera/)).toBeInTheDocument();
  });

  it('says there is no session yet instead of spinning, and reports a failed session list as a failure', async () => {
    mockReads();
    server.use(http.get(apiUrl('/api/v1/sessions'), () => HttpResponse.json({ items: [], nextCursor: null })));

    const { unmount } = renderWithProviders(
      <MemoryRouter>
        <Dashboard session={session('report.view')} />
      </MemoryRouter>,
    );
    expect(await screen.findByText('No term has been set up yet.')).toBeInTheDocument();
    expect(screen.getByText('No session has been set up yet.')).toBeInTheDocument();
    unmount();

    server.use(http.get(apiUrl('/api/v1/sessions'), () => HttpResponse.json({}, { status: 500 })));
    renderWithProviders(
      <MemoryRouter>
        <Dashboard session={session('report.view')} />
      </MemoryRouter>,
    );
    expect((await screen.findAllByText('Could not load this. Open it to try again.')).length).toBeGreaterThanOrEqual(2);
  });

  it('shows no illness tile without the safeguarding privilege, and no tiles without report.view', () => {
    mockReads();

    const { unmount } = renderWithProviders(
      <MemoryRouter>
        <Dashboard session={session('report.view')} />
      </MemoryRouter>,
    );
    expect(screen.queryByRole('region', { name: 'Illness noted on several days' })).not.toBeInTheDocument();
    unmount();

    renderWithProviders(
      <MemoryRouter>
        <Dashboard session={session('pupil.view')} />
      </MemoryRouter>,
    );
    expect(screen.getByText('Choose a page from the menu to begin.')).toBeInTheDocument();
  });
});
