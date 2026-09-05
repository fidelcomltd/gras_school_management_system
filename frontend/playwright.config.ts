import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright runs against the production build (`vite build` + `vite preview`), not the dev
 * server — a smoke spec is only worth something if it exercises what actually ships. The
 * `webServer` block below builds and serves it, then waits for the port before any spec runs.
 *
 * §7 mandates E2E for auth, the primary create path, and one failure path (frontend spec §12).
 * None of those flows exist yet — TASK-0020 only lands the harness, so `e2e/smoke.spec.ts` is a
 * placeholder assertion against the current shell. TASK-0021 replaces it with the real flows.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : undefined,
  // 'html' would launch and block CI waiting for a browser; 'list' prints progress, 'github'
  // annotates failures inline on the PR diff.
  reporter: process.env.CI ? [['list'], ['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    // 'localhost', not '127.0.0.1': `vite preview` with no --host binds the IPv6 loopback
    // ([::1]) only on this stack, so a literal IPv4 address never connects.
    baseURL: 'http://localhost:4173',
    // Legible failure output in CI per the task card: a trace on the first retry and a
    // screenshot on every failure, both collected into test-results/ and the HTML report.
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run build && npm run preview -- --port 4173 --strictPort',
    url: 'http://localhost:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
