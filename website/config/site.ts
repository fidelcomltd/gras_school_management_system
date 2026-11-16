import { env } from "@/config/env"
import { formatPhone } from "@/lib/phone"

/**
 * The school's identity and every contact detail, in one place. Anything that can differ between
 * environments comes from `config/env.ts`; anything that is simply true about the school is here.
 */
const address = {
  street: env.street,
  city: env.city,
  region: env.region,
  country: "Nigeria",
  countryCode: "NG",
  /** "World Bank Area, Zone E, Owerri, Imo State" */
  oneLine: `${env.street}, ${env.city}, ${env.region}`,
} as const

const mapsQuery = encodeURIComponent(
  env.lat !== undefined && env.lng !== undefined ? `${env.lat},${env.lng}` : address.oneLine
)

export const site = {
  name: "Golden Royal Ark School",
  shortName: "Golden Royal Ark",
  initials: "GRAS",
  motto: "Sailing into Greatness",
  tagline: "Building Future Leaders Through Excellence, Character, and Care.",
  foundingYear: 2018,
  url: env.siteUrl,
  locale: "en_NG",
  language: "en-NG",
  resultPortalUrl: env.resultPortalUrl,
  contact: {
    phone: env.phone,
    phoneDisplay: formatPhone(env.phone),
    phoneHref: `tel:${env.phone}`,
    email: env.email,
    emailHref: `mailto:${env.email}`,
  },
  address,
  geo: env.lat !== undefined && env.lng !== undefined ? { lat: env.lat, lng: env.lng } : undefined,
  maps: {
    directionsUrl: `https://www.google.com/maps/search/?api=1&query=${mapsQuery}`,
    embedUrl: `https://www.google.com/maps?q=${mapsQuery}&z=15&output=embed`,
  },
  socials: {
    tiktok: env.tiktokUrl,
    facebook: env.facebookUrl,
  },
  founder: {
    name: "Mrs. Fredrick Juliet",
    role: "Founder & Proprietress",
    qualification: "B.Sc. (Ed.) Agricultural Science, Imo State University",
  },
} as const

/** Social profiles that are actually configured, for links and schema.org `sameAs`. */
export const socialLinks = (
  [
    { key: "tiktok", label: "TikTok", href: site.socials.tiktok },
    { key: "facebook", label: "Facebook", href: site.socials.facebook },
  ] as const
).filter((s): s is typeof s & { href: string } => Boolean(s.href))

export type SocialKey = "tiktok" | "facebook"
