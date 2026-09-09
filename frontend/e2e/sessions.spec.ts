import { expect, test, type Page, type Route } from '@playwright/test';

/**
 * TASK-0042 AC: one Playwright spec covering create-session → open-term
 * end to end through the real nav, mirroring `e2e/settings.spec.ts`'s shape.
 * Stubbed network per that file's own ruling: no real backend.
 */

const SESSION = {
  accountId: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40',
  email: 'admin@example.com',
  staffName: 'Chisom Maxwell',
  isSuperAdmin: false,
  mustChangePassword: false,
  effectivePrivileges: [
    { privilege: 'session.view', scope: 'SchoolWide', armIds: [] },
    { privilege: 'session.create', scope: 'SchoolWide', armIds: [] },
    { privilege: 'term.open', scope: 'SchoolWide', armIds: [] },
  ],
  sessionExpiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
};

function term(id: string, ordinal: number, name: string, startDate: string, endDate: string) {
  return {
    id,
    sessionId: 'session-1',
    ordinal,
    name,
    startDate,
    endDate,
    timesSchoolOpened: null,
    nextResumptionDate: null,
    state: 'Upcoming',
    closedAtUtc: null,
    closedBy: null,
  };
}

async function stubBackOffice(page: Page): Promise<{ getIdempotencyKeySeen: () => string | null }> {
  let signedIn = false;
  let sessions: Array<Record<string, unknown>> = [];
  let detail: Record<string, unknown> | null = null;
  let idempotencyKeySeen: string | null = null;

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

  await page.route('**/api/v1/sessions', async (route: Route) => {
    if (route.request().method() === 'POST') {
      idempotencyKeySeen = route.request().headers()['idempotency-key'] ?? null;
      const body = route.request().postDataJSON() as { name: string };
      detail = {
        id: 'session-1',
        name: body.name,
        startDate: '2027-09-13',
        endDate: '2028-07-23',
        state: 'Upcoming',
        terms: [
          term('term-1', 1, 'First Term', '2027-09-13', '2027-12-17'),
          term('term-2', 2, 'Second Term', '2028-01-03', '2028-04-01'),
          term('term-3', 3, 'Third Term', '2028-04-19', '2028-07-23'),
        ],
      };
      sessions = [{ id: 'session-1', name: body.name, startDate: '2027-09-13', endDate: '2028-07-23', state: 'Upcoming' }];
      return route.fulfill({ status: 201, json: detail });
    }
    return route.fulfill({ json: { items: sessions, nextCursor: null } });
  });

  await page.route('**/api/v1/sessions/session-1', (route: Route) => route.fulfill({ json: detail }));

  await page.route('**/api/v1/terms/term-1/open', (route: Route) => {
    if (!detail) throw new Error('no session created yet');
    const terms = detail['terms'] as Array<Record<string, unknown>>;
    const opened = { ...terms[0], state: 'Active' };
    detail = { ...detail, terms: [opened, terms[1], terms[2]] };
    return route.fulfill({ json: opened });
  });

  return { getIdempotencyKeySeen: () => idempotencyKeySeen };
}

test('create session then open its first term, end to end through the nav', async ({ page }) => {
  const stub = await stubBackOffice(page);
  await page.goto('/sign-in');

  await page.getByLabel('Email address').fill('admin@example.com');
  await page.getByLabel('Password').fill('correct horse battery staple 9');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL('/');

  await page.getByRole('link', { name: 'Sessions' }).click();
  await expect(page).toHaveURL(/\/sessions$/);
  await page.getByRole('button', { name: 'New session' }).click();

  await page.getByLabel('Session name').fill('2027/2028');
  await page.getByLabel('Session start date').fill('2027-09-13');
  await page.getByLabel('Session end date').fill('2028-07-23');
  await page.getByLabel('First term start date').fill('2027-09-13');
  await page.getByLabel('First term end date').fill('2027-12-17');
  await page.getByLabel('Second term start date').fill('2028-01-03');
  await page.getByLabel('Second term end date').fill('2028-04-01');
  await page.getByLabel('Third term start date').fill('2028-04-19');
  await page.getByLabel('Third term end date').fill('2028-07-23');
  await page.getByRole('button', { name: 'Create session' }).click();

  // `Idempotency-Key` reached the real request the real dialog issued (AC).
  // A second, distinct submit never reusing the same key is proven by the
  // unit test `sessions-list-screen.test.tsx` — "sends a fresh Idempotency-Key
  // per submit" — which drives two submits against a mocked 422 and asserts
  // the two keys differ; this spec only needs to prove the header is wired
  // end to end through the real form and the real HTTP client.
  await expect(page.getByRole('link', { name: /2027\/2028/ })).toBeVisible();
  expect(stub.getIdempotencyKeySeen()).not.toBeNull();

  await page.getByRole('link', { name: /2027\/2028/ }).click();

  await expect(page.getByRole('heading', { name: '2027/2028' })).toBeVisible();
  const firstTermCard = page.getByRole('listitem').filter({ hasText: 'First Term' });
  await firstTermCard.getByRole('button', { name: 'Open term' }).click();

  await expect(firstTermCard.getByText('Active')).toBeVisible();
});
