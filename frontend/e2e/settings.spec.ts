import { expect, test, type Page, type Route } from '@playwright/test';

/**
 * TASK-0041 AC: one Playwright spec covering the full round trip this card
 * adds — sign-in → settings edit → sign-out — through the real back-office
 * shell and its nav, not just the isolated auth pair `e2e/auth.spec.ts`
 * already covers. Stubbed network per that file's own ruling: no real
 * backend, so this suite gains no database dependency.
 */

const SESSION = {
  accountId: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40',
  email: 'admin@example.com',
  staffName: 'Chisom Maxwell',
  isSuperAdmin: false,
  mustChangePassword: false,
  effectivePrivileges: [
    { privilege: 'settings.view', scope: 'SchoolWide', armIds: [] },
    { privilege: 'settings.identity.update', scope: 'SchoolWide', armIds: [] },
  ],
  sessionExpiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
};

const INITIAL_IDENTITY = {
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

async function stubBackOffice(page: Page): Promise<void> {
  let signedIn = false;
  let identity = { ...INITIAL_IDENTITY };

  await page.route('**/api/v1/auth/csrf', (route: Route) =>
    route.fulfill({ json: { csrfToken: 'e2e-csrf-token' } }),
  );

  await page.route('**/api/v1/auth/me', (route: Route) =>
    signedIn
      ? route.fulfill({ json: SESSION })
      : route.fulfill({
          status: 401,
          json: {
            type: 'urn:schoolmanagement:error:authentication.required',
            title: 'Unauthorized',
            detail: 'Not signed in.',
            errorCode: 'authentication.required',
            traceId: 'e2e-trace-id',
          },
        }),
  );

  await page.route('**/api/v1/auth/sign-in', async (route: Route) => {
    signedIn = true;
    await route.fulfill({ json: SESSION });
  });

  await page.route('**/api/v1/auth/sign-out', (route: Route) => {
    signedIn = false;
    return route.fulfill({ status: 204, body: '' });
  });

  await page.route('**/api/v1/settings', (route: Route) => route.fulfill({ json: { identity } }));

  await page.route('**/api/v1/settings/identity', async (route: Route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    identity = { ...identity, ...body, versionNumber: identity.versionNumber + 1 };
    await route.fulfill({ json: identity });
  });
}

test('sign-in, edit school settings from the nav, and sign out round-trips end to end', async ({ page }) => {
  await stubBackOffice(page);
  await page.goto('/sign-in');

  await page.getByLabel('Email address').fill('admin@example.com');
  await page.getByLabel('Password').fill('correct horse battery staple 9');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL('/');

  await page.getByRole('link', { name: 'Settings' }).click();
  await expect(page).toHaveURL(/\/settings$/);
  await expect(page.getByRole('heading', { name: 'School settings' })).toBeVisible();

  const shortName = page.getByLabel('Short name');
  await expect(shortName).toHaveValue('GRAS');
  await shortName.fill('GRA School');
  await page.getByRole('button', { name: 'Save changes' }).click();

  await expect(page.getByText('Settings updated.')).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/sign-in$/);
});
