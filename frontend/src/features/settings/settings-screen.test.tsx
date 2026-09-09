import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { SettingsScreen } from './settings-screen';

const IDENTITY = {
  schoolName: 'Golden Royal Ark School',
  shortName: 'GRAS',
  address: '12 Ark Crescent, Lekki, Lagos',
  phone: '+2348012345678',
  email: 'info@goldenroyalark.example',
  motto: 'Excellence Through Character',
  headTeacherName: 'Chisom Maxwell',
  timezone: 'Africa/Lagos',
  versionNumber: 3,
};

function sessionWith(...privileges: string[]) {
  return {
    accountId: 'acc-1',
    email: 'admin@example.com',
    staffName: 'Chisom Maxwell',
    isSuperAdmin: false,
    mustChangePassword: false,
    effectivePrivileges: privileges.map((privilege) => ({ privilege, scope: 'SchoolWide', armIds: [] })),
    sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
  };
}

function mockMe(...privileges: string[]) {
  server.use(http.get(apiUrl('/api/v1/auth/me'), () => HttpResponse.json(sessionWith(...privileges))));
}

function renderScreen() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <SettingsScreen />
    </QueryClientProvider>,
  );
}

describe('SettingsScreen — four required states', () => {
  it('loading: shows a status region before /settings resolves', () => {
    mockMe('settings.view');
    server.use(http.get(apiUrl('/api/v1/settings'), () => new Promise(() => undefined)));

    renderScreen();

    expect(screen.getByText('Loading settings…')).toBeInTheDocument();
  });

  it('unauthorized: renders nothing — ProtectedLayout is already navigating away', async () => {
    mockMe('settings.view');
    server.use(
      http.get(apiUrl('/api/v1/settings'), () => problemResponse(401, { errorCode: 'authentication.required' })),
    );

    const { container } = renderScreen();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('error: a server failure shows a retry, not a crash', async () => {
    mockMe('settings.view');
    server.use(http.get(apiUrl('/api/v1/settings'), () => problemResponse(500)));

    renderScreen();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('success, read-only: a caller without settings.identity.update sees values but no form', async () => {
    mockMe('settings.view');
    server.use(http.get(apiUrl('/api/v1/settings'), () => HttpResponse.json({ identity: IDENTITY })));

    renderScreen();

    expect(await screen.findByText('Golden Royal Ark School')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save changes' })).not.toBeInTheDocument();
  });

  it('success, editable: a caller with settings.identity.update sees the form, pre-filled', async () => {
    mockMe('settings.view', 'settings.identity.update');
    server.use(http.get(apiUrl('/api/v1/settings'), () => HttpResponse.json({ identity: IDENTITY })));

    renderScreen();

    expect(await screen.findByLabelText('Short name')).toHaveValue('GRAS');
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeInTheDocument();
  });
});

describe('SettingsScreen — edit path', () => {
  it('a successful PATCH updates the view', async () => {
    mockMe('settings.view', 'settings.identity.update');
    server.use(http.get(apiUrl('/api/v1/settings'), () => HttpResponse.json({ identity: IDENTITY })));
    server.use(
      http.patch(apiUrl('/api/v1/settings/identity'), () =>
        HttpResponse.json({ ...IDENTITY, shortName: 'GRA School', versionNumber: 4 }),
      ),
    );

    const user = userEvent.setup();
    renderScreen();

    const shortName = await screen.findByLabelText('Short name');
    await user.clear(shortName);
    await user.type(shortName, 'GRA School');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('Settings updated.')).toBeInTheDocument();
  });

  it('a 422 maps errors onto the matching fields by name', async () => {
    mockMe('settings.view', 'settings.identity.update');
    server.use(http.get(apiUrl('/api/v1/settings'), () => HttpResponse.json({ identity: IDENTITY })));
    server.use(
      http.patch(apiUrl('/api/v1/settings/identity'), () =>
        problemResponse(422, { errors: { Email: ['Enter a valid email address.'] } }),
      ),
    );

    const user = userEvent.setup();
    renderScreen();

    await screen.findByLabelText('Short name');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
  });
});
