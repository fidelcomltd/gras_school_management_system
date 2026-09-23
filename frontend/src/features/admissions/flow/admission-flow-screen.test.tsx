import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { admissionRecord, pupil } from '../components/test-fixtures';
import { AdmissionFlowScreen } from './admission-flow-screen';

const HEALTH = { step: 5, code: 'health.unanswered', message: 'Answer all three health questions: allergy, medical condition, medication.' };
const CONTACTS = { step: 3, code: 'contacts.responsible_adult', message: 'Add the father, mother or guardian.' };

function mockAdmission(blocking: { step: number; code: string; message: string }[]) {
  server.use(
    http.get(apiUrl('/api/v1/pupils/:id'), () => HttpResponse.json(pupil())),
    http.get(apiUrl('/api/v1/admissions/:id'), () => HttpResponse.json(admissionRecord({ declarationSigned: false, declarationName: null, declarationDate: null }))),
    http.get(apiUrl('/api/v1/admissions/:id/completeness'), () => HttpResponse.json({ pupilId: 'pupil-1', blocking, chased: [], chasedPercent: 50 })),
    http.get(apiUrl('/api/v1/pupils/:pupilId/contacts'), () => HttpResponse.json({ pupilId: 'pupil-1', items: [] })),
    http.get(apiUrl('/api/v1/arms'), () => HttpResponse.json({ items: [{ id: 'arm-1', displayName: 'Primary 2C' }], nextCursor: null })),
  );
}

function renderFlow(entry: string) {
  return renderWithProviders(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route path="/admissions/:id" element={<AdmissionFlowScreen />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('AdmissionFlowScreen', () => {
  it('resumes at the first step still blocking approval', async () => {
    mockMe('pupil.view', 'contact.view', 'contact.update');
    mockAdmission([CONTACTS, HEALTH]);

    renderFlow('/admissions/pupil-1');

    expect(await screen.findByRole('heading', { name: 'Step 3: Parents and contacts' })).toBeInTheDocument();
    expect(await screen.findByLabelText('Father: full name')).toBeInTheDocument();
  });

  it('saves only section I on the declaration step, then opens the review', async () => {
    mockMe('pupil.view', 'pupil.update');
    mockAdmission([]);
    let sent: Record<string, unknown> | undefined;
    server.use(
      http.patch(apiUrl('/api/v1/admissions/:id'), async ({ request }) => {
        sent = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(admissionRecord());
      }),
    );

    const { user } = renderFlow('/admissions/pupil-1?step=8');
    await user.type(await screen.findByLabelText('Name of the parent or guardian making the declaration'), 'Chinwe Okafor');
    await user.click(screen.getByRole('checkbox', { name: 'The declaration on the paper form has been signed' }));
    await user.click(screen.getByRole('button', { name: 'Save and continue' }));

    await waitFor(() => expect(sent).toBeDefined());
    expect(sent).toMatchObject({ declarationName: 'Chinwe Okafor', declarationSigned: true, dateAdmitted: null, classAdmittedInto: null });
    expect(sent?.['declarationDate']).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(await screen.findByRole('heading', { name: 'Step 9: Review and approve' })).toBeInTheDocument();
  });

  it('shows the declaration read-only to staff who cannot update the record', async () => {
    mockMe('pupil.view');
    mockAdmission([]);

    renderFlow('/admissions/pupil-1?step=8');

    expect(await screen.findByLabelText('Name of the parent or guardian making the declaration')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Save and continue' })).not.toBeInTheDocument();
  });

  it('lets a holder of the override approve with a reason when only the health answers are missing', async () => {
    mockMe('pupil.view', 'pupil.admission.approve', 'pupil.admission.override');
    mockAdmission([HEALTH]);
    let body: Record<string, unknown> | undefined;
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/approve'), async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ ...pupil(), status: 'Active', registrationNumber: 'GRAS/2026/0042' });
      }),
    );

    const { user } = renderFlow('/admissions/pupil-1?step=9');
    await user.click(await screen.findByRole('combobox', { name: 'Arm' }));
    await user.click(await screen.findByRole('option', { name: 'Primary 2C' }));
    await user.type(screen.getByLabelText('Reason for approving without the health answers'), 'Parent declined at the counter.');
    await user.click(screen.getByRole('checkbox'));
    await user.click(screen.getByRole('button', { name: 'Approve' }));

    expect(await screen.findByText('GRAS/2026/0042')).toBeInTheDocument();
    expect(body).toMatchObject({ armId: 'arm-1', healthOverrideReason: 'Parent declined at the counter.' });
  });

  it('keeps approval closed when something other than the health answers is missing, even for the override holder', async () => {
    mockMe('pupil.view', 'pupil.admission.approve', 'pupil.admission.override');
    mockAdmission([CONTACTS, HEALTH]);

    renderFlow('/admissions/pupil-1?step=9');

    expect(await screen.findByText('Approval opens here once the steps marked above are complete.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });
});
