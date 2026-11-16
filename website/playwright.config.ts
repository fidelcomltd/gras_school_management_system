import { defineConfig, devices } from "@playwright/test"

/**
 * E2E against the production build (`npm run build` first; `npm run verify` does both).
 * Reuses a server already running on :3000, otherwise starts `next start`.
 */
export default defineConfig({
  testDir: "tests",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: { baseURL: "http://localhost:3000", trace: "retain-on-failure" },
  projects: [
    // @phone: phone-only UI; @api: browser-independent, so it runs once (desktop).
    {
      name: "desktop",
      grepInvert: /@phone/,
      use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 900 } },
    },
    { name: "phone", grepInvert: /@api/, use: { ...devices["Pixel 7"] } },
  ],
  webServer: {
    command: "npm run start",
    url: "http://localhost:3000",
    reuseExistingServer: true,
    timeout: 120_000,
  },
})
