import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { SectionList } from './section-list';

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

function renderList() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <SectionList />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('SectionList — four required states', () => {
  it('loading, then empty when there are no sections', async () => {
    mockMe('level.view');
    server.use(http.get(apiUrl('/api/v1/sections'), () => HttpResponse.json({ sections: [] })));

    renderList();

    expect(screen.getByText('Loading sections…')).toBeInTheDocument();
    expect(await screen.findByText('No sections yet.')).toBeInTheDocument();
  });

  it('unauthorized renders nothing', async () => {
    mockMe('level.view');
    server.use(http.get(apiUrl('/api/v1/sections'), () => problemResponse(401)));

    const { container } = renderList();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('error shows a retry', async () => {
    mockMe('level.view');
    server.use(http.get(apiUrl('/api/v1/sections'), () => problemResponse(500)));

    renderList();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });
});

describe('SectionList — create, gated by level.create', () => {
  it('a caller without level.create sees no New section button', async () => {
    mockMe('level.view');
    server.use(http.get(apiUrl('/api/v1/sections'), () => HttpResponse.json({ sections: [] })));

    renderList();

    await screen.findByText('No sections yet.');
    expect(screen.queryByRole('button', { name: 'New section' })).not.toBeInTheDocument();
  });

  it('creates a section and shows it once the list refetches', async () => {
    mockMe('level.view', 'level.create');
    let created = false;
    server.use(
      http.get(apiUrl('/api/v1/sections'), () =>
        HttpResponse.json({ sections: created ? [{ id: 'sec-2', name: 'Secondary', ratesTraits: false }] : [] }),
      ),
    );
    server.use(
      http.post(apiUrl('/api/v1/sections'), () => {
        created = true;
        return HttpResponse.json({ id: 'sec-2', name: 'Secondary', ratesTraits: false }, { status: 201 });
      }),
    );

    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'New section' }));
    await user.type(screen.getByLabelText('Section name'), 'Secondary');
    await user.click(screen.getByRole('button', { name: 'Create section' }));

    expect(await screen.findByText('Secondary')).toBeInTheDocument();
  });
});

describe('SectionList — rename (UpdateSection)', () => {
  it('renames a section and shows the new name once the list refetches', async () => {
    mockMe('level.view', 'level.update');
    let name = 'Primary';
    server.use(
      http.get(apiUrl('/api/v1/sections'), () =>
        HttpResponse.json({ sections: [{ id: 'sec-1', name, ratesTraits: true }] }),
      ),
    );
    server.use(
      http.patch(apiUrl('/api/v1/sections/sec-1'), () => {
        name = 'Primary School';
        return HttpResponse.json({ id: 'sec-1', name, ratesTraits: true });
      }),
    );

    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'Rename' }));
    await user.clear(screen.getByLabelText('Section name'));
    await user.type(screen.getByLabelText('Section name'), 'Primary School');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('Primary School')).toBeInTheDocument();
  });
});
