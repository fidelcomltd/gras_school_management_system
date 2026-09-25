import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';
import { onSessionEnded, setSession } from '@/lib/auth/auth-session';
import { render, screen, userEvent } from '@/test/render';
import { apiUrl, http, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ChangePasswordForm } from './change-password-form';

function renderForm() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ChangePasswordForm />
    </QueryClientProvider>,
  );
}

async function submit(user: ReturnType<typeof userEvent.setup>, current: string, next: string) {
  await user.type(screen.getByLabelText('Current password'), current);
  await user.type(screen.getByLabelText('New password'), next);
  await user.type(screen.getByLabelText('Confirm new password'), next);
  await user.click(screen.getByRole('button', { name: 'Change password' }));
}

describe('ChangePasswordForm', () => {
  it('shows a wrong current password on its field and keeps the user signed in', async () => {
    const onEnded = vi.fn();
    onSessionEnded(onEnded);
    // A live session, or "still signed in" would hold vacuously.
    setSession({
      accountId: 'acc-1',
      email: 'admin@example.com',
      staffName: 'Chisom Maxwell',
      isSuperAdmin: true,
      mustChangePassword: true,
      effectivePrivileges: [],
      sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
    });
    server.use(
      http.post(apiUrl('/api/v1/auth/password'), () =>
        problemResponse(401, { errorCode: 'auth.current_password_incorrect', detail: 'Your current password is not correct.' }),
      ),
    );
    const user = userEvent.setup();
    renderForm();

    await submit(user, 'not-my-password-1', 'a much better pass 9');

    expect(await screen.findByText('Your current password is not correct.')).toBeInTheDocument();
    expect(onEnded).not.toHaveBeenCalled();
  });

  it('maps a reused password (422 keyed NewPassword) onto the new-password field', async () => {
    server.use(
      http.post(apiUrl('/api/v1/auth/password'), () =>
        problemResponse(422, { errors: { NewPassword: ['Choose a password you have not used recently.'] } }),
      ),
    );
    const user = userEvent.setup();
    renderForm();

    await submit(user, 'temporary-pass-1', 'an old password 7');

    expect(await screen.findByText('Choose a password you have not used recently.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('refuses a new password that breaks the composition rule before calling the server', async () => {
    let called = false;
    server.use(
      http.post(apiUrl('/api/v1/auth/password'), () => {
        called = true;
        return problemResponse(500);
      }),
    );
    const user = userEvent.setup();
    renderForm();

    await submit(user, 'temporary-pass-1', 'nodigitshere');

    expect(await screen.findByText('Include at least one digit.')).toBeInTheDocument();
    expect(called).toBe(false);
  });
});
