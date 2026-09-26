import { MemoryRouter, Route, Routes } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { PupilDetailScreen } from '../pupil-detail-screen';
import { pupil } from '../test-fixtures';

const TYPES = ['BirthCertificate', 'PassportPhotograph', 'PreviousSchoolResult', 'TransferLetter', 'Other'] as const;

function documents(file: { contentType: string; sizeBytes: number; uploadedAtUtc: string } | null) {
  return {
    pupilId: 'pupil-1',
    items: TYPES.map((documentType) =>
      documentType === 'BirthCertificate' && file
        ? { documentType, otherLabel: null, received: true, receivedDate: '2026-09-26', remarks: null, file }
        : { documentType, otherLabel: null, received: false, receivedDate: null, remarks: null, file: null },
    ),
  };
}

function mockPupil(photoUpdatedAtUtc: () => string | null) {
  server.use(
    http.get(apiUrl('/api/v1/pupils/:id'), () => HttpResponse.json(pupil({ photoUpdatedAtUtc: photoUpdatedAtUtc() }))),
    http.get(apiUrl('/api/v1/admissions/:id/completeness'), () =>
      HttpResponse.json({ pupilId: 'pupil-1', blocking: [], chased: [], chasedPercent: 100 }),
    ),
    http.get(apiUrl('/api/v1/pupils/:pupilId/photo'), () => new HttpResponse(new Uint8Array([255, 216, 255]), { headers: { 'Content-Type': 'image/jpeg' } })),
  );
}

function renderScreen() {
  return renderWithProviders(
    <MemoryRouter initialEntries={['/pupils/pupil-1']}>
      <Routes>
        <Route path="/pupils/:id" element={<PupilDetailScreen />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:photo'), revokeObjectURL: vi.fn() });
});

describe('pupil photograph', () => {
  it('uploads the chosen file as multipart with an Idempotency-Key, then shows the photograph', async () => {
    mockMe('pupil.view', 'pupil.photo.update');
    let uploadedAt: string | null = null;
    let received: { key: string | null; file: FormDataEntryValue | null } | undefined;
    mockPupil(() => uploadedAt);
    server.use(
      http.post(apiUrl('/api/v1/pupils/:pupilId/photo'), async ({ request }) => {
        received = { key: request.headers.get('Idempotency-Key'), file: (await request.formData()).get('file') };
        uploadedAt = '2026-09-26T10:00:00+00:00';
        return HttpResponse.json({ pupilId: 'pupil-1', updatedAtUtc: uploadedAt, photoUrl: '/p', thumbnailUrl: '/t' });
      }),
    );

    const { user } = renderScreen();
    expect(await screen.findByText('No photograph')).toBeInTheDocument();
    await user.upload(screen.getByLabelText(/Upload photograph/), new File(['jpeg'], 'child.jpg', { type: 'image/jpeg' }));

    expect(await screen.findByRole('img', { name: /Photograph of/ })).toHaveAttribute('src', 'blob:photo');
    expect(received?.key).toBeTruthy();
    expect((received?.file as Blob | null)?.type).toBe('image/jpeg'); // a File from the test realm, so not instanceof here
  });

  it("shows the server's reason when a photograph is refused", async () => {
    mockMe('pupil.view', 'pupil.photo.update');
    mockPupil(() => null);
    const reason = 'This photograph is 8 MB. The limit is 3 MB. Reduce the size or take the photograph again at a lower quality.';
    server.use(
      http.post(apiUrl('/api/v1/pupils/:pupilId/photo'), () => problemResponse(422, { detail: reason, errorCode: 'pupil_photo.too_large' })),
    );

    const { user } = renderScreen();
    await user.upload(await screen.findByLabelText(/Upload photograph/), new File(['big'], 'big.jpg', { type: 'image/jpeg' }));

    expect(await screen.findByText(reason)).toBeInTheDocument();
  });

  it('removes the photograph only after confirming', async () => {
    mockMe('pupil.view', 'pupil.photo.update');
    let uploadedAt: string | null = '2026-09-20T10:00:00+00:00';
    let deleted = 0;
    mockPupil(() => uploadedAt);
    server.use(
      http.delete(apiUrl('/api/v1/pupils/:pupilId/photo'), () => {
        deleted += 1;
        uploadedAt = null;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const { user } = renderScreen();
    await user.click(await screen.findByRole('button', { name: 'Remove photograph' }));
    expect(deleted).toBe(0);
    await user.click(screen.getByRole('button', { name: 'Remove' }));

    await waitFor(() => expect(deleted).toBe(1));
    expect(await screen.findByText('No photograph')).toBeInTheDocument();
  });

  it('offers no upload to staff without pupil.photo.update', async () => {
    mockMe('pupil.view');
    mockPupil(() => null);

    renderScreen();
    expect(await screen.findByText('No photograph')).toBeInTheDocument();
    expect(screen.queryByLabelText(/Upload photograph/)).not.toBeInTheDocument();
  });
});

describe('document scans', () => {
  it('attaches a scan, which ticks the row, and removing it keeps the tick', async () => {
    mockMe('pupil.view', 'pupil.document.manage');
    mockPupil(() => null);
    const file = { contentType: 'application/pdf', sizeBytes: 2048, uploadedAtUtc: '2026-09-26T10:00:00+00:00' };
    server.use(
      http.get(apiUrl('/api/v1/pupils/:pupilId/documents'), () => HttpResponse.json(documents(null))),
      http.post(apiUrl('/api/v1/pupils/:pupilId/documents/:documentType/file'), () => HttpResponse.json(documents(file))),
      http.delete(apiUrl('/api/v1/pupils/:pupilId/documents/:documentType/file'), () => {
        const list = documents(file);
        const row = list.items[0] as { file: unknown };
        row.file = null;
        return HttpResponse.json(list);
      }),
    );

    const { user } = renderScreen();
    await user.click(await screen.findByRole('tab', { name: 'Documents' }));
    await user.upload(await screen.findByLabelText('Scan of Birth certificate'), new File(['%PDF-'], 'cert.pdf', { type: 'application/pdf' }));

    expect(await screen.findByText('Scan attached (PDF, 2 KB)')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Birth certificate' })).toBeChecked();

    await user.click(screen.getByRole('button', { name: 'Remove scan' }));

    await waitFor(() => expect(screen.queryByText('Scan attached (PDF, 2 KB)')).not.toBeInTheDocument());
    expect(screen.getByRole('checkbox', { name: 'Birth certificate' })).toBeChecked();
  });
});
