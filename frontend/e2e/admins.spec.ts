import { expect, test, type Page, type Route } from '@playwright/test';

/**
 * TASK-0043 AC: one Playwright spec covering create-admin → reveal temp
 * password → suspend, end to end through the real nav, mirroring
 * `e2e/sessions.spec.ts`'s shape. Stubbed network per `e2e/settings.spec.ts`'s
 * own ruling: no real backend.
 */

const SESSION = {
  accountId: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40',
  email: 'admin@example.com',
  staffName: 'Chisom Maxwell',
  isSuperAdmin: false,
  mustChangePassword: false,
  effectivePrivileges: [
    { privilege: 'admin.view', scope: 'SchoolWide', armIds: [] },
    { privilege: 'admin.create', scope: 'SchoolWide', armIds: [] },
    { privilege: 'admin.suspend', scope: 'SchoolWide', armIds: [] },
  ],
  sessionExpiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
};

const TEMP_PASSWORD = 'not-a-real-password-fixture';

function summaryOf(detail: Record<string, unknown>): Record<string, unknown> {
  const { id, staffName, email, phone, status, isSuperAdmin, mustChangePassword, lastLoginAtUtc, createdAtUtc } =
    detail;
  return { id, staffName, email, phone, status, isSuperAdmin, mustChangePassword, lastLoginAtUtc, createdAtUtc };
}

async function stubBackOffice(page: Page): Promise<void> {
  let signedIn = false;
  let detail: Record<string, unknown> | null = null;

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

  await page.route('**/api/v1/admins', async (route: Route) => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON() as { staffName: string; email: string; phone: string };
      detail = {
        id: 'admin-1',
        staffName: body.staffName,
        email: body.email,
        phone: body.phone,
        status: 'Active',
        isSuperAdmin: false,
        mustChangePassword: true,
        lastLoginAtUtc: null,
        createdAtUtc: '2026-09-08T00:00:00+00:00',
      };
      return route.fulfill({ status: 201, json: { ...detail, temporaryPassword: TEMP_PASSWORD } });
    }
    return route.fulfill({ json: { items: detail ? [summaryOf(detail)] : [], nextCursor: null } });
  });

  await page.route('**/api/v1/admins/admin-1', (route: Route) => route.fulfill({ json: detail }));

  await page.route('**/api/v1/admins/admin-1/status', async (route: Route) => {
    if (!detail) throw new Error('no admin created yet');
    const body = route.request().postDataJSON() as { status: string };
    detail = { ...detail, status: body.status };
    return route.fulfill({ json: detail });
  });
}

test('create admin, reveal the temporary password once, then suspend the account', async ({ page }) => {
  await stubBackOffice(page);
  await page.goto('/sign-in');

  await page.getByLabel('Email address').fill('admin@example.com');
  await page.getByLabel('Password').fill('correct horse battery staple 9');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL('/');

  await page.getByRole('link', { name: 'Admins' }).click();
  await expect(page).toHaveURL(/\/admins$/);
  await page.getByRole('button', { name: 'New admin' }).click();

  await page.getByLabel('Staff name').fill('Ngozi Adeyemi');
  await page.getByLabel('Email').fill('ngozi.adeyemi@example.com');
  await page.getByLabel('Phone').fill('08012345678');
  await page.getByRole('button', { name: 'Create admin' }).click();

  // Revealed exactly once (AC): the password and the "never again" notice.
  await expect(page.getByText(TEMP_PASSWORD)).toBeVisible();
  await expect(page.getByText(/will not be shown again/i)).toBeVisible();
  await page.getByRole('button', { name: 'Done' }).click();

  // Gone once the dialog closes, and the list shows the new account.
  await expect(page.getByText(TEMP_PASSWORD)).toHaveCount(0);
  await expect(page.getByRole('link', { name: /Ngozi Adeyemi/ })).toBeVisible();

  await page.getByRole('link', { name: /Ngozi Adeyemi/ }).click();
  await expect(page.getByRole('heading', { name: 'Ngozi Adeyemi' })).toBeVisible();

  await page.getByRole('button', { name: 'Change status' }).click();
  await page.getByRole('combobox', { name: 'New status' }).click();
  await page.getByRole('option', { name: 'Suspended' }).click();
  await page.getByRole('button', { name: 'Change status' }).click();
  // Base UI keeps the dialog (and its Select popup) mounted through its exit
  // transition — wait for it to fully detach before querying by plain text,
  // otherwise the closed popup's own leftover "Suspended" node makes the
  // page's `getByText` ambiguous.
  await page.getByRole('dialog').waitFor({ state: 'detached' });

  await expect(page.getByRole('definition').filter({ hasText: 'Suspended' })).toBeVisible();
  // Never reappears anywhere on the page, including this later screen.
  await expect(page.getByText(TEMP_PASSWORD)).toHaveCount(0);
});
