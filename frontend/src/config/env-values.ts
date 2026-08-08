import { z } from 'zod';

/**
 * The single point in the application that reads `import.meta.env`.
 *
 * Everything else imports the typed constants below. A direct
 * `import.meta.env` read anywhere outside this file is a review blocker —
 * it dodges validation and hides configuration in the component tree.
 *
 * Validation runs at module load, so a missing or malformed variable fails
 * loudly at boot rather than as a confusing runtime error three screens in.
 */

const booleanFromString = z
  .enum(['true', 'false'])
  .transform((value) => value === 'true');

const envSchema = z.object({
  VITE_APP_NAME: z.string().min(1),
  VITE_APP_ENV: z.enum(['development', 'staging', 'production']),
  VITE_API_BASE_URL: z.url({ message: 'must be an absolute URL, e.g. https://api.example.com' }),
  VITE_API_TIMEOUT_MS: z.coerce.number().int().positive().max(120_000),
  /**
   * How far ahead of a token's real expiry we treat it as already expired.
   * Absorbs clock skew and in-flight request latency so a request is never
   * sent with a credential that dies mid-flight.
   */
  VITE_AUTH_EXPIRY_LEEWAY_SECONDS: z.coerce.number().int().nonnegative().max(600),
  VITE_ENABLE_DEV_TOOLS: booleanFromString,
});

export type AppEnvironment = z.infer<typeof envSchema>['VITE_APP_ENV'];

function readEnv(source: ImportMetaEnv): z.infer<typeof envSchema> {
  const parsed = envSchema.safeParse(source);

  if (!parsed.success) {
    const detail = parsed.error.issues
      .map((issue) => `  - ${issue.path.join('.')}: ${issue.message}`)
      .join('\n');

    throw new Error(
      `Invalid environment configuration.\n${detail}\n\n` +
        'Copy .env.example to .env and fill in the missing values.',
    );
  }

  return parsed.data;
}

const env = readEnv(import.meta.env);

export const APP_NAME = env.VITE_APP_NAME;
export const APP_ENV = env.VITE_APP_ENV;
export const API_BASE_URL = env.VITE_API_BASE_URL.replace(/\/+$/, '');
export const API_TIMEOUT_MS = env.VITE_API_TIMEOUT_MS;
export const AUTH_EXPIRY_LEEWAY_MS = env.VITE_AUTH_EXPIRY_LEEWAY_SECONDS * 1000;
export const ENABLE_DEV_TOOLS = env.VITE_ENABLE_DEV_TOOLS;

export const IS_PRODUCTION = APP_ENV === 'production';
export const IS_DEVELOPMENT = APP_ENV === 'development';

/** Exported for unit tests only — lets the schema be exercised without a real `.env`. */
export const __testing = { envSchema, readEnv };
