/// <reference types="vite/client" />

/**
 * Declared shape of the `.env` surface.
 *
 * Keep this in step with `.env.example` and the schema in
 * `src/config/env-values.ts`. This gives compile-time keys; the zod schema
 * gives runtime guarantees. Both are required — neither alone is enough.
 */
interface ImportMetaEnv {
  readonly VITE_APP_NAME: string;
  readonly VITE_APP_ENV: string;
  readonly VITE_API_BASE_URL: string;
  readonly VITE_API_TIMEOUT_MS: string;
  readonly VITE_AUTH_EXPIRY_LEEWAY_SECONDS: string;
  readonly VITE_ENABLE_DEV_TOOLS: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
