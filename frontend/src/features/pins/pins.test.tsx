import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { GenerateBatchDialog } from './components/generate-batch-dialog';
import { PinBatchScreen } from './pin-batch-screen';

const BATCH = {
  id: 'b-1', sessionId: 's-1', name: '2026/2027 First Term batch 1', purposeNote: null, pinLength: 10, maxUses: 3, pinCount: 2,
  pinsUsed: 1, pinsExhausted: 0, pinsSuspended: 0, pinsRevoked: 0, state: 'Generated', generatedAt: '2026-09-22T09:00:00Z',
  plaintextPurgeAt: '2099-10-22T09:00:00Z', revokeReason: null,
};
const PINS = [
  { id: 'pin-1', prefix: 'H7K2', state: 'Active', useCount: 1, maxUses: 3, distinctPupilCount: 1, stateReason: null },
  { id: 'pin-2', prefix: 'M3PQ', state: 'Unused', useCount: 0, maxUses: 3, distinctPupilCount: 0, stateReason: null },
];

function renderBatch() {
  server.use(http.get(apiUrl('/api/v1/pin-batches/:batchId'), () => HttpResponse.json({ batch: BATCH, pins: PINS })));
  return renderWithProviders(
    <MemoryRouter initialEntries={['/pins/b-1']}>
      <Routes>
        <Route path="/pins/:id" element={<PinBatchScreen />} />
      </Routes>
    </MemoryRouter>,
  );
}

afterEach(() => vi.restoreAllMocks());

describe('GenerateBatchDialog', () => {
  it('asks for the number of uses twice above ten, then sends it as the confirmation', async () => {
    let body: Record<string, unknown> | undefined;
    server.use(
      http.post(apiUrl('/api/v1/pin-batches'), async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(BATCH, { status: 201 });
      }),
    );

    const { user } = renderWithProviders(<GenerateBatchDialog sessionId="s-1" onClose={() => {}} onCreated={() => {}} />);
    await user.clear(screen.getByLabelText('Uses per pin'));
    await user.type(screen.getByLabelText('Uses per pin'), '12');
    await user.click(screen.getByRole('button', { name: 'Generate' }));
    expect(await screen.findByText('Type the number of uses again to confirm.')).toBeInTheDocument();
    expect(body).toBeUndefined();

    await user.type(screen.getByLabelText('Type 12 again to confirm'), '12');
    await user.click(screen.getByRole('button', { name: 'Generate' }));
    await waitFor(() => expect(body).toMatchObject({ sessionId: 's-1', maxUses: 12, confirmMaxUses: 12, pinCount: 100, pinLength: 10 }));
  });
});

describe('PinBatchScreen', () => {
  it('prints the slips as a downloaded PDF', async () => {
    mockMe('pin.view', 'pin.print');
    const createObjectURL = vi.fn(() => 'blob:slips');
    Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    server.use(
      http.get(apiUrl('/api/v1/pin-batches/:batchId/print'), () =>
        new HttpResponse(new TextEncoder().encode('%PDF-1.7'), { headers: { 'Content-Type': 'application/pdf' } }),
      ),
    );

    const { user } = renderBatch();
    await user.click(await screen.findByRole('button', { name: 'Print slips (PDF)' }));

    await waitFor(() => expect(click).toHaveBeenCalled());
    expect(createObjectURL).toHaveBeenCalled();
  });

  it('shows the server refusal for a purged batch, read out of the binary error body', async () => {
    mockMe('pin.view', 'pin.print');
    server.use(
      http.get(apiUrl('/api/v1/pin-batches/:batchId/print'), () =>
        problemResponse(410, { detail: 'These pins can no longer be printed.', errorCode: 'pin_batch.plaintext_purged' }),
      ),
    );

    const { user } = renderBatch();
    await user.click(await screen.findByRole('button', { name: 'Print slips (PDF)' }));

    expect(await screen.findByText('These pins can no longer be printed.')).toBeInTheDocument();
  });

  it('revokes one pin with a reason', async () => {
    mockMe('pin.view', 'pin.revoke');
    let reason: string | undefined;
    server.use(
      http.post(apiUrl('/api/v1/pins/:pinId/revoke'), async ({ request }) => {
        reason = ((await request.json()) as { reason: string }).reason;
        return HttpResponse.json({ ...PINS[1], state: 'Revoked', stateReason: reason });
      }),
    );

    const { user } = renderBatch();
    await user.click(await screen.findByRole('button', { name: 'Revoke pin M3PQ' }));
    await user.type(screen.getByLabelText('Reason'), 'Slip lost');
    await user.click(screen.getByRole('button', { name: 'Revoke' }));

    await waitFor(() => expect(reason).toBe('Slip lost'));
  });
});
