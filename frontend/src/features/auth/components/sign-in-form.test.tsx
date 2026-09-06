import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, userEvent } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { SignInForm } from './sign-in-form';

/**
 * `SignInForm` uses `useNavigate`, so it needs real routing context — a second
 * route renders a marker so "did it navigate to /" is an assertion on the DOM,
 * not on a mocked hook.
 */
function renderSignInForm() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/sign-in']}>
        <Routes>
          <Route path="/sign-in" element={<SignInForm />} />
          <Route path="/" element={<p>landing reached</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const SESSION = {
  accountId: 'acc-1',
  email: 'admin@example.com',
  staffName: 'Chisom Maxwell',
  isSuperAdmin: true,
  mustChangePassword: false,
  effectivePrivileges: [],
  sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
  sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
};

describe('SignInForm', () => {
  it('shows client-side validation before any request is sent', async () => {
    const user = userEvent.setup();
    let called = false;
    server.use(
      http.post(apiUrl('/api/v1/auth/sign-in'), () => {
        called = true;
        return HttpResponse.json(SESSION);
      }),
    );

    renderSignInForm();
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
    expect(called).toBe(false);
  });

  it('navigates to the landing route on a successful sign-in', async () => {
    const user = userEvent.setup();
    server.use(http.post(apiUrl('/api/v1/auth/sign-in'), () => HttpResponse.json(SESSION)));

    renderSignInForm();
    await user.type(screen.getByLabelText('Email address'), 'admin@example.com');
    await user.type(screen.getByLabelText('Password'), 'correct horse battery staple 9');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('landing reached')).toBeInTheDocument();
  });

  it('shows one generic message for a wrong password, without naming the field', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(apiUrl('/api/v1/auth/sign-in'), () =>
        problemResponse(401, {
          errorCode: 'auth.invalid_credentials',
          detail: 'Login details are not correct.',
        }),
      ),
    );

    renderSignInForm();
    await user.type(screen.getByLabelText('Email address'), 'admin@example.com');
    await user.type(screen.getByLabelText('Password'), 'wrong password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Login details are not correct.');
  });

  it('maps a 422 onto the matching field', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(apiUrl('/api/v1/auth/sign-in'), () =>
        problemResponse(422, { errors: { Email: ['Enter a valid email address.'] } }),
      ),
    );

    renderSignInForm();
    // A syntactically valid address so the client-side zod check passes and the
    // request actually reaches the (stubbed) server.
    await user.type(screen.getByLabelText('Email address'), 'admin@example.test');
    await user.type(screen.getByLabelText('Password'), 'correct horse battery staple 9');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
  });
});
