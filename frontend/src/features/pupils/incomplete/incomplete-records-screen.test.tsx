import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import type { components } from '@/api/schema';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, within } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { IncompleteRecordsScreen } from './incomplete-records-screen';

type Report = components['schemas']['IncompleteRecordsReportDto'];
type Pupil = components['schemas']['IncompleteRecordDto'];

const NO_EMERGENCY = { step: 3, code: 'contacts.emergency_primary', message: 'Add the primary emergency contact.' };
const NO_BIRTH_CERTIFICATE = { step: 7, code: 'documents.BirthCertificate', message: 'Birth certificate.' };

function pupil(surname: string, armId: string, armName: string, overrides: Partial<Pupil> = {}): Pupil {
  return {
    pupilId: `p-${surname}`,
    registrationNumber: `GRAS/2026/${surname.length}`,
    surname,
    firstName: 'Ada',
    middleName: null,
    armId,
    armName,
    chasedPercent: 80,
    required: [],
    chased: [NO_BIRTH_CERTIFICATE],
    ...overrides,
  };
}

const REPORT: Report = {
  sessionName: '2026/2027',
  pupilsChecked: 10,
  counts: [
    { code: 'contacts.emergency_primary', label: 'No primary emergency contact', required: true, count: 1 },
    { code: 'documents.BirthCertificate', label: 'Birth certificate not received', required: false, count: 2 },
  ],
  pupils: [
    pupil('Bello', 'arm-a', 'Primary 1A', { required: [NO_EMERGENCY] }),
    pupil('Okafor', 'arm-b', 'Primary 1B'),
  ],
};

function renderScreen(body: Report | Response = REPORT) {
  server.use(http.get(apiUrl('/api/v1/reports/incomplete-records'), () => (body instanceof Response ? body : HttpResponse.json(body))));
  return renderWithProviders(
    <MemoryRouter>
      <IncompleteRecordsScreen />
    </MemoryRouter>,
  );
}

describe('IncompleteRecordsScreen', () => {
  it('lists each pupil with what is missing, and counts each gap', async () => {
    mockMe('report.view');
    renderScreen();

    expect(await screen.findByText('2026/2027: 2 of 10 active pupils have something missing.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'BELLO Ada' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'No primary emergency contact: 1' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Birth certificate not received: 2' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Download CSV' })).not.toBeInTheDocument();
  });

  it('filters by a gap when its count is pressed, and again to clear it', async () => {
    mockMe('report.view', 'report.export');
    const { user } = renderScreen();

    await user.click(await screen.findByRole('button', { name: 'No primary emergency contact: 1' }));
    const table = screen.getByRole('table');
    expect(within(table).getByRole('link', { name: 'BELLO Ada' })).toBeInTheDocument();
    expect(within(table).queryByRole('link', { name: 'OKAFOR Ada' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'No primary emergency contact: 1' }));
    expect(within(screen.getByRole('table')).getByRole('link', { name: 'OKAFOR Ada' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Download CSV' })).toBeEnabled();
  });

  it('says so when no session is active', async () => {
    mockMe('report.view');
    renderScreen({ sessionName: null, pupilsChecked: 0, counts: [], pupils: [] });

    expect(await screen.findByText('No session is active, so there is no roll to check.')).toBeInTheDocument();
  });

  it('offers a retry when the report fails', async () => {
    mockMe('report.view');
    renderScreen(problemResponse(500));

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});
