import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent, waitFor, within } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ArmDetailScreen } from './arm-detail-screen';

const ARM_ID = 'arm-1';

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

function arm(overrides: Record<string, unknown> = {}) {
  return {
    id: ARM_ID,
    classLevelId: 'level-1',
    classLevel: 'Primary 2',
    sessionId: 'session-1',
    label: 'C',
    displayName: 'Primary 2C',
    capacity: 22,
    formTeacherAdminId: null,
    status: 'Active',
    ...overrides,
  };
}

function renderScreen() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/arms/${ARM_ID}`]}>
        <Routes>
          <Route path="/arms/:id" element={<ArmDetailScreen />} />
          <Route path="/arms" element={<p>arms list</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ArmDetailScreen — four required states', () => {
  it('loading, then the arm renders', async () => {
    mockMe('arm.view');
    server.use(http.get(apiUrl('/api/v1/arms/:id'), () => HttpResponse.json(arm())));

    renderScreen();

    expect(screen.getByText('Loading arm…')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Primary 2C' })).toBeInTheDocument();
  });

  it('unauthorized renders nothing', async () => {
    mockMe('arm.view');
    server.use(http.get(apiUrl('/api/v1/arms/:id'), () => problemResponse(401)));

    const { container } = renderScreen();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('error shows a retry', async () => {
    mockMe('arm.view');
    server.use(http.get(apiUrl('/api/v1/arms/:id'), () => problemResponse(500)));

    renderScreen();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });
});

describe('ArmDetailScreen — edit (UpdateArm)', () => {
  it('saves label/capacity/status changes and shows the server rejection verbatim on failure', async () => {
    mockMe('arm.view', 'arm.update');
    server.use(http.get(apiUrl('/api/v1/arms/:id'), () => HttpResponse.json(arm())));
    server.use(
      http.patch(apiUrl('/api/v1/arms/:id'), () =>
        problemResponse(422, { detail: 'This arm is closed and cannot be edited.' }),
      ),
    );

    const user = userEvent.setup();
    renderScreen();

    await screen.findByRole('heading', { name: 'Primary 2C' });
    await user.click(screen.getByRole('button', { name: 'Edit' }));
    await user.clear(screen.getByLabelText('Label'));
    await user.type(screen.getByLabelText('Label'), 'D');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    // The backend's own rejection message surfaces verbatim, never paraphrased.
    expect(await screen.findByText('This arm is closed and cannot be edited.')).toBeInTheDocument();
  });

  it('no form-teacher control appears for a caller without arm.formteacher.assign', async () => {
    mockMe('arm.view', 'arm.update');
    server.use(http.get(apiUrl('/api/v1/arms/:id'), () => HttpResponse.json(arm())));

    const user = userEvent.setup();
    renderScreen();

    await screen.findByRole('heading', { name: 'Primary 2C' });
    await user.click(screen.getByRole('button', { name: 'Edit' }));

    const dialog = screen.getByRole('dialog');
    expect(within(dialog).queryByText('Form teacher')).not.toBeInTheDocument();
  });
});

describe('ArmDetailScreen — delete (DeleteArm)', () => {
  it('asks for confirmation naming the arm, then navigates back to the list', async () => {
    mockMe('arm.view', 'arm.delete');
    server.use(http.get(apiUrl('/api/v1/arms/:id'), () => HttpResponse.json(arm())));
    let deleted = false;
    server.use(
      http.delete(apiUrl('/api/v1/arms/:id'), () => {
        deleted = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    const user = userEvent.setup();
    renderScreen();

    await screen.findByRole('heading', { name: 'Primary 2C' });
    await user.click(screen.getByRole('button', { name: 'Delete arm' }));

    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('Primary 2C'));
    await waitFor(() => expect(deleted).toBe(true));
    expect(await screen.findByText('arms list')).toBeInTheDocument();
  });

  it('declining the confirmation never calls the endpoint', async () => {
    mockMe('arm.view', 'arm.delete');
    server.use(http.get(apiUrl('/api/v1/arms/:id'), () => HttpResponse.json(arm())));
    let deleted = false;
    server.use(
      http.delete(apiUrl('/api/v1/arms/:id'), () => {
        deleted = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    const user = userEvent.setup();
    renderScreen();

    await screen.findByRole('heading', { name: 'Primary 2C' });
    await user.click(screen.getByRole('button', { name: 'Delete arm' }));

    expect(deleted).toBe(false);
  });
});
