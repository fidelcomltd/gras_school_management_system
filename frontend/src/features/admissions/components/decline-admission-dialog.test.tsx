import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { apiUrl, http, problemResponse, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { DeclineAdmissionDialog } from './decline-admission-dialog';
import { pupil } from './test-fixtures';

function renderDialog(onClose: () => void = () => {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <DeclineAdmissionDialog pupil={pupil()} onClose={onClose} />
    </QueryClientProvider>,
  );
}

describe('DeclineAdmissionDialog — reason required', () => {
  it('shows a validation message and never calls the endpoint when reason is empty', async () => {
    let called = false;
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/decline'), () => {
        called = true;
        return HttpResponse.json({ ...pupil(), status: 'Declined' });
      }),
    );

    const user = userEvent.setup();
    renderDialog();

    await user.click(screen.getByRole('button', { name: 'Decline application' }));

    expect(await screen.findByText('Give a reason for declining this application.')).toBeInTheDocument();
    expect(called).toBe(false);
  });
});

describe('DeclineAdmissionDialog — success and errors', () => {
  it('closes on success', async () => {
    let closed = false;
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/decline'), () => HttpResponse.json({ ...pupil(), status: 'Declined' })),
    );

    const user = userEvent.setup();
    renderDialog(() => {
      closed = true;
    });

    await user.type(screen.getByLabelText('Reason'), 'Family relocated before the intake began.');
    await user.click(screen.getByRole('button', { name: 'Decline application' }));

    await waitFor(() => expect(closed).toBe(true));
  });

  it('surfaces a 422 (blocking condition) verbatim', async () => {
    server.use(
      http.post(apiUrl('/api/v1/admissions/:id/decline'), () =>
        problemResponse(422, { detail: 'This admission is not pending and cannot be declined.' }),
      ),
    );

    const user = userEvent.setup();
    renderDialog();

    await user.type(screen.getByLabelText('Reason'), 'Family relocated before the intake began.');
    await user.click(screen.getByRole('button', { name: 'Decline application' }));

    expect(
      await screen.findByText('This admission is not pending and cannot be declined.'),
    ).toBeInTheDocument();
  });
});
