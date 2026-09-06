import { expect, test, type Page, type Route } from '@playwright/test';

/**
 * §7 mandates E2E for auth (first), the primary create path, and one failure
 * path. This is the auth pair — TASK-0021 ruling 7: stub `**\/api/v1/auth/**`
 * with `page.route`, no real backend, so this suite never gains a database
 * dependency. `e2e/smoke.spec.ts` covers the unauthenticated redirect and the
 * unknown-route fallback.
 */

const SESSION = {
  accountId: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40',
  email: 'admin@example.com',
  staffName: 'Chisom Maxwell',
  isSuperAdmin: true,
  mustChangePassword: false,
  effectivePrivileges: [{ privilege: 'admin.view', scope: 'SchoolWide', armIds: [] }],
  // Far enough out that this suite's own run time never crosses the leeway
  // window and triggers a real (stubbed) keepalive mid-test.
  sessionExpiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
};

function problem(errorCode: string, detail: string) {
  return {
    type: `urn:schoolmanagement:error:${errorCode}`,
    title: 'Error',
    detail,
    errorCode,
    traceId: 'e2e-trace-id',
  };
}

/** Wires the auth surface this suite needs; `signedIn` controls what `me` reports. */
async function stubAuth(page: Page, options: { signedIn: boolean; password: string }): Promise<void> {
  let signedIn = options.signedIn;

  await page.route('**/api/v1/auth/csrf', (route: Route) =>
    route.fulfill({ json: { csrfToken: 'e2e-csrf-token' } }),
  );

  await page.route('**/api/v1/auth/me', (route: Route) =>
    signedIn
      ? route.fulfill({ json: SESSION })
      : route.fulfill({ status: 401, json: problem('authentication.required', 'Not signed in.') }),
  );

  await page.route('**/api/v1/auth/sign-in', async (route: Route) => {
    const body = route.request().postDataJSON() as { email: string; password: string };
    if (body.password === options.password) {
      signedIn = true;
      await route.fulfill({ json: SESSION });
    } else {
      // Wrong password and unknown email get the identical body (spec 6.1.11) —
      // this suite only ever sends a wrong password, but the point under test
      // is that the message never varies by account existence.
      await route.fulfill({
        status: 401,
        json: problem('auth.invalid_credentials', 'Login details are not correct.'),
      });
    }
  });

  await page.route('**/api/v1/auth/sign-out', (route: Route) => {
    signedIn = false;
    return route.fulfill({ status: 204, body: '' });
  });
}

test('successful sign-in reaches the landing screen and can sign out again', async ({ page }) => {
  await stubAuth(page, { signedIn: false, password: 'correct horse battery staple 9' });
  await page.goto('/sign-in');

  await page.getByLabel('Email address').fill('admin@example.com');
  await page.getByLabel('Password').fill('correct horse battery staple 9');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL('/');
  await expect(page.getByRole('heading', { name: 'Welcome, Chisom Maxwell' })).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/sign-in$/);
});

test('a wrong password shows a generic error without revealing whether the email exists', async ({
  page,
}) => {
  await stubAuth(page, { signedIn: false, password: 'correct horse battery staple 9' });
  await page.goto('/sign-in');

  await page.getByLabel('Email address').fill('admin@example.com');
  await page.getByLabel('Password').fill('the wrong password entirely');
  await page.getByRole('button', { name: 'Sign in' }).click();

  const error = page.getByRole('alert').filter({ hasText: 'Login details are not correct.' });
  await expect(error).toBeVisible();
  // Still on the sign-in screen — a failed attempt never navigates.
  await expect(page).toHaveURL(/\/sign-in$/);
});
