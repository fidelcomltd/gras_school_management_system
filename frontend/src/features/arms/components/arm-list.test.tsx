import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ArmList } from './arm-list';

const ARM_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d48';
const TEACHER_ID = '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d42';

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

function arm(overrides: Partial<Record<string, unknown>> = {}) {
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

function renderList() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <ArmList filters={{}} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ArmList — four required states', () => {
  it('loading, then empty when there are no arms', async () => {
    mockMe('arm.view');
    server.use(http.get(apiUrl('/api/v1/arms'), () => HttpResponse.json({ items: [], nextCursor: null })));

    renderList();

    expect(screen.getByText('Loading arms…')).toBeInTheDocument();
    expect(await screen.findByText('No arms found.')).toBeInTheDocument();
  });

  it('unauthorized renders nothing', async () => {
    mockMe('arm.view');
    server.use(http.get(apiUrl('/api/v1/arms'), () => problemResponse(401)));

    const { container } = renderList();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('error shows a retry', async () => {
    mockMe('arm.view');
    server.use(http.get(apiUrl('/api/v1/arms'), () => problemResponse(500)));

    renderList();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });
});

describe('ArmList — displayName is always the server value (TASK-0045 AC)', () => {
  it('renders the server-composed displayName verbatim, never level + label recomposed client-side', async () => {
    mockMe('arm.view');
    // Deliberately a displayName that a naive `classLevel + label` join would
    // NOT reproduce ("Primary 2" + "C" -> "Primary 2C"), so this only passes
    // if the component renders `displayName` directly.
    server.use(
      http.get(apiUrl('/api/v1/arms'), () =>
        HttpResponse.json({
          items: [arm({ displayName: 'Room 2C — East Wing', classLevel: 'Primary 2', label: 'C' })],
          nextCursor: null,
        }),
      ),
    );

    renderList();

    expect(await screen.findByText('Room 2C — East Wing')).toBeInTheDocument();
    expect(screen.queryByText('Primary 2C')).not.toBeInTheDocument();
  });
});

describe('ArmList — server order is never re-sorted (TASK-0045 AC)', () => {
  it('renders items in the exact order the server returned, not alphabetically', async () => {
    mockMe('arm.view');
    server.use(
      http.get(apiUrl('/api/v1/arms'), () =>
        HttpResponse.json({
          items: [
            arm({ id: 'a-4', displayName: 'Primary 4A' }),
            arm({ id: 'a-5', displayName: 'Primary 5A' }),
            arm({ id: 'a-10', displayName: 'Primary 10A' }),
            arm({ id: 'a-2', displayName: 'Primary 2A' }),
          ],
          nextCursor: null,
        }),
      ),
    );

    renderList();

    const rows = await screen.findAllByRole('listitem');
    expect(rows.map((row) => row.textContent)).toEqual([
      expect.stringContaining('Primary 4A'),
      expect.stringContaining('Primary 5A'),
      expect.stringContaining('Primary 10A'),
      expect.stringContaining('Primary 2A'),
    ]);
  });
});

describe('ArmList — occupancy (TASK-0045 AC)', () => {
  it('renders capacity alone, never enrolled/capacity or a fabricated 0', async () => {
    mockMe('arm.view');
    server.use(
      http.get(apiUrl('/api/v1/arms'), () => HttpResponse.json({ items: [arm({ capacity: 22 })], nextCursor: null })),
    );

    renderList();

    expect(await screen.findByText(/Capacity 22/)).toBeInTheDocument();
    expect(screen.queryByText(/0\s*\/\s*22/)).not.toBeInTheDocument();
    expect(screen.queryByText(/22\s*\/\s*22/)).not.toBeInTheDocument();
  });
});

describe('ArmList — §8 unknown enum tolerance', () => {
  it('an unrecognised status renders as its raw string, no crash', async () => {
    mockMe('arm.view');
    server.use(
      http.get(apiUrl('/api/v1/arms'), () =>
        HttpResponse.json({ items: [arm({ status: 'SomeFutureArmStatus' })], nextCursor: null }),
      ),
    );

    renderList();

    expect(await screen.findByText(/SomeFutureArmStatus/)).toBeInTheDocument();
  });
});

describe('ArmList — form teacher name resolution (TASK-0045 judgement call)', () => {
  it('never shows the bare id when unresolved — no form teacher assigned', async () => {
    mockMe('arm.view');
    server.use(
      http.get(apiUrl('/api/v1/arms'), () =>
        HttpResponse.json({ items: [arm({ formTeacherAdminId: null })], nextCursor: null }),
      ),
    );

    renderList();

    expect(await screen.findByText('No form teacher assigned')).toBeInTheDocument();
  });

  it('resolves the name when the caller holds admin.view', async () => {
    mockMe('arm.view', 'admin.view');
    server.use(
      http.get(apiUrl('/api/v1/arms'), () =>
        HttpResponse.json({ items: [arm({ formTeacherAdminId: TEACHER_ID })], nextCursor: null }),
      ),
      http.get(apiUrl('/api/v1/admins/:id'), () =>
        HttpResponse.json({
          id: TEACHER_ID,
          staffName: 'Ngozi Adeyemi',
          email: 'ngozi@example.com',
          phone: null,
          status: 'Active',
          isSuperAdmin: false,
          mustChangePassword: false,
          lastLoginAtUtc: null,
          createdAtUtc: new Date().toISOString(),
        }),
      ),
    );

    renderList();

    expect(await screen.findByText('Ngozi Adeyemi')).toBeInTheDocument();
    expect(screen.queryByText(TEACHER_ID)).not.toBeInTheDocument();
  });

  it('renders the gap honestly, never the bare GUID, when the caller lacks admin.view', async () => {
    mockMe('arm.view');
    server.use(
      http.get(apiUrl('/api/v1/arms'), () =>
        HttpResponse.json({ items: [arm({ formTeacherAdminId: TEACHER_ID })], nextCursor: null }),
      ),
    );

    renderList();

    expect(await screen.findByText('Assigned — name not visible to you')).toBeInTheDocument();
    expect(screen.queryByText(TEACHER_ID)).not.toBeInTheDocument();
  });
});
