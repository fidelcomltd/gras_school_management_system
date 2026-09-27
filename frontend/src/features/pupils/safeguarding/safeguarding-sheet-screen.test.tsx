import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { SafeguardingSheetScreen } from './safeguarding-sheet-screen';

function mockClass() {
  server.use(
    http.get(apiUrl('/api/v1/sessions'), () =>
      HttpResponse.json({ items: [{ id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active' }], nextCursor: null }),
    ),
    http.get(apiUrl('/api/v1/sessions/:id'), () =>
      HttpResponse.json({
        id: 's-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Active', armCount: 1,
        terms: [{ id: 't-1', sessionId: 's-1', ordinal: 1, name: 'First Term', startDate: '2026-09-14', endDate: '2026-12-18', nextResumptionDate: null, timesSchoolOpened: null, state: 'Active', closedAtUtc: null, closedBy: null }],
      }),
    ),
    http.get(apiUrl('/api/v1/arms'), () =>
      HttpResponse.json({ items: [{ id: 'arm-1', displayName: 'Primary 4A', label: 'A', classLevelId: 'p4', classLevel: 'Primary 4', sessionId: 's-1', capacity: null, formTeacherAdminId: null, status: 'Active' }], nextCursor: null }),
    ),
  );
}

const SHEET = {
  armId: 'arm-1',
  armName: 'Primary 4A',
  sessionName: '2026/2027',
  generatedAtUtc: '2026-10-05T07:45:00+00:00',
  pupils: [
    {
      pupilId: 'p-1', registrationNumber: 'GRAS/2026/0041', name: 'OKAFOR Chidera', photoUpdatedAtUtc: null, allergies: 'Groundnuts',
      medicalConditions: 'None', medication: 'Not asked', specialInstructions: '', hospital: 'St. Charles Borromeo, 08037776666',
      pickupPersons: ['Ngozi Okafor (Aunt) 08059876543'], barredMarker: 'Yes: see office',
    },
  ],
};

describe('SafeguardingSheetScreen', () => {
  it('generates the sheet only when asked, since every generation is audited, then shows the class', async () => {
    mockMe('pupil.safeguarding.view');
    mockClass();
    let generated = 0;
    server.use(
      http.get(apiUrl('/api/v1/reports/safeguarding'), ({ request }) => {
        generated += 1;
        expect(new URL(request.url).searchParams.get('armId')).toBe('arm-1');
        return HttpResponse.json(SHEET);
      }),
    );

    const { user } = renderWithProviders(<SafeguardingSheetScreen />);
    await user.click(await screen.findByRole('button', { name: 'Show sheet' }));

    expect(await screen.findByText('OKAFOR Chidera')).toBeInTheDocument();
    expect(screen.getByText('Groundnuts')).toBeInTheDocument();
    expect(screen.getByText('Yes: see office')).toBeInTheDocument();
    expect(generated).toBe(1);
  });

  it('says so when the caller can view no class', async () => {
    mockMe('pupil.view');
    mockClass();

    renderWithProviders(<SafeguardingSheetScreen />);

    expect(await screen.findByText('There are no classes whose safeguarding details you can view.')).toBeInTheDocument();
  });
});
