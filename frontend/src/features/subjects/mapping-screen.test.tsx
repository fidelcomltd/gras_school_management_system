import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { MappingScreen } from './mapping-screen';

const GRID = {
  levels: [
    { classLevelId: 'p1', classLevelName: 'Primary 1', progressionOrder: 4 },
    { classLevelId: 'p2', classLevelName: 'Primary 2', progressionOrder: 5 },
  ],
  subjects: [
    {
      subjectId: 'maths',
      subjectName: 'Mathematics',
      subjectCode: null,
      cells: [
        { classLevelId: 'p1', mapped: true, displayOrder: 1 },
        { classLevelId: 'p2', mapped: false, displayOrder: null },
      ],
    },
  ],
  armExceptions: [],
};

function mockTerm(state = 'Active') {
  server.use(
    http.get(apiUrl('/api/v1/sessions'), () =>
      HttpResponse.json({ items: [{ id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active' }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/sessions/:id'), () =>
      HttpResponse.json({
        id: 's-1',
        name: '2026/2027',
        startDate: '2026-09-14',
        endDate: '2027-07-25',
        state: 'Active',
        armCount: 2,
        terms: [{ id: 't-1', sessionId: 's-1', ordinal: 1, name: 'First Term', startDate: '2026-09-14', endDate: '2026-12-18', nextResumptionDate: null, timesSchoolOpened: null, state, closedAtUtc: null, closedBy: null }],
      }),
    ),
    http.get(apiUrl('/api/v1/subject-mappings'), () => HttpResponse.json(GRID)),
  );
}

function renderScreen() {
  return renderWithProviders(
    <MemoryRouter>
      <MappingScreen />
    </MemoryRouter>,
  );
}

describe('MappingScreen', () => {
  it('previews a ticked cell as an addition, then applies it with dryRun false', async () => {
    mockMe('subject.view', 'subject.map', 'subject.unmap');
    mockTerm();
    const sent: { dryRun: boolean; entries: { subjectId: string; classLevelId: string; displayOrder: number }[] }[] = [];
    server.use(
      http.put(apiUrl('/api/v1/subject-mappings'), async ({ request }) => {
        const body = (await request.json()) as (typeof sent)[number];
        sent.push(body);
        return HttpResponse.json({
          additions: [{ subjectId: 'maths', subjectName: 'Mathematics', classLevelId: 'p2', classLevelName: 'Primary 2' }],
          endings: [],
          dryRun: body.dryRun,
        });
      }),
    );

    const { user } = renderScreen();
    await user.click(await screen.findByRole('checkbox', { name: 'Mathematics in Primary 2' }));
    await user.click(screen.getByRole('button', { name: 'Review changes' }));

    expect(await screen.findByText('Mathematics in Primary 2')).toBeInTheDocument();
    expect(sent[0]?.dryRun).toBe(true);
    expect(sent[0]?.entries).toEqual([
      { subjectId: 'maths', classLevelId: 'p1', displayOrder: 1 },
      { subjectId: 'maths', classLevelId: 'p2', displayOrder: 1 },
    ]);

    await user.click(screen.getByRole('button', { name: 'Apply changes' }));
    await waitFor(() => expect(sent[1]?.dryRun).toBe(false));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Changes to apply' })).not.toBeInTheDocument());
  });

  it('is read-only for a closed term', async () => {
    mockMe('subject.view', 'subject.map');
    mockTerm('Closed');

    renderScreen();

    expect(await screen.findByRole('checkbox', { name: 'Mathematics in Primary 1' })).toBeDisabled();
    expect(screen.getByText(/This term is closed/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Review changes' })).not.toBeInTheDocument();
  });
});
