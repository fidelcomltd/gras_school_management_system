import { fileURLToPath } from 'node:url';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    rolldownOptions: {
      output: {
        // Split vendors so an app-code change does not bust the whole cache.
        // Groups are ordered most-specific first; the first match wins.
        codeSplitting: {
          groups: [
            { name: 'vendor-react', test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/ },
            { name: 'vendor-router', test: /node_modules[\\/]react-router/ },
            { name: 'vendor-data', test: /node_modules[\\/](@tanstack|axios|zustand)[\\/]/ },
            { name: 'vendor-ui', test: /node_modules[\\/](@base-ui|@floating-ui)[\\/]/ },
            { name: 'vendor-validation', test: /node_modules[\\/]zod[\\/]/ },
          ],
        },
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: true,
    restoreMocks: true,
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    // Hermetic test environment: the suite must not depend on a developer's
    // untracked `frontend/.env`. These are the same six VITE_* variables
    // env-values.ts validates, given deliberately obvious test values so the
    // suite is unaffected by whether a real `.env` exists (TASK-0023).
    env: {
      VITE_APP_NAME: 'Test Portal',
      VITE_APP_ENV: 'development',
      VITE_API_BASE_URL: 'http://test.invalid',
      VITE_API_TIMEOUT_MS: '5000',
      VITE_AUTH_EXPIRY_LEEWAY_SECONDS: '30',
      VITE_ENABLE_DEV_TOOLS: 'true',
    },
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/**/*.{test,spec}.{ts,tsx}', 'src/test/**', 'src/main.tsx'],
    },
  },
});
