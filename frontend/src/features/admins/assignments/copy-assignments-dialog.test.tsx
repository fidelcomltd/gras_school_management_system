import { describe, expect, it, vi } from 'vitest';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { CopyAssignmentsDialog } from './copy-assignments-dialog';

const row = {
  sourceAssignmentId: 'as-1', adminAccountId: 'admin-1', staffName: 'Ngozi Adeyemi', roleName: 'Class Teacher',
  scopeType: 'ArmList', armNames: ['Primary 2A'], skipReason: null,
};

describe('CopyAssignmentsDialog', () => {
  it('previews what will be copied and skipped, then copies on confirmation', async () => {
    const sent: { dryRun: boolean; fromSessionId: string; toSessionId: string }[] = [];
    server.use(
      http.get(apiUrl('/api/v1/sessions'), () =>
        HttpResponse.json({
          items: [
            { id: 's-now', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active' },
            { id: 's-next', name: '2027/2028', startDate: '2027-09-13', endDate: '2028-07-24', state: 'Upcoming' },
          ],
          nextCursor: null,
        }),
      ),
      http.post(apiUrl('/api/v1/assignments/copy-to-session'), async ({ request }) => {
        const body = (await request.json()) as { dryRun: boolean; fromSessionId: string; toSessionId: string };
        sent.push(body);
        return HttpResponse.json({
          dryRun: body.dryRun,
          fromSessionName: '2026/2027',
          toSessionName: '2027/2028',
          copied: [row],
          skipped: [{ ...row, sourceAssignmentId: 'as-2', staffName: 'Emeka Obi', armNames: ['Primary 5B'], skipReason: 'Primary 5B has no class in 2027/2028.' }],
        });
      }),
    );
    const onClose = vi.fn();

    const { user } = renderWithProviders(<CopyAssignmentsDialog onClose={onClose} />);

    await user.click(await screen.findByRole('button', { name: 'Preview' }));
    expect(await screen.findByText('1 will be created in 2027/2028.')).toBeInTheDocument();
    expect(screen.getByText(/Primary 5B has no class in 2027\/2028/)).toBeInTheDocument();
    expect(sent[0]).toEqual({ fromSessionId: 's-now', toSessionId: 's-next', dryRun: true });

    await user.click(screen.getByRole('button', { name: 'Copy 1 assignment' }));
    expect(await screen.findByRole('status')).toHaveTextContent('Copied 1 assignment into 2027/2028; 1 skipped.');
    await waitFor(() => expect(sent[1]).toEqual({ fromSessionId: 's-now', toSessionId: 's-next', dryRun: false }));
  });
});
