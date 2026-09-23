import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { WeeklyScreen } from './weekly-screen';

const DATES = ['2027-01-11', '2027-01-12', '2027-01-13', '2027-01-14', '2027-01-15'];
const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];

const blankDays = (overrides: Record<number, Record<string, string>> = {}) =>
  DAYS.map((dayOfWeek, index) => ({
    dayOfWeek, date: DATES[index], behaviour: null, performance: null, dressing: null, homeWork: null, eating: null,
    symptomsOfIllness: null, teacherComment: null, parentComment: null, lastEditedAt: null, lastEditedById: null, lastEditedBy: null,
    ...overrides[index],
  }));

const GRID = {
  armId: 'arm-1', termId: 't-1', weekNumber: 2, weekStartDate: '2027-01-11', weekEndDate: '2027-01-15', outsideTerm: false,
  published: false, publishedAt: null, autoPublish: false, locked: false,
  rows: [
    { pupilId: 'p-1', registrationNumber: 'GRAS/2026/0041', displayName: 'Bello Musa', onRoll: true, illnessDays: 2,
      days: blankDays({ 0: { symptomsOfIllness: 'Cough', behaviour: 'Quiet today' }, 1: { symptomsOfIllness: 'Cough' } }) },
    { pupilId: 'p-2', registrationNumber: 'GRAS/2026/0042', displayName: 'Okafor Chidera', onRoll: true, illnessDays: 0, days: blankDays() },
  ],
  weeks: [
    { weekNumber: 1, startDate: '2027-01-04', endDate: '2027-01-08', outsideTerm: false, published: true, pupilsWithNotes: 2 },
    { weekNumber: 2, startDate: '2027-01-11', endDate: '2027-01-15', outsideTerm: false, published: false, pupilsWithNotes: 1 },
  ],
  phrases: { behaviour: ['Calm and helpful'], performance: [], dressing: [], homeWork: [], eating: [], symptomsOfIllness: [], teacherComment: [], parentComment: [] },
};

type SavedBody = { termId: string; weekNumber: number; cells: { pupilId: string; dayOfWeek: string; field: string; value: string | null }[] };

function mockClass() {
  server.use(
    http.get(apiUrl('/api/v1/sessions'), () =>
      HttpResponse.json({ items: [{ id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active' }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/sessions/:id'), () =>
      HttpResponse.json({
        id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active', armCount: 1,
        terms: [{ id: 't-1', sessionId: 's-1', ordinal: 2, name: 'Second Term', startDate: '2027-01-04', endDate: '2027-04-02', nextResumptionDate: null, timesSchoolOpened: null, state: 'Active', closedAtUtc: null, closedBy: null }],
      }),
    ),
    http.get(apiUrl('/api/v1/arms'), () =>
      HttpResponse.json({ items: [{ id: 'arm-1', displayName: 'Nursery 1A', label: 'A', classLevelId: 'n1', classLevel: 'Nursery 1', sessionId: 's-1', capacity: null, formTeacherAdminId: null, status: 'Active' }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/arms/:armId/weekly'), () => HttpResponse.json(GRID)),
  );
}

describe('WeeklyScreen', () => {
  it('saves only the cell typed into, on blur, with an idempotency key', async () => {
    mockMe('weekly.view', 'weekly.enter');
    mockClass();
    let saved: SavedBody | undefined;
    let key: string | null = null;
    server.use(
      http.put(apiUrl('/api/v1/arms/:armId/weekly'), async ({ request }) => {
        saved = (await request.json()) as SavedBody;
        key = request.headers.get('Idempotency-Key');
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const { user } = renderWithProviders(<WeeklyScreen />);
    await user.type(await screen.findByLabelText('Behaviour, Tuesday, Okafor Chidera'), 'Settled well');
    expect(screen.getByText('1 unsaved change.')).toBeInTheDocument();
    await user.tab();

    await waitFor(() => expect(saved?.cells).toEqual([{ pupilId: 'p-2', dayOfWeek: 'Tuesday', field: 'Behaviour', value: 'Settled well' }]));
    expect(saved?.weekNumber).toBe(2);
    expect(key).toBeTruthy();
    expect(await screen.findByText('All changes saved.')).toBeInTheDocument();
  });

  it('fills a whole-class note down every pupil without a note that day, and marks a pupil unwell two days running', async () => {
    mockMe('weekly.view', 'weekly.enter');
    mockClass();
    let saved: SavedBody | undefined;
    server.use(
      http.put(apiUrl('/api/v1/arms/:armId/weekly'), async ({ request }) => {
        saved = (await request.json()) as SavedBody;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const { user } = renderWithProviders(<WeeklyScreen />);
    expect(await screen.findByText('Unwell 2 days')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Behaviour for every pupil'), 'Class went on excursion');
    await user.click(screen.getByRole('button', { name: 'Apply to every pupil without a note' }));

    // Bello Musa already has a Monday note; a whole-class fact never overwrites it.
    await waitFor(() => expect(saved?.cells.map((cell) => [cell.pupilId, cell.dayOfWeek, cell.value])).toEqual([['p-2', 'Monday', 'Class went on excursion']]));
    expect(screen.getByLabelText('Behaviour, Monday, Bello Musa')).toHaveValue('Quiet today');
  });

  it("shows the server's refusal to publish an empty week, and is read-only without weekly.enter", async () => {
    mockMe('weekly.view', 'weekly.publish');
    mockClass();
    server.use(
      http.post(apiUrl('/api/v1/arms/:armId/weekly/:weekNumber/publish'), () =>
        HttpResponse.json(
          { type: 'about:blank', title: 'Conflict', status: 409, detail: 'Nothing has been written for Week 2 yet. Add at least one note before publishing.', errorCode: 'weekly.nothing_to_publish', traceId: 't' },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    const { user } = renderWithProviders(<WeeklyScreen />);
    expect(await screen.findByLabelText('Behaviour, Monday, Bello Musa')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Apply to every pupil without a note' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Publish week' }));

    expect(await screen.findByText(/Nothing has been written for Week 2 yet/)).toBeInTheDocument();
  });
});
