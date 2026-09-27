import { MemoryRouter, Route, Routes } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { PupilDetailScreen } from './pupil-detail-screen';
import { PupilsListScreen } from './pupils-list-screen';
import { pupil } from './test-fixtures';

function renderDetail() {
  return renderWithProviders(
    <MemoryRouter initialEntries={['/pupils/pupil-1']}>
      <Routes>
        <Route path="/pupils/:id" element={<PupilDetailScreen />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:slip'), revokeObjectURL: vi.fn() });
});

describe('register extras', () => {
  it("shows each row's completeness on the pupil list", async () => {
    mockMe('pupil.view');
    server.use(
      http.get(apiUrl('/api/v1/pupils'), () =>
        HttpResponse.json({ items: [pupil({ status: 'Active', registrationNumber: 'GRA/2026/0014', chasedPercent: 45 })], nextCursor: null }),
      ),
    );

    renderWithProviders(
      <MemoryRouter>
        <PupilsListScreen />
      </MemoryRouter>,
    );

    expect(await screen.findByText('45% complete')).toBeInTheDocument();
  });

  it('prints the admission slip from the record of an admitted pupil', async () => {
    mockMe('pupil.view');
    let slips = 0;
    server.use(
      http.get(apiUrl('/api/v1/pupils/:id'), () => HttpResponse.json(pupil({ status: 'Active', registrationNumber: 'GRA/2026/0014' }))),
      http.get(apiUrl('/api/v1/pupils/:pupilId/admission-slip'), () => {
        slips += 1;
        return new HttpResponse(new Uint8Array([37, 80, 68, 70]), { headers: { 'Content-Type': 'application/pdf' } });
      }),
    );

    const { user } = renderDetail();
    await user.click(await screen.findByRole('button', { name: 'Print admission slip' }));

    await waitFor(() => expect(slips).toBe(1));
  });

  it('warns on a pending admission whose contact phone another record shares', async () => {
    mockMe('pupil.view', 'contact.view', 'contact.update');
    server.use(
      // Before /pupils/:id, which would otherwise match /pupils/duplicates too.
      http.get(apiUrl('/api/v1/pupils/duplicates'), ({ request }) =>
        new URL(request.url).searchParams.get('contactPhone') === '+2348031234567'
          ? HttpResponse.json([pupil({ id: 'pupil-9', firstName: 'Emeka', status: 'Active', registrationNumber: 'GRA/2024/0003' })])
          : HttpResponse.json([]),
      ),
      http.get(apiUrl('/api/v1/pupils/:id'), () => HttpResponse.json(pupil())),
      http.get(apiUrl('/api/v1/admissions/:id/completeness'), () =>
        HttpResponse.json({ pupilId: 'pupil-1', blocking: [], chased: [], chasedPercent: 100 }),
      ),
      http.get(apiUrl('/api/v1/pupils/:pupilId/contacts'), () =>
        HttpResponse.json({
          pupilId: 'pupil-1',
          items: [
            { role: 'Father', fullName: 'Bayo Okafor', relationship: null, phone: '+2348031234567', whatsappNumber: null, occupation: null, email: null, isPrimaryContact: true },
          ],
        }),
      ),
    );

    const { user } = renderDetail();
    await user.click(await screen.findByRole('tab', { name: 'Contacts' }));

    expect(await screen.findByText(/shares a contact phone/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'OKAFOR Emeka' })).toHaveAttribute('href', '/pupils/pupil-9');
  });
});
