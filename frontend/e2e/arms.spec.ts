import { expect, test, type Page, type Route } from '@playwright/test';

/**
 * TASK-0045 AC: one Playwright spec covering bulk-create-preview → commit,
 * end to end through the real nav — proving the UI never fires a non-dryRun
 * `POST /arms/bulk` before the caller has seen and confirmed a preview.
 * Stubbed network, no real backend, per `e2e/settings.spec.ts`'s ruling.
 */

const SESSION = {
  accountId: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40',
  email: 'admin@example.com',
  staffName: 'Chisom Maxwell',
  isSuperAdmin: false,
  mustChangePassword: false,
  effectivePrivileges: [
    { privilege: 'arm.view', scope: 'SchoolWide', armIds: [] },
    { privilege: 'arm.create', scope: 'SchoolWide', armIds: [] },
  ],
  sessionExpiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
};

function arm(id: string, label: string, capacity: number) {
  return {
    id,
    classLevelId: 'level-1',
    classLevel: 'Primary 1',
    sessionId: 'session-1',
    label,
    displayName: `Primary 1${label}`,
    capacity,
    formTeacherAdminId: null,
    status: 'Active',
  };
}

async function stubBackOffice(page: Page): Promise<{ getBulkRequestBodies: () => { dryRun: boolean }[] }> {
  let signedIn = false;
  let arms: Array<Record<string, unknown>> = [];
  const bulkRequestBodies: { dryRun: boolean }[] = [];

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

  await page.route('**/api/v1/sessions', (route: Route) =>
    route.fulfill({
      json: {
        items: [
          {
            id: 'session-1',
            name: '2027/2028',
            startDate: '2027-09-13',
            endDate: '2028-07-23',
            state: 'Upcoming',
            armCount: 0,
          },
        ],
        nextCursor: null,
      },
    }),
  );

  await page.route('**/api/v1/levels', (route: Route) =>
    route.fulfill({
      json: {
        items: [
          {
            id: 'level-1',
            name: 'Primary 1',
            sectionId: 'sec-1',
            section: 'Primary',
            progressionOrder: 1,
            nextLevelId: null,
            isEntryLevel: true,
            isGraduatingLevel: true,
            status: 'Active',
          },
        ],
        nextCursor: null,
      },
    }),
  );

  await page.route('**/api/v1/arms/bulk', async (route: Route) => {
    const body = route.request().postDataJSON() as { dryRun: boolean };
    bulkRequestBodies.push({ dryRun: body.dryRun });
    const created = [arm('arm-a', 'A', 30), arm('arm-b', 'B', 30)];
    if (!body.dryRun) arms = created;
    return route.fulfill({ json: { created } });
  });

  await page.route('**/api/v1/arms', (route: Route) => route.fulfill({ json: { items: arms, nextCursor: null } }));

  return { getBulkRequestBodies: () => bulkRequestBodies };
}

test('bulk-create-preview always precedes commit, proven end to end through the nav', async ({ page }) => {
  const stub = await stubBackOffice(page);
  await page.goto('/sign-in');

  await page.getByLabel('Email address').fill('admin@example.com');
  await page.getByLabel('Password').fill('correct horse battery staple 9');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL('/');

  await page.getByRole('link', { name: 'Arms' }).click();
  await expect(page).toHaveURL(/\/arms$/);
  await expect(page.getByText('No arms found.')).toBeVisible();

  await page.getByRole('button', { name: 'Create arms for session' }).click();
  await page.getByRole('combobox', { name: 'Session' }).click();
  await page.getByRole('option', { name: '2027/2028' }).click();
  await page.getByLabel('Arms to create for Primary 1').fill('2');
  await page.getByRole('button', { name: 'Preview' }).click();

  // The preview is shown, and ONLY a dryRun request has fired so far.
  await expect(page.getByRole('listitem').filter({ hasText: 'Primary 1A' })).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: 'Primary 1B' })).toBeVisible();
  expect(stub.getBulkRequestBodies()).toEqual([{ dryRun: true }]);

  await page.getByRole('button', { name: 'Create 2 arms' }).click();

  // The dialog closes and the list refetches — now showing the real result —
  // proving the second, real request only followed the confirmed preview.
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page.getByRole('list', { name: 'Arms' }).getByText('Primary 1A')).toBeVisible();
  await expect(page.getByRole('list', { name: 'Arms' }).getByText('Primary 1B')).toBeVisible();
  expect(stub.getBulkRequestBodies()).toEqual([{ dryRun: true }, { dryRun: false }]);
});
