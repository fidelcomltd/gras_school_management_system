import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { TransferDialog } from './transfer-dialog';

const ARMS = [
  { id: 'arm-a', displayName: 'Primary 2A' },
  { id: 'arm-c', displayName: 'Primary 2C' },
];

function outcome(dryRun: boolean, effect: 'RevertsToDraft' | 'Blocks') {
  return {
    dryRun,
    pupil: { id: 'pupil-1', status: 'Active' },
    fromStatus: 'Active',
    toStatus: 'Active',
    fromArmId: 'arm-a',
    fromArmName: 'Primary 2A',
    toArmId: 'arm-c',
    toArmName: 'Primary 2C',
    effectiveDate: '2026-10-14',
    enrolmentClosesOn: '2026-10-13',
    resultSets: [
      {
        resultSetId: 'rs-1',
        armId: 'arm-a',
        armName: 'Primary 2A',
        termId: 'term-1',
        termName: 'First Term',
        state: effect === 'Blocks' ? 'Published' : 'AwaitingApproval',
        effect,
      },
    ],
    capacity: { capacity: '22', enrolledAfter: '20', overCapacity: false, canOverride: false },
  };
}

function renderDialog(onClose: () => void = () => {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  server.use(http.get(apiUrl('/api/v1/arms'), () => HttpResponse.json({ items: ARMS, nextCursor: null })));
  return render(
    <QueryClientProvider client={queryClient}>
      <TransferDialog pupilId="pupil-1" currentArmId="arm-a" sessionId="session-1" onClose={onClose} />
    </QueryClientProvider>,
  );
}

async function chooseArm(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('combobox', { name: 'New class' }));
  // The pupil's own class is never offered.
  await screen.findByRole('option', { name: 'Primary 2C' });
  expect(screen.queryByRole('option', { name: 'Primary 2A' })).toBeNull();
  await user.click(screen.getByRole('option', { name: 'Primary 2C' }));
}

describe('TransferDialog', () => {
  it('previews the consequences with a dry run, then commits with a different key', async () => {
    const calls: { dryRun: boolean; key: string | null }[] = [];
    server.use(
      http.post(apiUrl('/api/v1/pupils/:id/transfer'), async ({ request }) => {
        const body = (await request.json()) as { dryRun: boolean };
        calls.push({ dryRun: body.dryRun, key: request.headers.get('Idempotency-Key') });
        return HttpResponse.json(outcome(body.dryRun, 'RevertsToDraft'));
      }),
    );
    let closed = false;
    const user = userEvent.setup();
    renderDialog(() => {
      closed = true;
    });

    await chooseArm(user);
    await user.click(screen.getByRole('button', { name: 'Check what changes' }));

    expect(await screen.findByText(/Primary 2A First Term results: goes back to Draft/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Move pupil' }));

    await waitFor(() => expect(closed).toBe(true));
    expect(calls.map((call) => call.dryRun)).toEqual([true, false]);
    expect(calls[0]?.key).not.toBe(calls[1]?.key);
  });

  it('will not commit when published results block the move', async () => {
    server.use(http.post(apiUrl('/api/v1/pupils/:id/transfer'), () => HttpResponse.json(outcome(true, 'Blocks'))));
    const user = userEvent.setup();
    renderDialog();

    await chooseArm(user);
    await user.click(screen.getByRole('button', { name: 'Check what changes' }));

    expect(await screen.findByText(/This move is blocked/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Move pupil' })).toBeDisabled();
  });
});
