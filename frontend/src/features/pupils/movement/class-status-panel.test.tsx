import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ClassStatusPanel } from './class-status-panel';

function history(changedAtUtc: string) {
  return {
    pupilId: 'pupil-1',
    status: 'Withdrawn',
    currentArmId: null,
    currentArmName: null,
    enrolments: [
      { armId: 'arm-a', armName: 'Primary 2A', sessionId: 's-1', sessionName: '2026/2027', effectiveFrom: '2026-09-08', effectiveTo: '2026-10-01' },
    ],
    statusChanges: [
      { fromStatus: 'Active', toStatus: 'Withdrawn', effectiveDate: '2026-10-01', reason: 'Left.', armName: null, changedAtUtc },
    ],
  };
}

function renderPanel() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <ClassStatusPanel pupilId="pupil-1" status="Withdrawn" declined={false} canTransfer canChangeStatus />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ClassStatusPanel — same-day undo', () => {
  it('offers undo for a leave recorded today and posts it once confirmed', async () => {
    let undone = false;
    server.use(
      http.get(apiUrl('/api/v1/pupils/:id/enrolments'), () => HttpResponse.json(history(new Date().toISOString()))),
      http.post(apiUrl('/api/v1/pupils/:id/status/undo'), () => {
        undone = true;
        return HttpResponse.json({ dryRun: false, pupil: { id: 'pupil-1', status: 'Active' }, resultSets: [], capacity: null });
      }),
    );
    const user = userEvent.setup();
    renderPanel();

    await user.click(await screen.findByRole('button', { name: 'Undo this change' }));
    await user.click(screen.getByRole('button', { name: 'Confirm undo' }));

    await waitFor(() => expect(undone).toBe(true));
  });

  it('does not offer undo for a change recorded on an earlier day', async () => {
    server.use(
      http.get(apiUrl('/api/v1/pupils/:id/enrolments'), () =>
        HttpResponse.json(history(new Date(Date.now() - 3 * 24 * 3_600_000).toISOString())),
      ),
    );
    renderPanel();

    expect(await screen.findByText(/Active to Withdrawn/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Undo this change' })).toBeNull();
  });
});
