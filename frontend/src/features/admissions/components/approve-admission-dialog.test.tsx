import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ApproveAdmissionDialog } from './approve-admission-dialog';
import { admissionRecord, pupil } from './test-fixtures';

function renderDialog() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ApproveAdmissionDialog pupil={pupil()} onClose={() => {}} />
    </QueryClientProvider>,
  );
}

/**
 * TASK-0066 unblocked this dialog: it now fetches `GET /admissions/{id}`
 * first (for `sessionId`/`classAdmittedInto`), so it carries its own four
 * required states for THAT fetch, mirroring `ArmDetailScreen`'s shape. The
 * form itself (arm selector, tick gating, idempotency, success, errors) is
 * `approve-admission-form.test.tsx`, once the record has loaded.
 */
describe('ApproveAdmissionDialog — four required states (GetAdmissionRecord)', () => {
  it('loading, then the form appears', async () => {
    server.use(http.get(apiUrl('/api/v1/admissions/:id'), () => HttpResponse.json(admissionRecord())));

    renderDialog();

    expect(screen.getByText('Loading application…')).toBeInTheDocument();
    expect(await screen.findByRole('combobox', { name: 'Arm' })).toBeInTheDocument();
  });

  it('unauthorized renders nothing', async () => {
    server.use(http.get(apiUrl('/api/v1/admissions/:id'), () => problemResponse(401)));

    const { container } = renderDialog();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('error shows a retry', async () => {
    server.use(http.get(apiUrl('/api/v1/admissions/:id'), () => problemResponse(500)));

    renderDialog();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });

  it('404 (no longer pending) surfaces the server message', async () => {
    server.use(
      http.get(apiUrl('/api/v1/admissions/:id'), () =>
        problemResponse(404, { detail: 'This admission is no longer pending.' }),
      ),
    );

    renderDialog();

    expect(await screen.findByText('This admission is no longer pending.')).toBeInTheDocument();
  });
});
