import { expect, test, type Route } from '@playwright/test';

/**
 * Smoke coverage for the shell. TASK-0021 replaces `ScaffoldStatusScreen` with a
 * real, protected `/` — this spec's original assertions (the scaffold heading)
 * break BY DESIGN and are replaced with equivalent ones against what actually
 * renders now, not deleted or weakened. The auth flow itself (successful
 * sign-in, a rejected credential) lives in `e2e/auth.spec.ts`; this file only
 * proves the unauthenticated redirect and the unknown-route fallback, per
 * ruling 7 with no real backend.
 */

test('an unauthenticated visitor to / is redirected to sign-in, not shown a blank page', async ({
  page,
}) => {
  await page.route('**/api/v1/auth/csrf', (route: Route) =>
    route.fulfill({ json: { csrfToken: 'e2e-csrf-token' } }),
  );
  await page.route('**/api/v1/auth/me', (route: Route) =>
    route.fulfill({
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

  await page.goto('/');

  await expect(page).toHaveURL(/\/sign-in$/);
  await expect(page).toHaveTitle('Golden Royal Ark School Portal');
  await expect(page.getByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible();
});

test('unknown route falls back to the not-found screen, not a blank page', async ({ page }) => {
  await page.goto('/this-route-does-not-exist');

  await expect(page.getByRole('heading', { level: 1, name: 'Page not found' })).toBeVisible();
});
