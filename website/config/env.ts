/**
 * Public (browser-safe) environment, validated once at import.
 *
 * Each NEXT_PUBLIC_ variable is read by its literal name: Next.js inlines them at build time and only
 * recognises the literal `process.env.NEXT_PUBLIC_X` form. The contract is `.env.example`.
 * Server-only secrets live in `config/env.server.ts`.
 *
 * Deliberately dependency-free: this module is imported by client components, so a schema library
 * here would ship to every visitor. An invalid value fails the build with a clear message.
 */
/** Unset or whitespace-only → undefined; otherwise trimmed. Shared with env.server.ts. */
export const blank = (v: string | undefined) => (v === undefined || v.trim() === "" ? undefined : v.trim())

function fail(name: string, why: string): never {
  throw new Error(`[env] ${name} ${why}. See .env.example.`)
}

export function readUrl(name: string, raw: string | undefined, fallback: string): string {
  const v = blank(raw) ?? fallback
  let u: URL
  try {
    u = new URL(v)
  } catch {
    fail(name, `is not a valid URL ("${v}")`)
  }
  if (u.protocol !== "https:" && u.protocol !== "http:") fail(name, `must be an http(s) URL ("${v}")`)
  return v.replace(/\/$/, "")
}

function optionalUrl(name: string, raw: string | undefined) {
  return blank(raw) === undefined ? undefined : readUrl(name, raw, "")
}

function optionalNumber(name: string, raw: string | undefined) {
  const v = blank(raw)
  if (v === undefined) return undefined
  const n = Number(v)
  return Number.isFinite(n) ? n : fail(name, `must be a number ("${v}")`)
}

export function readEmail(name: string, raw: string | undefined, fallback: string) {
  const v = blank(raw) ?? fallback
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v) ? v : fail(name, `is not a valid email ("${v}")`)
}

const phone = blank(process.env.NEXT_PUBLIC_CONTACT_PHONE) ?? "+2348136475557"
if (!/^\+\d{10,15}$/.test(phone)) fail("NEXT_PUBLIC_CONTACT_PHONE", `must look like +2348136475557 ("${phone}")`)

const siteEnv = blank(process.env.NEXT_PUBLIC_SITE_ENV) ?? "production"
if (siteEnv !== "production" && siteEnv !== "preview") fail("NEXT_PUBLIC_SITE_ENV", 'must be "production" or "preview"')

export const env = {
  siteUrl: readUrl("NEXT_PUBLIC_SITE_URL", process.env.NEXT_PUBLIC_SITE_URL, "https://goldenroyalark.com"),
  siteEnv: siteEnv as "production" | "preview",
  resultPortalUrl: readUrl(
    "NEXT_PUBLIC_RESULT_PORTAL_URL",
    process.env.NEXT_PUBLIC_RESULT_PORTAL_URL,
    "https://results.goldenroyalark.com"
  ),
  phone,
  email: readEmail("NEXT_PUBLIC_CONTACT_EMAIL", process.env.NEXT_PUBLIC_CONTACT_EMAIL, "goldenroyalark@gmail.com"),
  street: blank(process.env.NEXT_PUBLIC_ADDRESS_STREET) ?? "World Bank Area, Zone E",
  city: blank(process.env.NEXT_PUBLIC_ADDRESS_CITY) ?? "Owerri",
  region: blank(process.env.NEXT_PUBLIC_ADDRESS_REGION) ?? "Imo State",
  lat: optionalNumber("NEXT_PUBLIC_GEO_LAT", process.env.NEXT_PUBLIC_GEO_LAT),
  lng: optionalNumber("NEXT_PUBLIC_GEO_LNG", process.env.NEXT_PUBLIC_GEO_LNG),
  tiktokUrl: optionalUrl("NEXT_PUBLIC_TIKTOK_URL", process.env.NEXT_PUBLIC_TIKTOK_URL),
  facebookUrl: optionalUrl("NEXT_PUBLIC_FACEBOOK_URL", process.env.NEXT_PUBLIC_FACEBOOK_URL),
  googleSiteVerification: blank(process.env.NEXT_PUBLIC_GOOGLE_SITE_VERIFICATION),
} as const

export const isProductionSite = env.siteEnv === "production"
