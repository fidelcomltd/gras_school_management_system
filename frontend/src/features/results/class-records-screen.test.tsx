import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ClassRecordsScreen } from './class-records-screen';

const DRAFT = { id: 'rs-1', state: 'Draft', needsRecompute: false, returnReason: null };
const ROWS = [
  { pupilId: 'p-1', registrationNumber: 'GRAS/2026/0041', displayName: 'Okafor Chidera' },
  { pupilId: 'p-2', registrationNumber: 'GRAS/2026/0042', displayName: 'Bello Musa' },
];

function mockPrimaryClass() {
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
    http.get(apiUrl('/api/v1/levels'), () =>
      HttpResponse.json({ items: [{ id: 'p4', name: 'Primary 4', sectionId: 'primary', section: 'Primary', status: 'Active', progressionOrder: 7, nextLevelId: null, isEntryLevel: false, isGraduatingLevel: false }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/sections'), () => HttpResponse.json({ sections: [{ id: 'primary', name: 'Primary', ratesTraits: true }] })),
    http.get(apiUrl('/api/v1/remark-templates'), () => HttpResponse.json({ templates: [] })),
    http.get(apiUrl('/api/v1/arms/:armId/trait-ratings'), () =>
      HttpResponse.json({
        armId: 'arm-1', termId: 't-1', version: null, resultSet: null,
        blocks: [{ domain: 'Affective', scale: { id: 'sc', name: 'Primary trait', points: [{ id: 'pt-e', pointCode: 'E', pointLabel: 'Excellent', pointOrder: 3 }] }, traits: [{ id: 'punct', domain: 'Affective', name: 'Punctuality', displayOrder: 1, status: 'Active' }] }],
        rows: ROWS.map((row) => ({ ...row, ratings: { punct: null } })),
      }),
    ),
    http.get(apiUrl('/api/v1/arms/:armId/attendance'), () =>
      HttpResponse.json({ armId: 'arm-1', termId: 't-1', version: 'v1', resultSet: DRAFT, timesSchoolOpened: 60, rows: ROWS.map((row) => ({ ...row, timesPresent: null, timesAbsent: null })) }),
    ),
    http.get(apiUrl('/api/v1/arms/:armId/head-teacher-remarks'), () =>
      HttpResponse.json({ armId: 'arm-1', termId: 't-1', version: 'v1', resultSet: DRAFT, rows: ROWS.map((row) => ({ ...row, remark: null, writtenByName: null, writtenAt: null })) }),
    ),
  );
}

describe('ClassRecordsScreen', () => {
  it('rates a primary class on its traits and saves the chosen point', async () => {
    mockMe('result.view', 'result.trait.enter');
    mockPrimaryClass();
    let saved: { rows: { pupilId: string; ratings: Record<string, string | null> }[] } | undefined;
    server.use(
      http.put(apiUrl('/api/v1/arms/:armId/trait-ratings'), async ({ request }) => {
        saved = (await request.json()) as typeof saved;
        return HttpResponse.json({ armId: 'arm-1', termId: 't-1', version: 'v2', resultSet: DRAFT, blocks: [], rows: [] });
      }),
    );

    const { user } = renderWithProviders(<ClassRecordsScreen />);
    await user.selectOptions(await screen.findByLabelText('Punctuality for Bello Musa'), 'pt-e');
    await user.click(screen.getByRole('button', { name: 'Save ratings' }));

    await waitFor(() => expect(saved?.rows[1]).toEqual({ pupilId: 'p-2', ratings: { punct: 'pt-e' } }));
    expect(saved?.rows[0]).toEqual({ pupilId: 'p-1', ratings: { punct: null } });
  });

  it('attendance derives absent days and refuses more days present than school opened', async () => {
    mockMe('result.view', 'result.attendance.enter');
    mockPrimaryClass();

    const { user } = renderWithProviders(<ClassRecordsScreen />);
    await user.click(await screen.findByRole('tab', { name: 'Attendance' }));
    const present = await screen.findByLabelText('Times present for Okafor Chidera');
    await user.type(present, '55');
    expect(screen.getByRole('row', { name: /Okafor Chidera/ }).lastElementChild).toHaveTextContent(/^5$/);
    await user.clear(present);
    await user.type(present, '61');

    expect(screen.getByRole('button', { name: 'Save attendance' })).toBeDisabled();
    expect(screen.getByText('Times present must be a whole number from 0 to 60.')).toBeInTheDocument();
  });

  it("the head teacher's fill-empty phrase is sent with the save", async () => {
    mockMe('result.view', 'result.remark.headteacher');
    mockPrimaryClass();
    let saved: { rows: unknown[]; fillEmpty: string | null } | undefined;
    server.use(
      http.put(apiUrl('/api/v1/arms/:armId/head-teacher-remarks'), async ({ request }) => {
        saved = (await request.json()) as typeof saved;
        return HttpResponse.json({ armId: 'arm-1', termId: 't-1', version: 'v2', resultSet: DRAFT, rows: [] });
      }),
    );

    const { user } = renderWithProviders(<ClassRecordsScreen />);
    await user.click(await screen.findByRole('tab', { name: "Head teacher's remarks" }));
    await user.type(await screen.findByLabelText('Remark for Okafor Chidera'), 'A pleasure to teach.');
    await user.type(screen.getByLabelText('Fill every pupil still without a remark with (optional)'), 'Keep it up.');
    await user.click(screen.getByRole('button', { name: 'Save remarks' }));

    await waitFor(() => expect(saved?.fillEmpty).toBe('Keep it up.'));
    expect(saved?.rows).toEqual([{ pupilId: 'p-1', remark: 'A pleasure to teach.' }]);
  }, 15_000);
});
