import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ProgressScreen } from './progress-screen';

function readiness(state: string, canSubmit = false) {
  return {
    armId: 'arm-1',
    termId: 't-1',
    resultSet: { id: 'rs-1', state, needsRecompute: false, returnReason: null },
    subjects: [{ subjectId: 'maths', name: 'Mathematics' }],
    componentCount: 2,
    pupils: [
      {
        pupilId: 'p-1', registrationNumber: 'GRAS/2026/0041', displayName: 'Okafor Chidera',
        marks: [{ subjectId: 'maths', status: 'Partial', filledParts: 1 }],
        ratingsComplete: true, attendanceComplete: false, classTeacherRemarkPresent: true, headTeacherRemarkPresent: false,
      },
    ],
    leftDuringTerm: [],
    counters: {
      marks: { complete: 0, total: 1 }, ratings: { complete: 1, total: 1 }, attendance: { complete: 0, total: 1 },
      classTeacherRemarks: { complete: 1, total: 1 }, headTeacherRemarks: { complete: 0, total: 1 },
    },
    blockers: canSubmit ? [] : [{ code: 'marks_incomplete', message: '1 of 1 mark cells are still missing.' }],
    canSubmit,
  };
}

function mockClass(state: string, canSubmit = false) {
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
    http.get(apiUrl('/api/v1/arms/:armId/readiness'), () => HttpResponse.json(readiness(state, canSubmit))),
  );
}

describe('ProgressScreen', () => {
  it('shows the counters and the blocker, and keeps Submit disabled while blocked', async () => {
    mockMe('result.view', 'result.compute', 'result.submit');
    mockClass('Draft');

    renderWithProviders(<ProgressScreen />);

    expect(await screen.findByText('1 of 1 mark cells are still missing.')).toBeInTheDocument();
    expect(screen.getByText('Draft')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Compute results' })).toBeEnabled();
    expect(screen.getByRole('row', { name: /Okafor Chidera/ })).toHaveTextContent('part');
  });

  it('publishes an approved set with an Idempotency-Key', async () => {
    mockMe('result.view', 'result.publish', 'result.return');
    mockClass('Approved', true);
    let key: string | null = null;
    server.use(
      http.post(apiUrl('/api/v1/result-sets/:id/publish'), ({ request }) => {
        key = request.headers.get('Idempotency-Key');
        return HttpResponse.json({ resultSet: { id: 'rs-1', state: 'Published', needsRecompute: false, returnReason: null }, publishedAt: '2026-12-18T10:00:00Z', revisionNumber: 1 });
      }),
    );

    const { user } = renderWithProviders(<ProgressScreen />);
    await user.click(await screen.findByRole('button', { name: 'Publish to parents' }));

    await waitFor(() => expect(key).toMatch(/^[0-9a-f-]{36}$/));
    expect(screen.queryByRole('button', { name: 'Withdraw from parents' })).not.toBeInTheDocument();
  });

  it('returns for correction with a reason of at least ten characters', async () => {
    mockMe('result.view', 'result.approve', 'result.return');
    mockClass('AwaitingApproval', true);
    let reason: string | undefined;
    server.use(
      http.post(apiUrl('/api/v1/result-sets/:id/return'), async ({ request }) => {
        reason = ((await request.json()) as { reason: string }).reason;
        return HttpResponse.json({ resultSet: { id: 'rs-1', state: 'ReturnedForCorrection', needsRecompute: false, returnReason: reason } });
      }),
    );

    const { user } = renderWithProviders(<ProgressScreen />);
    await user.click(await screen.findByRole('button', { name: 'Return for correction' }));
    await user.type(screen.getByLabelText('Reason (10 to 500 characters)'), 'too short');
    expect(screen.getByRole('button', { name: 'Return' })).toBeDisabled();
    await user.type(screen.getByLabelText('Reason (10 to 500 characters)'), ' — check the maths exam marks.');
    await user.click(screen.getByRole('button', { name: 'Return' }));

    await waitFor(() => expect(reason).toBe('too short — check the maths exam marks.'));
  }, 15_000);
});
