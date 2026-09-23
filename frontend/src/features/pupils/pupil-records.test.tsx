import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { PupilDetailScreen } from './pupil-detail-screen';
import { pupil } from './test-fixtures';

const BLOCKING = [
  { step: 3, code: 'contacts.responsible_adult', message: 'Add the father, mother or guardian.' },
  { step: 5, code: 'health.unanswered', message: 'Answer all three health questions: allergy, medical condition, medication.' },
];

function mockPendingPupil() {
  server.use(
    http.get(apiUrl('/api/v1/pupils/:id'), () => HttpResponse.json(pupil())),
    http.get(apiUrl('/api/v1/admissions/:id/completeness'), () =>
      HttpResponse.json({ pupilId: 'pupil-1', blocking: BLOCKING, chased: [], chasedPercent: 40 }),
    ),
    http.get(apiUrl('/api/v1/pupils/:pupilId/contacts'), () => HttpResponse.json({ pupilId: 'pupil-1', items: [] })),
    http.get(apiUrl('/api/v1/pupils/:pupilId/health'), () =>
      HttpResponse.json({
        pupilId: 'pupil-1', hasAllergy: null, allergyDetails: null, hasMedicalCondition: null, medicalConditionDetails: null,
        takesRegularMedication: null, medicationDetails: null, specialInstructions: null, preferredHospital: null, hospitalPhone: null,
        bloodGroup: null, genotype: null,
      }),
    ),
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

describe('PupilDetailScreen admission sections', () => {
  it('lists what blocks approval, and Go opens the step that fixes it', async () => {
    mockMe('pupil.view', 'contact.view', 'contact.update', 'pupil.safeguarding.view', 'pupil.safeguarding.update');
    mockPendingPupil();

    const { user } = renderScreen();
    expect(await screen.findByText('Before this admission can be approved')).toBeInTheDocument();
    expect(screen.getByText('Add the father, mother or guardian.')).toBeInTheDocument();

    await user.click(screen.getAllByRole('button', { name: 'Go' })[1] as HTMLElement);

    expect(await screen.findByRole('tab', { name: 'Health', selected: true })).toBeInTheDocument();
    expect(await screen.findByText('Does the child have any allergy?')).toBeInTheDocument();
    expect(screen.getAllByText('Not answered yet')).toHaveLength(3);
  });

  it('saves contacts with the first parent as primary, and copies the father into the emergency slot', async () => {
    mockMe('pupil.view', 'contact.view', 'contact.update');
    mockPendingPupil();
    let saved: { contacts: { role: string; fullName: string; phone: string; relationship: string | null; isPrimaryContact: boolean }[] } | undefined;
    server.use(
      http.put(apiUrl('/api/v1/pupils/:pupilId/contacts'), async ({ request }) => {
        saved = (await request.json()) as typeof saved;
        return HttpResponse.json({ pupilId: 'pupil-1', items: [] });
      }),
    );

    const { user } = renderScreen();
    await user.click(await screen.findByRole('tab', { name: 'Contacts' }));
    await user.type(await screen.findByLabelText('Father: full name'), 'Emeka Okafor');
    await user.type(screen.getByLabelText('Father: phone'), '08031234567');
    await user.click(screen.getByRole('button', { name: 'Copy from father' }));
    await user.click(screen.getByRole('button', { name: 'Save contacts' }));

    await waitFor(() => expect(saved?.contacts).toHaveLength(2));
    expect(saved?.contacts[0]).toMatchObject({ role: 'Father', fullName: 'Emeka Okafor', phone: '08031234567', isPrimaryContact: true });
    expect(saved?.contacts[1]).toMatchObject({ role: 'EmergencyPrimary', fullName: 'Emeka Okafor', relationship: 'Father', isPrimaryContact: false });
  });

  it("shows a rejected contact's own reason, not the generic validation message", async () => {
    mockMe('pupil.view', 'contact.view', 'contact.update');
    mockPendingPupil();
    server.use(
      http.put(apiUrl('/api/v1/pupils/:pupilId/contacts'), () =>
        HttpResponse.json(
          {
            type: 'about:blank', title: 'One or more validation errors occurred.', status: 422, errorCode: 'validation', traceId: 't',
            errors: { 'Contacts[0]': ['Enter a Nigerian phone number, for example 08031234567.'] },
          },
          { status: 422, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    const { user } = renderScreen();
    await user.click(await screen.findByRole('tab', { name: 'Contacts' }));
    await user.type(await screen.findByLabelText('Father: full name'), 'Emeka Okafor');
    await user.type(screen.getByLabelText('Father: phone'), '0803');
    await user.click(screen.getByRole('button', { name: 'Save contacts' }));

    expect(await screen.findByText('Enter a Nigerian phone number, for example 08031234567.')).toBeInTheDocument();
  });

  it('hides the Health tab from staff without the safeguarding privilege', async () => {
    mockMe('pupil.view', 'contact.view');
    mockPendingPupil();

    renderScreen();
    expect(await screen.findByRole('tab', { name: 'Contacts' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Health' })).not.toBeInTheDocument();
  });
});
