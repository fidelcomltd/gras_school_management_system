import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import type { components } from '@/api/schema';
import type { PupilImportRow } from './api';
import { ImportPupilsScreen } from './import-pupils-screen';
import { reportToCsv } from './report-csv';

type Report = components['schemas']['PupilImportReportDto'];
type Row = components['schemas']['PupilImportRowDto'];

const HASH = 'a'.repeat(64);

function row(sheetRow: number, overrides: Partial<Row> = {}): Row {
  return {
    sheetRow,
    surname: 'Okafor',
    firstName: `Child${sheetRow}`,
    dateOfBirth: '2020-05-03',
    armId: 'arm-1',
    armName: 'Primary 1A',
    outcome: 'Accepted',
    errors: [],
    registerMatches: [],
    ...overrides,
  };
}

function report(rows: Row[], overrides: Partial<Report> = {}): Report {
  const rejected = rows.filter((candidate) => candidate.outcome === 'Rejected').length;
  return {
    fileSha256: HASH,
    totalRows: rows.length,
    acceptedCount: rows.length - rejected,
    rejectedCount: rejected,
    registerMatchCount: rows.filter((candidate) => candidate.registerMatches.length > 0).length,
    rows,
    capacityWarnings: [],
    ...overrides,
  };
}

function renderScreen() {
  return renderWithProviders(
    <MemoryRouter>
      <ImportPupilsScreen />
    </MemoryRouter>,
  );
}

