import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor, within } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { PromotionScreen } from './promotion-screen';
import type { PromotionPreviewDto } from './types';

const SOURCE = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41';
const TARGET = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d42';
const P2 = 'level-p2';
const P3 = 'level-p3';

function preview(overrides: Partial<PromotionPreviewDto> = {}): PromotionPreviewDto {
  return {
    sourceSession: { id: SOURCE, name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-23' },
    targetSession: { id: TARGET, name: '2027/2028', startDate: '2027-09-13', endDate: '2028-07-21' },
    blockers: [],
    rows: [
      {
        pupilId: 'pupil-strong', displayName: 'Okafor Ada', registrationNumber: 'GRA/2026/0001', currentArmId: 'arm-2a', currentArmName: 'Primary 2A',
        classLevelId: P2, nextLevelId: P3, annualAverage: 75, coreResults: [{ subjectId: 'maths', mean: 70, passed: true }],
        proposedOutcome: 'Promoted', proposedTargetArmId: 'arm-3a',
      },
      {
        pupilId: 'pupil-weak', displayName: 'Bello Tunde', registrationNumber: 'GRA/2026/0002', currentArmId: 'arm-2a', currentArmName: 'Primary 2A',
        classLevelId: P2, nextLevelId: P3, annualAverage: 30, coreResults: [{ subjectId: 'maths', mean: 25, passed: false }],
        proposedOutcome: 'Repeat', proposedTargetArmId: 'arm-2a-next',
      },
    ],
    targetArms: [
      { armId: 'arm-3a', name: 'Primary 3A', classLevelId: P3, capacity: 1, enrolledCount: 0 },
      { armId: 'arm-2a-next', name: 'Primary 2A', classLevelId: P2, capacity: 30, enrolledCount: 0 },
    ],
    coreSubjects: [{ subjectId: 'maths', name: 'Mathematics' }],
    excluded: [{ pupilId: 'pupil-gone', displayName: 'Eze Obi', registrationNumber: 'GRA/2025/0009', status: 'Withdrawn' }],
    committedBatch: null,
    canDecide: true,
    ...overrides,
  };
}

function mockMe(privileges: string[], isSuperAdmin = false) {
  server.use(
    http.get(apiUrl('/api/v1/auth/me'), () =>
      HttpResponse.json({
        accountId: 'acc-1', email: 'admin@example.com', staffName: 'Chisom Maxwell', isSuperAdmin, mustChangePassword: false,
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
      <MemoryRouter initialEntries={[`/sessions/${SOURCE}/promotion`]}>
        <Routes>
          <Route path="/sessions/:id/promotion" element={<PromotionScreen />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PromotionScreen', () => {
  it('commits every pupil with the edited outcome, target arm and on-trial reason', async () => {
    mockMe(['promotion.run', 'promotion.decide']);
    server.use(http.get(apiUrl(`/api/v1/sessions/${SOURCE}/promotion/preview`), () => HttpResponse.json(preview())));
    let posted: unknown;
    server.use(
      http.post(apiUrl(`/api/v1/sessions/${SOURCE}/promotion`), async ({ request }) => {
        posted = await request.json();
        return HttpResponse.json({
          id: 'batch-1', sourceSessionId: SOURCE, targetSessionId: TARGET, targetSessionName: '2027/2028', state: 'Committed',
          committedAtUtc: '2027-07-28T10:15:00+00:00', reversedAtUtc: null, promoted: 1, repeated: 0, promotedOnTrial: 1, graduated: 0,
        });
      }),
    );
    const user = userEvent.setup();
    renderScreen();

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Outcome for Bello Tunde' }), 'PromotedOnTrial');
    // The arm follows the new destination level, and the capacity strip warns rather than blocks.
    expect(screen.getByRole('combobox', { name: 'Target arm for Bello Tunde' })).toHaveValue('arm-3a');
    expect(screen.getByText(/Over capacity: Primary 3A/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Commit promotion' })).toBeDisabled();

    await user.type(screen.getByRole('textbox', { name: 'Reason for promoting Bello Tunde on trial' }), 'Strong Third Term after illness.');
    await user.click(screen.getByRole('button', { name: 'Commit promotion' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Commit promotion' }));

    await waitFor(() => expect(posted).toBeDefined());
    expect(posted).toEqual({
      sessionId: SOURCE,
      targetSessionId: TARGET,
      decisions: [
        { pupilId: 'pupil-strong', outcome: 'Promoted', targetArmId: 'arm-3a', reason: null },
        { pupilId: 'pupil-weak', outcome: 'PromotedOnTrial', targetArmId: 'arm-3a', reason: 'Strong Third Term after illness.' },
      ],
    });
  });

  it('shows the blockers and keeps the commit disabled', async () => {
    mockMe(['promotion.run']);
    const message = 'Primary 3 has no arm in 2027/2028. Create at least one arm before running promotion.';
    server.use(
      http.get(apiUrl(`/api/v1/sessions/${SOURCE}/promotion/preview`), () =>
        HttpResponse.json(preview({ blockers: [{ code: 'promotion.receiving_level_without_arm', message }], canDecide: false })),
      ),
    );
    renderScreen();

    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Commit promotion' })).toBeDisabled();
    expect(screen.getByText(/1 pupil is no longer active/)).toBeInTheDocument();
  });

  it('without promotion.decide a proposal cannot be changed', async () => {
    mockMe(['promotion.run']);
    server.use(http.get(apiUrl(`/api/v1/sessions/${SOURCE}/promotion/preview`), () => HttpResponse.json(preview({ canDecide: false }))));
    renderScreen();

    expect(await screen.findByRole('combobox', { name: 'Outcome for Okafor Ada' })).toBeDisabled();
  });

  it('a committed batch offers a Super Admin reversal and surfaces a refusal verbatim', async () => {
    mockMe(['promotion.run', 'promotion.reverse'], true);
    const batch = {
      id: 'batch-1', sourceSessionId: SOURCE, targetSessionId: TARGET, targetSessionName: '2027/2028', state: 'Committed',
      committedAtUtc: '2027-07-28T10:15:00+00:00', reversedAtUtc: null, promoted: 180, repeated: 9, promotedOnTrial: 3, graduated: 41,
    } as const;
    server.use(
      http.get(apiUrl(`/api/v1/sessions/${SOURCE}/promotion/preview`), () =>
        HttpResponse.json(preview({ rows: [], committedBatch: batch, blockers: [{ code: 'promotion.already_run', message: 'Already run.' }] })),
      ),
    );
    const refusal = 'Marks have already been entered in 2027/2028. This promotion cannot be reversed. Move individual pupils between arms instead.';
    server.use(http.post(apiUrl('/api/v1/promotion-batches/batch-1/reverse'), () => problemResponse(409, { detail: refusal })));
    const user = userEvent.setup();
    renderScreen();

    expect(await screen.findByText('Promotion committed into 2027/2028')).toBeInTheDocument();
    expect(screen.getByText('180')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Reverse promotion' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByRole('textbox', { name: 'Reason' }), 'Committed into the wrong session.');
    await user.click(within(dialog).getByRole('button', { name: 'Reverse promotion' }));

    expect(await within(dialog).findByText(refusal)).toBeInTheDocument();
  });
});
