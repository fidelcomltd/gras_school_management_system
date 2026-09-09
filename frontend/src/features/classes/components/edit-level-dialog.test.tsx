import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { LevelList } from './level-list';

function mockMe(...privileges: string[]) {
  server.use(
    http.get(apiUrl('/api/v1/auth/me'), () =>
      HttpResponse.json({
        accountId: 'acc-1',
        email: 'admin@example.com',
        staffName: 'Chisom Maxwell',
        isSuperAdmin: false,
        mustChangePassword: false,
        effectivePrivileges: privileges.map((privilege) => ({ privilege, scope: 'SchoolWide', armIds: [] })),
        sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
      }),
    ),
  );
}

function level(id: string, name: string, order: number) {
  return {
    id,
    name,
    sectionId: 'sec-1',
    section: 'Primary',
    progressionOrder: order,
    nextLevelId: null,
    isEntryLevel: order === 1,
    isGraduatingLevel: true,
    status: 'Active',
  };
}

function renderList() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <LevelList />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('EditLevelDialog', () => {
  it('refetches the level (GET /levels/{id}) and prefills from that fresher data, not the stale list row', async () => {
    mockMe('level.view', 'level.update');
    server.use(
      http.get(apiUrl('/api/v1/levels'), () =>
        HttpResponse.json({ items: [level('l-1', 'Primary 1', 1)], nextCursor: null }),
      ),
    );
    let getLevelCalled = false;
    server.use(
      http.get(apiUrl('/api/v1/levels/l-1'), () => {
        getLevelCalled = true;
        // A concurrent editor already renamed it server-side.
        return HttpResponse.json(level('l-1', 'Primary One (renamed)', 1));
      }),
    );

    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'Edit' }));

    await screen.findByDisplayValue('Primary One (renamed)');
    expect(getLevelCalled).toBe(true);
  });

  it('a chain-rule 422 with no matching field surfaces verbatim in the general banner', async () => {
    mockMe('level.view', 'level.update');
    server.use(
      http.get(apiUrl('/api/v1/levels'), () =>
        HttpResponse.json({ items: [level('l-1', 'Primary 1', 1)], nextCursor: null }),
      ),
    );
    server.use(http.get(apiUrl('/api/v1/levels/l-1'), () => HttpResponse.json(level('l-1', 'Primary 1', 1))));
    server.use(
      http.patch(apiUrl('/api/v1/levels/l-1'), () =>
        problemResponse(422, { detail: 'This change would leave two levels with no entry point.' }),
      ),
    );

    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'Edit' }));
    await screen.findByDisplayValue('Primary 1');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(
      await screen.findByText('This change would leave two levels with no entry point.'),
    ).toBeInTheDocument();
  });
});
