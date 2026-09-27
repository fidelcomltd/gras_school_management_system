import { describe, expect, it } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ReportScreen } from './report-screen';
import { ReportsScreen } from './reports-screen';

function mockClasses() {
  server.use(
    http.get(apiUrl('/api/v1/sessions'), () =>
      HttpResponse.json({ items: [{ id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active' }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/sessions/:id'), () =>
      HttpResponse.json({
        id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active', armCount: 1,
        terms: [{ id: 't-1', sessionId: 's-1', ordinal: 1, name: 'First Term', startDate: '2026-09-14', endDate: '2026-12-18', nextResumptionDate: null, timesSchoolOpened: 60, state: 'Active', closedAtUtc: null, closedBy: null }],
      }),
    ),
    http.get(apiUrl('/api/v1/arms'), () =>
      HttpResponse.json({ items: [{ id: 'arm-1', displayName: 'Primary 4A', label: 'A', classLevelId: 'p4', classLevel: 'Primary 4', sessionId: 's-1', capacity: null, formTeacherAdminId: null, status: 'Active' }], nextCursor: null }),
    ),
  );
}

const broadsheet = {
  key: 'broadsheet',
  title: 'Arm broadsheet',
  filters: ['Class: Primary 4A', 'First Term, 2026/2027'],
  orientation: 'Landscape',
  twoUp: false,
  columns: [
    { label: 'Pos.', align: 'Right', group: null },
    { label: 'Name', align: 'Left', group: null },
    { label: 'CA', align: 'Right', group: 'Mathematics' },
    { label: 'Exam', align: 'Right', group: 'Mathematics' },
    { label: 'Average', align: 'Right', group: null },
  ],
  rows: [{ kind: 'Data', cells: ['1', 'EZE Chidera', '38', '52', '90.00'] }],
  notes: ['Not yet published (Approved): figures change if marks are corrected and computed again.'],
  rowCount: 1,
  generatedAtUtc: '2026-12-11T09:30:00Z',
};

function renderReport(key: string, search = '') {
  return renderWithProviders(
    <MemoryRouter initialEntries={[`/reports/${key}${search}`]}>
      <Routes>
        <Route path="/reports/:key" element={<ReportScreen />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('ReportScreen', () => {
  it('shows the broadsheet for the chosen class with grouped headers, and exports it as CSV', async () => {
    mockMe('report.view', 'report.export');
    mockClasses();
    let asked = new URLSearchParams();
    const exported: string[] = [];
    server.use(
      http.get(apiUrl('/api/v1/reports/broadsheet'), ({ request }) => {
        asked = new URL(request.url).searchParams;
        return HttpResponse.json(broadsheet);
      }),
      http.get(apiUrl('/api/v1/reports/broadsheet/export'), ({ request }) => {
        exported.push(new URL(request.url).search);
        return new HttpResponse('Pos.,Name', { headers: { 'Content-Type': 'text/csv' } });
      }),
    );

    const { user } = renderReport('broadsheet');

    expect(await screen.findByRole('cell', { name: 'EZE Chidera' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Mathematics' })).toHaveAttribute('colspan', '2');
    expect(screen.getByText(/Not yet published/)).toBeInTheDocument();
    expect(asked.get('armId')).toBe('arm-1');
    expect(asked.get('termId')).toBe('t-1');

    await user.click(screen.getByRole('button', { name: 'CSV' }));
    await waitFor(() => expect(exported).toHaveLength(1));
    expect(exported[0]).toContain('format=csv');
    expect(exported[0]).toContain('armId=arm-1');
  });

  it('offers no export without report.export', async () => {
    mockMe('report.view');
    mockClasses();
    server.use(http.get(apiUrl('/api/v1/reports/broadsheet'), () => HttpResponse.json(broadsheet)));

    renderReport('broadsheet');

    expect(await screen.findByRole('cell', { name: 'EZE Chidera' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'CSV' })).not.toBeInTheDocument();
  });

  it('asks subject performance for the first level the caller can see', async () => {
    mockMe('report.view');
    mockClasses();
    let asked = new URLSearchParams();
    server.use(
      http.get(apiUrl('/api/v1/reports/subject-performance'), ({ request }) => {
        asked = new URL(request.url).searchParams;
        return HttpResponse.json({ ...broadsheet, key: 'subject-performance', title: 'Subject performance' });
      }),
    );

    renderReport('subject-performance');

    expect(await screen.findByRole('cell', { name: 'EZE Chidera' })).toBeInTheDocument();
    expect(asked.get('levelId')).toBe('p4');
    expect(asked.get('armId')).toBeNull();
  });

  it('shows a pupil record for the pupil named in the address, and asks for one otherwise', async () => {
    mockMe('report.view');
    mockClasses();
    let asked = '';
    server.use(
      http.get(apiUrl('/api/v1/reports/pupil-record'), ({ request }) => {
        asked = new URL(request.url).searchParams.get('pupilId') ?? '';
        return HttpResponse.json({ ...broadsheet, key: 'pupil-record', title: 'Pupil cumulative record' });
      }),
    );

    renderReport('pupil-record', '?pupilId=p-9');

    expect(await screen.findByRole('cell', { name: 'EZE Chidera' })).toBeInTheDocument();
    expect(asked).toBe('p-9');
  });

  it('asks for a pupil when the address names none', () => {
    mockMe('report.view');
    mockClasses();

    renderReport('pupil-record');

    return waitFor(() => expect(screen.getByText(/Open a pupil’s record/)).toBeInTheDocument());
  });

  it('lists only the reports the caller may open', () => {
    mockMe('report.view');

    renderWithProviders(
      <MemoryRouter>
        <ReportsScreen />
      </MemoryRouter>,
    );

    return waitFor(() => {
      expect(screen.getByRole('link', { name: /Arm broadsheet/ })).toHaveAttribute('href', '/reports/broadsheet');
      expect(screen.queryByRole('link', { name: /safeguarding/i })).not.toBeInTheDocument();
    });
  });
});
