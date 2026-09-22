import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { cellError, draftFrom, toCommand } from './components/score-draft';
import { MarksScreen } from './marks-screen';
import type { ScoreSheetDto } from './types';

const SHEET: ScoreSheetDto = {
  armId: 'arm-1',
  subjectId: 'maths',
  termId: 't-1',
  version: 'v1',
  resultSet: { id: 'rs-1', state: 'Draft', needsRecompute: false, returnReason: null },
  components: [{ id: 'ca1', label: '1st CA', maxMark: 20 }],
  examination: { id: 'exam', label: 'Exam', maxMark: 60 },
  rows: [
    { pupilId: 'p-1', registrationNumber: 'GRAS/2026/0041', displayName: 'Okafor Chidera', componentMarks: { ca1: 18 }, examMark: 55, examAbsent: false, caTotal: 18, subjectTotal: 73 },
    { pupilId: 'p-2', registrationNumber: 'GRAS/2026/0042', displayName: 'Bello Musa', componentMarks: { ca1: null }, examMark: null, examAbsent: false, caTotal: null, subjectTotal: null },
  ],
};

function mockClass(sheet: ScoreSheetDto = SHEET) {
  server.use(
    http.get(apiUrl('/api/v1/sessions'), () =>
      HttpResponse.json({ items: [{ id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active' }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/sessions/:id'), () =>
      HttpResponse.json({
        id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active', armCount: 1,
        terms: [{ id: 't-1', sessionId: 's-1', ordinal: 1, name: 'First Term', startDate: '2026-09-14', endDate: '2026-12-18', nextResumptionDate: null, timesSchoolOpened: null, state: 'Active', closedAtUtc: null, closedBy: null }],
      }),
    ),
    http.get(apiUrl('/api/v1/arms'), () =>
      HttpResponse.json({ items: [{ id: 'arm-1', displayName: 'Primary 4A', label: 'A', classLevelId: 'p4', classLevel: 'Primary 4', sessionId: 's-1', capacity: null, formTeacherAdminId: null, status: 'Active' }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/arms/:id/subjects'), () =>
      HttpResponse.json([{ subjectId: 'maths', subjectName: 'Mathematics', subjectCode: null, displayOrder: 1, source: 'LevelInherited' }]),
    ),
    http.get(apiUrl('/api/v1/arms/:armId/score-sheets'), () => HttpResponse.json(sheet)),
  );
}

describe('score-draft', () => {
  it('blank is allowed, over-maximum and fractions are not', () => {
    expect(cellError('', 20)).toBeNull();
    expect(cellError('20', 20)).toBeNull();
    expect(cellError('21', 20)).toBe('At most 20');
    expect(cellError('7.5', 20)).toBe('Whole numbers only');
  });

  it('sends blanks as null, and an absent pupil with no exam mark', () => {
    const draft = draftFrom(SHEET);
    const command = toCommand(SHEET, { ...draft, 'p-2': { marks: { ca1: '' }, exam: '40', absent: true } });
    expect(command.version).toBe('v1');
    expect(command.rows[1]).toEqual({ pupilId: 'p-2', componentMarks: { ca1: null }, examMark: null, examAbsent: true });
  });
});

describe('MarksScreen', () => {
  it('saves the typed marks with the sheet version; an out-of-range mark blocks the save', async () => {
    mockMe('result.view', 'result.score.enter');
    mockClass();
    let saved: ReturnType<typeof toCommand> | undefined;
    server.use(
      http.put(apiUrl('/api/v1/arms/:armId/score-sheets'), async ({ request }) => {
        saved = (await request.json()) as ReturnType<typeof toCommand>;
        return HttpResponse.json({ ...SHEET, version: 'v2' });
      }),
    );

    const { user } = renderWithProviders(<MarksScreen />);
    const ca = await screen.findByLabelText('1st CA for Bello Musa');
    await user.type(ca, '25');
    expect(screen.getByRole('button', { name: 'Save marks' })).toBeDisabled();
    expect(screen.getByText('Fix the highlighted marks before saving.')).toBeInTheDocument();

    await user.clear(ca);
    await user.type(ca, '15');
    await user.type(screen.getByLabelText('Exam for Bello Musa'), '44');
    expect(screen.getByRole('row', { name: /Bello Musa/ })).toHaveTextContent('59');
    await user.click(screen.getByRole('button', { name: 'Save marks' }));

    await waitFor(() => expect(saved?.rows[1]).toEqual({ pupilId: 'p-2', componentMarks: { ca1: 15 }, examMark: 44, examAbsent: false }));
    expect(saved?.version).toBe('v1');
  });

  it('locks the sheet once the results are awaiting approval', async () => {
    mockMe('result.view', 'result.score.enter');
    mockClass({ ...SHEET, resultSet: { id: 'rs-1', state: 'AwaitingApproval', needsRecompute: false, returnReason: null } });

    renderWithProviders(<MarksScreen />);

    expect(await screen.findByText(/awaiting approval, so marks are locked/)).toBeInTheDocument();
    expect(screen.getByLabelText('1st CA for Okafor Chidera')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Save marks' })).not.toBeInTheDocument();
  });
});
