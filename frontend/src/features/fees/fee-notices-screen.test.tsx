import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor, within } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { FeeNoticesScreen } from './fee-notices-screen';
import type { FeeNoticeGridDto, OutstandingFeeSheetDto } from './types';

const SECTION = '00000000-0000-0000-0000-000000000302';

function mockTermAndClass() {
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
    http.get(apiUrl('/api/v1/sections'), () => HttpResponse.json({ sections: [{ id: SECTION, name: 'Primary', ratesTraits: true }] })),
    http.get(apiUrl('/api/v1/arms'), () =>
      HttpResponse.json({ items: [{ id: 'arm-1', displayName: 'Primary 1A', label: 'A', classLevelId: 'p1', classLevel: 'Primary 1', sessionId: 's-1', capacity: null, formTeacherAdminId: null, status: 'Active' }], nextCursor: null }),
    ),
  );
}

// The breadcrumb trail links, so the screen needs a router around it.
const renderScreen = () =>
  renderWithProviders(
    <MemoryRouter initialEntries={['/fees']}>
      <FeeNoticesScreen />
    </MemoryRouter>,
  );

function grid(overrides: Partial<FeeNoticeGridDto> = {}): FeeNoticeGridDto {
  return {
    sectionId: SECTION, sectionName: 'Primary', termId: 't-1', termName: 'First Term', sessionName: '2026/2027',
    previousTermId: 't-0', previousTermLabel: 'Third Term 2025/2026', isDefault: true,
    levels: [{ classLevelId: 'p1', name: 'Primary 1' }, { classLevelId: 'p2', name: 'Primary 2' }],
    lines: [
      { id: null, label: 'Tuition Fee', kind: 'Amount', showOnPortal: false, amounts: [] },
      { id: null, label: 'Outstanding Fee', kind: 'Outstanding', showOnPortal: false, amounts: [] },
    ],
    ...overrides,
  };
}

describe('FeeNoticesScreen', () => {
  it('saves the typed amounts for every class, a cleared cell as null, and copies from the previous term', async () => {
    mockMe('fee.manage', 'session.view', 'arm.view', 'level.view');
    mockTermAndClass();
    let saved: unknown;
    server.use(
      http.get(apiUrl('/api/v1/fee-notices'), ({ request }) =>
        new URL(request.url).searchParams.get('termId') === 't-0'
          ? HttpResponse.json(grid({ termId: 't-0', isDefault: false, lines: [{ id: 'l-1', label: 'Tuition Fee', kind: 'Amount', showOnPortal: false, amounts: [{ classLevelId: 'p2', amount: 40000 }] }] }))
          : HttpResponse.json(grid()),
      ),
      http.put(apiUrl('/api/v1/fee-notices'), async ({ request }) => {
        saved = await request.json();
        return HttpResponse.json(grid({ isDefault: false }));
      }),
    );

    const { user } = renderScreen();
    await user.click(await screen.findByRole('button', { name: 'Copy from Third Term 2025/2026' }));
    expect(await screen.findByDisplayValue('40000')).toBeInTheDocument();
    await user.type(screen.getByRole('textbox', { name: 'Tuition Fee, Primary 1' }), '45,000');
    await user.click(screen.getByRole('checkbox', { name: 'Show it to parents on the portal' }));
    await user.click(screen.getByRole('button', { name: 'Save fee lines' }));

    await waitFor(() => expect(saved).toBeDefined());
    expect(saved).toEqual({
      sectionId: SECTION,
      termId: 't-1',
      lines: [
        { id: null, label: 'Tuition Fee', kind: 'Amount', showOnPortal: false, amounts: [{ classLevelId: 'p1', amount: 45000 }, { classLevelId: 'p2', amount: 40000 }] },
        { id: null, label: 'Outstanding Fee', kind: 'Outstanding', showOnPortal: true, amounts: [] },
      ],
    });
  });

  it('refuses to save a duplicate label or an amount that is not whole naira', async () => {
    mockMe('fee.manage', 'session.view', 'arm.view', 'level.view');
    mockTermAndClass();
    server.use(http.get(apiUrl('/api/v1/fee-notices'), () => HttpResponse.json(grid())));

    const { user } = renderScreen();
    await user.type(await screen.findByRole('textbox', { name: 'Tuition Fee, Primary 1' }), '45.50');
    expect(screen.getByText(/Amounts are whole naira/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save fee lines' })).toBeDisabled();

    await user.clear(screen.getByRole('textbox', { name: 'Tuition Fee, Primary 1' }));
    await user.click(screen.getByRole('button', { name: 'Add line' }));
    await user.type(screen.getByRole('textbox', { name: 'Label for line 3' }), 'tuition fee');
    expect(screen.getByText('Two lines have the same label.')).toBeInTheDocument();
  });

  it('saves a class\'s outstanding figures, and shows them read-only once results are published', async () => {
    mockMe('fee.manage', 'session.view', 'arm.view', 'level.view');
    mockTermAndClass();
    const sheet = (locked: boolean): OutstandingFeeSheetDto => ({
      armId: 'arm-1', armName: 'Primary 1A', termId: 't-1', termName: 'First Term', locked,
      rows: [
        { pupilId: 'p-1', displayName: 'Okafor Ada', registrationNumber: 'GRA/2026/0001', amount: null },
        { pupilId: 'p-2', displayName: 'Bello Tunde', registrationNumber: 'GRA/2026/0002', amount: 5000 },
      ],
    });
    let saved: unknown;
    server.use(
      http.get(apiUrl('/api/v1/arms/:armId/outstanding-fees'), () => HttpResponse.json(sheet(false))),
      http.put(apiUrl('/api/v1/arms/:armId/outstanding-fees'), async ({ request }) => {
        saved = await request.json();
        return HttpResponse.json(sheet(false));
      }),
    );

    const { user } = renderScreen();
    await user.click(await screen.findByRole('button', { name: 'Outstanding figures' }));
    await user.type(await screen.findByRole('textbox', { name: 'Outstanding for Okafor Ada' }), '12500');
    await user.clear(screen.getByRole('textbox', { name: 'Outstanding for Bello Tunde' }));
    await user.click(screen.getByRole('button', { name: 'Save figures' }));

    await waitFor(() => expect(saved).toBeDefined());
    expect(saved).toEqual({ armId: 'arm-1', termId: 't-1', rows: [{ pupilId: 'p-1', amount: 12500 }, { pupilId: 'p-2', amount: null }] });

    server.use(http.get(apiUrl('/api/v1/arms/:armId/outstanding-fees'), () => HttpResponse.json(sheet(true))));
    server.use(http.get(apiUrl('/api/v1/fee-notices'), () => HttpResponse.json(grid())));
    await user.click(screen.getByRole('button', { name: 'Fee lines' }));
    await user.click(screen.getByRole('button', { name: 'Outstanding figures' }));
    const region = await screen.findByRole('region', { name: 'Primary 1A outstanding figures' });
    await waitFor(() => expect(within(region).getByRole('textbox', { name: 'Outstanding for Okafor Ada' })).toBeDisabled());
    expect(within(region).queryByRole('button', { name: 'Save figures' })).not.toBeInTheDocument();
  });
});
