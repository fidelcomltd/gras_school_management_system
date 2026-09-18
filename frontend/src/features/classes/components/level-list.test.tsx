import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent, waitFor, within } from '@/test/render';
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

function level(id: string, name: string, order: number, overrides: Record<string, unknown> = {}) {
  return {
    id,
    name,
    sectionId: 'sec-1',
    section: 'Primary',
    progressionOrder: order,
    nextLevelId: null,
    isEntryLevel: false,
    isGraduatingLevel: false,
    status: 'Active',
    ...overrides,
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

describe('LevelList — progressionOrder, not alphabetical (AC)', () => {
  it('renders in progressionOrder even when the server response is not already sorted that way', async () => {
    mockMe('level.view');
    server.use(
      http.get(apiUrl('/api/v1/levels'), () =>
        HttpResponse.json({
          items: [level('l-3', 'Zebra Class', 3), level('l-1', 'Aardvark Class', 1), level('l-2', 'Middle Class', 2)],
          nextCursor: null,
        }),
      ),
    );

    renderList();

    const names = (await screen.findAllByRole('listitem')).map((li) => within(li).getByText(/Class$/).textContent);
    expect(names).toEqual(['Aardvark Class', 'Middle Class', 'Zebra Class']);
  });
});

describe('LevelList — delete surfaces the named 409 verbatim', () => {
  it('shows the server-supplied reference list, not a generic conflict message', async () => {
    mockMe('level.view', 'level.delete');
    server.use(
      http.get(apiUrl('/api/v1/levels'), () =>
        HttpResponse.json({ items: [level('l-1', 'Primary 1', 1)], nextCursor: null }),
      ),
    );
    server.use(
      http.delete(apiUrl('/api/v1/levels/l-1'), () =>
        problemResponse(409, { detail: 'Primary 1 is referenced by Arm "1A" and cannot be deleted.' }),
      ),
    );
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'Delete' }));

    expect(
      await screen.findByText('Primary 1 is referenced by Arm "1A" and cannot be deleted.'),
    ).toBeInTheDocument();
  });
});

describe('LevelList — reorder', () => {
  it('moving a level posts the whole ordered array and re-renders from the response', async () => {
    mockMe('level.view', 'level.update');
    server.use(
      http.get(apiUrl('/api/v1/levels'), () =>
        HttpResponse.json({
          items: [level('l-1', 'Primary 1', 1), level('l-2', 'Primary 2', 2)],
          nextCursor: null,
        }),
      ),
    );

    let postedBody: { orderedLevelIds: string[] } | undefined;
    server.use(
      http.post(apiUrl('/api/v1/levels/reorder'), async ({ request }) => {
        postedBody = (await request.json()) as { orderedLevelIds: string[] };
        return HttpResponse.json([
          level('l-2', 'Primary 2', 1),
          level('l-1', 'Primary 1', 2),
        ]);
      }),
    );

    const user = userEvent.setup();
    renderList();

    await screen.findByText('Primary 1');
    await user.click(screen.getByRole('button', { name: 'Move Primary 2 up' }));

    await waitFor(() => expect(postedBody).toEqual({ orderedLevelIds: ['l-2', 'l-1'] }));

    const names = (await screen.findAllByRole('listitem')).map(
      (li) => within(li).getByText(/^Primary \d$/).textContent,
    );
    expect(names).toEqual(['Primary 2', 'Primary 1']);
  });
});
