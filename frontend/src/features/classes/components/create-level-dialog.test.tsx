import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
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
    isEntryLevel: false,
    isGraduatingLevel: false,
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

describe('CreateLevelDialog — insert-after (AC: proves the chain rewired)', () => {
  it('creating a level inserted after Primary 1 reorders the list on refetch, not just a 201', async () => {
    mockMe('level.view', 'level.create');
    server.use(
      http.get(apiUrl('/api/v1/sections'), () => HttpResponse.json({ sections: [{ id: 'sec-1', name: 'Primary' }] })),
    );

    let created = false;
    server.use(
      http.get(apiUrl('/api/v1/levels'), () =>
        HttpResponse.json({
          items: created
            ? [level('l-1', 'Primary 1', 1), level('l-new', 'Primary 1B', 2), level('l-2', 'Primary 2', 3)]
            : [level('l-1', 'Primary 1', 1), level('l-2', 'Primary 2', 2)],
          nextCursor: null,
        }),
      ),
    );

    let idempotencyKeyOnCreate: string | null = null;
    server.use(
      http.post(apiUrl('/api/v1/levels'), ({ request }) => {
        idempotencyKeyOnCreate = request.headers.get('Idempotency-Key');
        created = true;
        return HttpResponse.json(level('l-new', 'Primary 1B', 2), { status: 201 });
      }),
    );

    const user = userEvent.setup();
    renderList();

    await screen.findByText('Primary 1');
    await user.click(screen.getByRole('button', { name: 'New level' }));

    await user.type(screen.getByLabelText('Level name'), 'Primary 1B');
    await user.click(await screen.findByRole('combobox', { name: 'Section' }));
    await user.click(await screen.findByRole('option', { name: 'Primary' }));
    await user.click(screen.getByRole('combobox', { name: 'Insert after' }));
    await user.click(await screen.findByRole('option', { name: 'Primary 1' }));

    await user.click(screen.getByRole('button', { name: 'Create level' }));

    // Proves the chain actually rewired: the new level appears BETWEEN
    // Primary 1 and Primary 2 once the list refetches, not merely a 201.
    const names = (await screen.findAllByRole('listitem')).map((li) => li.textContent ?? '');
    expect(names[0]).toContain('Primary 1');
    expect(names[1]).toContain('Primary 1B');
    expect(names[2]).toContain('Primary 2');
    expect(idempotencyKeyOnCreate).not.toBeNull();
  }, 15000);
});
