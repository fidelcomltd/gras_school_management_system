import { expect, test } from '@playwright/test';

/**
 * Smoke coverage for the shell as it exists today — no feature, form, or auth flow exists yet
 * (that is TASK-0021), so there is nothing to write §7's auth/create/failure-path specs against.
 * This spec still has to assert something real: the page title from `index.html`, the heading
 * text rendered by `ScaffoldStatusScreen`, and the router's catch-all for an unknown path. Any of
 * the three breaking the build or the copy turns this spec red.
 */
test('root route renders the portal shell and its heading', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveTitle('Golden Royal Ark School Portal');
  await expect(page.getByRole('heading', { level: 1, name: 'Foundation is in place' })).toBeVisible();
});

test('unknown route falls back to the not-found screen, not a blank page', async ({ page }) => {
  await page.goto('/this-route-does-not-exist');

  await expect(page.getByRole('heading', { level: 1, name: 'Page not found' })).toBeVisible();
});
