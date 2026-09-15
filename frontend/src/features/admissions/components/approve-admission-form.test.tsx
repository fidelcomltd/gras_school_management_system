import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ApproveAdmissionForm } from './approve-admission-form';
import { admissionRecord, pupil } from './test-fixtures';

const ARM = { id: 'arm-1', displayName: 'Primary 2C' };

function mockArms() {
  server.use(
    http.get(apiUrl('/api/v1/arms'), () => HttpResponse.json({ items: [ARM], nextCursor: null })),
  );
}

function renderForm(onClose: () => void = () => {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ApproveAdmissionForm pupil={pupil()} record={admissionRecord()} onClose={onClose} />
    </QueryClientProvider>,
  );
}

async function fillAndCheck(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('combobox', { name: 'Arm' }));
  await user.click(await screen.findByRole('option', { name: 'Primary 2C' }));
  await user.click(screen.getByRole('checkbox'));
}

describe('ApproveAdmissionForm — arm selector sourced from the record ids', () => {
  it('queries GET /arms with the record\'s own sessionId and levelId, never a free-text id', async () => {
    let queried: URLSearchParams | undefined;
    server.use(
      http.get(apiUrl('/api/v1/arms'), ({ request }) => {
        queried = new URL(request.url).searchParams;
        return HttpResponse.json({ items: [ARM], nextCursor: null });
      }),
    );

    renderForm();

    // `useArms` fires on mount, independent of whether the dropdown is ever
    // opened — the ids come from `record`, never from a user-typed value.
    await waitFor(() => expect(queried?.get('sessionId')).toBe('session-1'));
    expect(queried?.get('levelId')).toBe('level-1');
  });
});

describe('ApproveAdmissionForm — the confirmation tick gates submit', () => {
  it('Approve is disabled until the tick is checked', async () => {
    mockArms();
    const user = userEvent.setup();
    renderForm();

    await user.click(screen.getByRole('combobox', { name: 'Arm' }));
    await user.click(await screen.findByRole('option', { name: 'Primary 2C' }));

    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();

    await user.click(screen.getByRole('checkbox'));
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled();
  });
});

describe('ApproveAdmissionForm — one Idempotency-Key per dialog opening', () => {
  it('a retry after a dropped response reuses the SAME key, never a fresh one', async () => {
    mockArms();
    const keys: (string | null)[] = [];
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/approve'), ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        // First call drops (transient failure); the form stays open for a
        // retry rather than switching to the success view.
        return keys.length === 1
          ? problemResponse(500)
          : HttpResponse.json({ ...pupil(), status: 'Active', registrationNumber: 'GRAS/2026/0042' });
      }),
    );

    const user = userEvent.setup();
    renderForm();
    await fillAndCheck(user);

    const submit = screen.getByRole('button', { name: 'Approve' });
    await user.click(submit);
    await waitFor(() => expect(keys).toHaveLength(1));

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(keys).toHaveLength(2));

    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
  });
});

describe('ApproveAdmissionForm — success', () => {
  it('shows the issued registration number without navigating away', async () => {
    mockArms();
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/approve'), () =>
        HttpResponse.json({ ...pupil(), status: 'Active', registrationNumber: 'GRAS/2026/0042' }),
      ),
    );
    const onClose = vi.fn();

    const user = userEvent.setup();
    renderForm(onClose);
    await fillAndCheck(user);
    await user.click(screen.getByRole('button', { name: 'Approve' }));

    expect(await screen.findByText('GRAS/2026/0042')).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });
});

describe('ApproveAdmissionForm — server rejections surface verbatim', () => {
  it('409 already decided', async () => {
    mockArms();
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/approve'), () =>
        problemResponse(409, { detail: 'This admission has already been decided.' }),
      ),
    );

    const user = userEvent.setup();
    renderForm();
    await fillAndCheck(user);
    await user.click(screen.getByRole('button', { name: 'Approve' }));

    expect(await screen.findByText('This admission has already been decided.')).toBeInTheDocument();
  });

  it('422 blocking condition (no active term)', async () => {
    mockArms();
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/approve'), () =>
        problemResponse(422, { detail: 'No term is currently active.' }),
      ),
    );

    const user = userEvent.setup();
    renderForm();
    await fillAndCheck(user);
    await user.click(screen.getByRole('button', { name: 'Approve' }));

    expect(await screen.findByText('No term is currently active.')).toBeInTheDocument();
  });
});
