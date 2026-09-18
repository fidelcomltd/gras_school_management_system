import { expect, test, type Page, type Route } from '@playwright/test';

/**
 * TASK-0042 AC: one Playwright spec covering create-level-by-insert-after,
 * proving the chain actually rewired (the new level appears between its
 * chosen predecessor and what used to be the immediate successor) rather
 * than merely asserting a 201. Stubbed network, no real backend, per
 * `e2e/settings.spec.ts`'s ruling.
 */

const SESSION = {
  accountId: '0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40',
  email: 'admin@example.com',
  staffName: 'Chisom Maxwell',
  isSuperAdmin: false,
  mustChangePassword: false,
  effectivePrivileges: [
    { privilege: 'level.view', scope: 'SchoolWide', armIds: [] },
    { privilege: 'level.create', scope: 'SchoolWide', armIds: [] },
  ],
  sessionExpiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  sessionAbsoluteExpiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
};

function level(id: string, name: string, order: number, nextLevelId: string | null) {
  return {
    id,
    name,
    sectionId: 'sec-1',
    section: 'Primary',
    progressionOrder: order,
    nextLevelId,
    isEntryLevel: order === 1,
    isGraduatingLevel: nextLevelId === null,
    status: 'Active',
  };
}

async function stubBackOffice(page: Page): Promise<{ getIdempotencyKeySeen: () => string | null }> {
  let signedIn = false;
  let idempotencyKeySeen: string | null = null;
  let levels = [level('l-1', 'Primary 1', 1, 'l-2'), level('l-2', 'Primary 2', 2, null)];

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

  await page.route('**/api/v1/sections', (route: Route) =>
    route.fulfill({ json: { sections: [{ id: 'sec-1', name: 'Primary' }] } }),
  );

  await page.route('**/api/v1/levels', async (route: Route) => {
    if (route.request().method() === 'POST') {
      idempotencyKeySeen = route.request().headers()['idempotency-key'] ?? null;
      const body = route.request().postDataJSON() as { name: string; insertAfterLevelId: string | null };
      // The server rewires the chain: the predecessor's `nextLevelId` now
      // points at the new level, which points at whatever the predecessor
      // used to point at.
      const predecessor = levels.find((l) => l.id === body.insertAfterLevelId);
      const newLevel = level('l-new', body.name, 0, predecessor?.nextLevelId ?? null);
      levels = levels
        .map((l) => (l.id === body.insertAfterLevelId ? { ...l, nextLevelId: newLevel.id } : l))
        .concat(newLevel)
        .map((l, index) => ({ ...l, progressionOrder: index + 1 }))
        .sort((a, b) => a.progressionOrder - b.progressionOrder);
      // Re-sort by actual chain order (predecessor, new level, successor):
      // simplest for this two-then-three-level fixture is a fixed re-derive.
      const first = levels.find((l) => l.isEntryLevel) ?? levels[0];
      const ordered: typeof levels = [];
      let current = first;
      while (current) {
        ordered.push(current);
        const next: (typeof levels)[number] | undefined = levels.find((l) => l.id === current?.nextLevelId);
        if (!next) break;
        current = next;
      }
      levels = ordered.map((l, index) => ({ ...l, progressionOrder: index + 1 }));
      return route.fulfill({ status: 201, json: newLevel });
    }
    return route.fulfill({ json: { items: levels, nextCursor: null } });
  });

  return { getIdempotencyKeySeen: () => idempotencyKeySeen };
}

test('creating a level by insert-after rewires the chain, not just a 201', async ({ page }) => {
  const stub = await stubBackOffice(page);
  await page.goto('/sign-in');

  await page.getByLabel('Email address').fill('admin@example.com');
  await page.getByLabel('Password').fill('correct horse battery staple 9');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL('/');

  await page.getByRole('link', { name: 'Classes' }).click();
  await expect(page).toHaveURL(/\/classes$/);
  await expect(page.getByText('Primary 1')).toBeVisible();

  await page.getByRole('button', { name: 'New level' }).click();
  await page.getByLabel('Level name').fill('Primary 1B');
  await page.getByRole('combobox', { name: 'Section' }).click();
  await page.getByRole('option', { name: 'Primary' }).click();
  await page.getByRole('combobox', { name: 'Insert after' }).click();
  await page.getByRole('option', { name: 'Primary 1', exact: true }).click();
  await page.getByRole('button', { name: 'Create level' }).click();

  expect(stub.getIdempotencyKeySeen()).not.toBeNull();

  // Proves the chain actually rewired: the new level lands BETWEEN Primary 1
  // and Primary 2 once the list refetches — the order, not just a 201. Scoped
  // to the levels list itself, not the page's nav (`page.getByRole('list',
  // { name: 'Levels' })`) — the nav also renders `<li>`s.
  const rows = page.getByRole('list', { name: 'Levels' }).getByRole('listitem');
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toContainText('Primary 1');
  await expect(rows.nth(1)).toContainText('Primary 1B');
  await expect(rows.nth(2)).toContainText('Primary 2');
});
