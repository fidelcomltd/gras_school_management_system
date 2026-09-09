import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { BulkCreateArmsDialog } from './bulk-create-arms-dialog';

function renderDialog() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <BulkCreateArmsDialog onClose={() => undefined} />
    </QueryClientProvider>,
  );
}

function mockLookups() {
  server.use(
    http.get(apiUrl('/api/v1/sessions'), () =>
      HttpResponse.json({
        items: [
          { id: 'session-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Upcoming', armCount: 0 },
        ],
        nextCursor: null,
      }),
    ),
    http.get(apiUrl('/api/v1/levels'), () =>
      HttpResponse.json({
        items: [
          {
            id: 'level-1',
            name: 'Primary 1',
            sectionId: 'sec-1',
            section: 'Primary',
            progressionOrder: 1,
            nextLevelId: null,
            isEntryLevel: true,
            isGraduatingLevel: true,
            status: 'Active',
          },
        ],
        nextCursor: null,
      }),
    ),
  );
}

/**
 * TASK-0045 AC: "Bulk create never commits without a dryRun preview first —
 * asserted by proving no non-dry-run request fires until confirmation."
 */
describe('BulkCreateArmsDialog — dry-run always first', () => {
  it('sends only a dryRun request until the caller confirms the preview, then commits for real', async () => {
    mockLookups();
    const requestsSeen: { dryRun: boolean }[] = [];
    server.use(
      http.post(apiUrl('/api/v1/arms/bulk'), async ({ request }) => {
        const body = (await request.json()) as { dryRun: boolean; levels: { levelId: string }[] };
        requestsSeen.push({ dryRun: body.dryRun });
        return HttpResponse.json({
          created: [
            {
              id: 'arm-new',
              classLevelId: 'level-1',
              classLevel: 'Primary 1',
              sessionId: 'session-1',
              label: 'A',
              displayName: 'Primary 1A',
              capacity: 30,
              formTeacherAdminId: null,
              status: 'Active',
            },
          ],
        });
      }),
    );

    const user = userEvent.setup();
    renderDialog();

    await screen.findByText('Primary 1');
    await user.click(screen.getByRole('combobox', { name: 'Session' }));
    await user.click(await screen.findByRole('option', { name: '2026/2027' }));
    await user.type(screen.getByLabelText('Arms to create for Primary 1'), '3');
    await user.type(screen.getByLabelText('Capacity each for Primary 1'), '30');

    await user.click(screen.getByRole('button', { name: 'Preview' }));

    // The preview response is shown, and ONLY a dryRun request has fired —
    // no commit request exists yet.
    expect(await screen.findByText('Primary 1A')).toBeInTheDocument();
    expect(requestsSeen).toEqual([{ dryRun: true }]);
    expect(requestsSeen.some((r) => r.dryRun === false)).toBe(false);

    await user.click(screen.getByRole('button', { name: 'Create 1 arms' }));

    await waitFor(() => expect(requestsSeen).toContainEqual({ dryRun: false }));
  });

  it('"Back" discards the preview and returns to editable inputs, with no commit control left standing', async () => {
    mockLookups();
    server.use(
      http.post(apiUrl('/api/v1/arms/bulk'), () =>
        HttpResponse.json({
          created: [
            {
              id: 'arm-new',
              classLevelId: 'level-1',
              classLevel: 'Primary 1',
              sessionId: 'session-1',
              label: 'A',
              displayName: 'Primary 1A',
              capacity: 30,
              formTeacherAdminId: null,
              status: 'Active',
            },
          ],
        }),
      ),
    );

    const user = userEvent.setup();
    renderDialog();

    await screen.findByText('Primary 1');
    await user.click(screen.getByRole('combobox', { name: 'Session' }));
    await user.click(await screen.findByRole('option', { name: '2026/2027' }));
    await user.type(screen.getByLabelText('Arms to create for Primary 1'), '3');
    await user.click(screen.getByRole('button', { name: 'Preview' }));

    expect(await screen.findByText('Primary 1A')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create 1 arms' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Back' }));

    expect(screen.queryByText('Primary 1A')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Create \d+ arms/ })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Arms to create for Primary 1')).toBeInTheDocument();
  });

  it('changing the session after a preview discards it — a stale preview can never be committed', async () => {
    mockLookups();
    server.use(
      http.get(apiUrl('/api/v1/sessions'), () =>
        HttpResponse.json({
          items: [
            { id: 'session-1', name: '2026/2027', startDate: '2026-09-14', endDate: '2027-07-25', state: 'Upcoming', armCount: 0 },
            { id: 'session-2', name: '2027/2028', startDate: '2027-09-14', endDate: '2028-07-25', state: 'Upcoming', armCount: 0 },
          ],
          nextCursor: null,
        }),
      ),
      http.post(apiUrl('/api/v1/arms/bulk'), () =>
        HttpResponse.json({
          created: [
            {
              id: 'arm-new',
              classLevelId: 'level-1',
              classLevel: 'Primary 1',
              sessionId: 'session-1',
              label: 'A',
              displayName: 'Primary 1A',
              capacity: 30,
              formTeacherAdminId: null,
              status: 'Active',
            },
          ],
        }),
      ),
    );

    const user = userEvent.setup();
    renderDialog();

    await screen.findByText('Primary 1');
    await user.click(screen.getByRole('combobox', { name: 'Session' }));
    await user.click(await screen.findByRole('option', { name: '2026/2027' }));
    await user.type(screen.getByLabelText('Arms to create for Primary 1'), '3');
    await user.click(screen.getByRole('button', { name: 'Preview' }));

    expect(await screen.findByText('Primary 1A')).toBeInTheDocument();

    await user.click(screen.getByRole('combobox', { name: 'Session' }));
    await user.click(await screen.findByRole('option', { name: '2027/2028' }));

    expect(screen.queryByText('Primary 1A')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Create \d+ arms/ })).not.toBeInTheDocument();
  });
});