async function chooseAndCheck(user: ReturnType<typeof renderScreen>['user'], body: Report) {
  server.use(http.post(apiUrl('/api/v1/pupils/import/validate'), () => HttpResponse.json(body)));
  const file = new File(['xlsx'], 'register.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
  await user.upload(screen.getByLabelText('Filled-in template (.xlsx)'), file);
  await user.click(screen.getByRole('button', { name: 'Check file' }));
}

describe('ImportPupilsScreen', () => {
  it('lists each rejection by row and column, and imports nothing until they are fixed', async () => {
    mockMe('pupil.import');
    const { user } = renderScreen();

    await chooseAndCheck(
      user,
      report([
        row(2),
        row(3, { outcome: 'Rejected', errors: [{ column: 'Date of Birth', message: '03/05/20 has a 2-digit year.' }] }),
      ]),
    );

    expect(await screen.findByText('Rejected rows')).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Date of Birth' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: '03/05/20 has a 2-digit year.' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Import 1 pupil' })).toBeDisabled();
  });

  it('needs a decision for each register match, then sends it and shows the numbers issued', async () => {
    mockMe('pupil.import');
    let sent: FormData | null = null;
    server.use(
      http.post(apiUrl('/api/v1/pupils/import/commit'), async ({ request }) => {
        sent = await request.formData();
        return HttpResponse.json({
          importedCount: 1,
          skippedCount: 1,
          pupils: [{ sheetRow: 3, pupilId: 'p-3', registrationNumber: 'GRAS/2026/0041' }],
        });
      }),
    );
    const { user } = renderScreen();
    const match = { pupilId: 'p-old', registrationNumber: 'GRAS/2024/0007', status: 'Active' as const, surname: 'Okafor', firstName: 'Child2', dateOfBirth: '2020-05-03' };

    await chooseAndCheck(user, report([row(2, { registerMatches: [match] }), row(3)]));

    const importButton = await screen.findByRole('button', { name: 'Import 2 pupils' });
    expect(importButton).toBeDisabled();
    await user.click(screen.getByLabelText('Skip this row'));
    await user.click(screen.getByRole('button', { name: 'Import 1 pupil' }));

    expect(await screen.findByText(/Imported 1 pupil, numbered GRAS\/2026\/0041\. Skipped 1\./)).toBeInTheDocument();
    const form = sent as unknown as FormData;
    expect(form.get('fileSha256')).toBe(HASH);
    expect(form.getAll('skipRows')).toEqual(['2']);
    expect(form.getAll('createRows')).toEqual([]);
  });

  it('blocks an over-capacity import for a caller without the override privilege', async () => {
    mockMe('pupil.import');
    const { user } = renderScreen();

    await chooseAndCheck(
      user,
      report([row(2)], { capacityWarnings: [{ armId: 'arm-1', armName: 'Primary 1A', capacity: 30, currentCount: 30, importCount: 1 }] }),
    );

    expect(await screen.findByText(/needs the capacity override privilege/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Import 1 pupil' })).toBeDisabled();
  });

  it('drops a capacity warning once skipping rows brings the arm back within capacity', async () => {
    mockMe('pupil.import');
    const { user } = renderScreen();
    const match = { pupilId: 'p-old', registrationNumber: 'GRAS/2024/0007', status: 'Active' as const, surname: 'Okafor', firstName: 'Child2', dateOfBirth: '2020-05-03' };

    await chooseAndCheck(
      user,
      report([row(2, { registerMatches: [match] }), row(3)], {
        capacityWarnings: [{ armId: 'arm-1', armName: 'Primary 1A', capacity: 30, currentCount: 29, importCount: 2 }],
      }),
    );

    expect(await screen.findByText(/needs the capacity override privilege/)).toBeInTheDocument();
    await user.click(screen.getByLabelText('Skip this row'));

    expect(screen.queryByText(/needs the capacity override privilege/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Import 1 pupil' })).toBeEnabled();
  });

  it('lets a caller holding the override confirm it', async () => {
    mockMe('pupil.import', 'arm.capacity.override');
    const { user } = renderScreen();

    await chooseAndCheck(
      user,
      report([row(2)], { capacityWarnings: [{ armId: 'arm-1', armName: 'Primary 1A', capacity: 30, currentCount: 30, importCount: 1 }] }),
    );

    await user.click(await screen.findByLabelText(/Import past capacity/));
    expect(screen.getByRole('button', { name: 'Import 1 pupil' })).toBeEnabled();
  });

  it('warns above 500 rows that the import may take a minute', async () => {
    mockMe('pupil.import');
    const { user } = renderScreen();

    await chooseAndCheck(user, report(Array.from({ length: 501 }, (_, index) => row(index + 2))));

    expect(await screen.findByText(/This file has 501 pupils. Importing it may take a minute/)).toBeInTheDocument();
  });

  it('shows a whole-file problem from the server', async () => {
    mockMe('pupil.import');
    server.use(
      http.post(apiUrl('/api/v1/pupils/import/validate'), () =>
        problemResponse(422, { errorCode: 'import.missing_columns', detail: 'The file has no Surname column. Use the headers from the template.' }),
      ),
    );
    const { user } = renderScreen();
    await user.upload(screen.getByLabelText('Filled-in template (.xlsx)'), new File(['x'], 'bad.xlsx'));
    await user.click(screen.getByRole('button', { name: 'Check file' }));

    expect(await screen.findByText('The file has no Surname column. Use the headers from the template.')).toBeInTheDocument();
  });
});

describe('reportToCsv', () => {
  it('writes one line per problem and defuses a formula-looking cell', () => {
    const rejected: PupilImportRow = {
      ...row(2),
      sheetRow: 2,
      surname: '=HYPERLINK("x")',
      outcome: 'Rejected',
      errors: [{ column: 'Sex', message: 'Enter Male or Female.' }],
    };
    const csv = reportToCsv({
      fileSha256: HASH,
      totalRows: 1,
      acceptedCount: 0,
      rejectedCount: 1,
      registerMatchCount: 0,
      rows: [rejected],
      capacityWarnings: [],
    });

    const lines = csv.trim().split('\r\n');
    expect(lines[0]).toBe('Sheet row,Surname,First name,Outcome,Column,Reason');
    expect(lines[1]).toBe(`2,"'=HYPERLINK(""x"")",Child2,Rejected,Sex,Enter Male or Female.`);
  });
});
