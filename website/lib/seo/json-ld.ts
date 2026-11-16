import type { BreadcrumbList, ContactPage, AboutPage, Person, School, WebPage, WebSite, WithContext } from "schema-dts"
import { routes } from "@/config/routes"
import { site, socialLinks } from "@/config/site"
import { photos } from "@/content/images"
import { services } from "@/content/school"

/**
 * schema.org structured data. One School entity (stable @id) that every page references, so search
 * engines and AI assistants build a single, consistent picture of the school: what it is, where it
 * is, how to reach it, who founded it and what it offers.
 */
const abs = (path: string) => new URL(path, site.url).toString()

export const ids = {
  school: abs("/#school"),
  website: abs("/#website"),
  founder: abs(`${routes.about}#founder`),
} as const

export function schoolJsonLd(): WithContext<School> {
  return {
    "@context": "https://schema.org",
    // School is an EducationalOrganization; Google also reads LocalBusiness-style fields from it.
    "@type": "School",
    "@id": ids.school,
    name: site.name,
    alternateName: [site.initials, site.shortName],
    slogan: site.motto,
    description:
      "Golden Royal Ark School is a crèche, preschool and primary school in Owerri, Imo State, Nigeria, providing quality, child-centred care and education for children aged 6 months to 12 years since 2018.",
    url: site.url,
    logo: abs("/icon.png"),
    image: [abs("/opengraph-image"), abs(photos.morningAssembly.src.src), abs(photos.schoolCelebration.src.src)],
    foundingDate: String(site.foundingYear),
    telephone: site.contact.phone,
    email: site.contact.email,
    address: {
      "@type": "PostalAddress",
      streetAddress: site.address.street,
      addressLocality: site.address.city,
      addressRegion: site.address.region,
      addressCountry: site.address.countryCode,
    },
    ...(site.geo ? { geo: { "@type": "GeoCoordinates", latitude: site.geo.lat, longitude: site.geo.lng } } : {}),
    hasMap: site.maps.directionsUrl,
    areaServed: [
      { "@type": "City", name: "Owerri" },
      { "@type": "State", name: "Imo State" },
    ],
    founder: { "@id": ids.founder },
    contactPoint: {
      "@type": "ContactPoint",
      contactType: "admissions",
      telephone: site.contact.phone,
      email: site.contact.email,
      areaServed: "NG",
      availableLanguage: ["English"],
    },
    hasOfferCatalog: {
      "@type": "OfferCatalog",
      name: "Care and education from 6 months to 12 years",
      itemListElement: services.map((s) => ({
        "@type": "Offer",
        itemOffered: { "@type": "Service", name: s.name, description: `${s.summary} Ages ${s.ages}.` },
      })),
    },
    ...(socialLinks.length ? { sameAs: socialLinks.map((s) => s.href) } : {}),
  }
}

export function websiteJsonLd(): WithContext<WebSite> {
  return {
    "@context": "https://schema.org",
    "@type": "WebSite",
    "@id": ids.website,
    name: site.name,
    alternateName: site.initials,
    url: site.url,
    inLanguage: site.language,
    publisher: { "@id": ids.school },
  }
}

export function founderJsonLd(): WithContext<Person> {
  return {
    "@context": "https://schema.org",
    "@type": "Person",
    "@id": ids.founder,
    name: "Fredrick Juliet",
    honorificPrefix: "Mrs.",
    jobTitle: site.founder.role,
    worksFor: { "@id": ids.school },
    alumniOf: { "@type": "CollegeOrUniversity", name: "Imo State University" },
    hasCredential: {
      "@type": "EducationalOccupationalCredential",
      credentialCategory: "degree",
      name: "Bachelor of Science (B.Sc.) in Education (Agricultural Science)",
    },
    image: abs(photos.proprietress.src.src),
  }
}

type PageKind = "WebPage" | "AboutPage" | "ContactPage"

export function pageJsonLd(
  kind: PageKind,
  { path, name, description }: { path: string; name: string; description: string }
): WithContext<WebPage | AboutPage | ContactPage> {
  return {
    "@context": "https://schema.org",
    "@type": kind,
    "@id": abs(`${path}#webpage`),
    url: abs(path),
    name,
    description,
    inLanguage: site.language,
    isPartOf: { "@id": ids.website },
    about: { "@id": ids.school },
  }
}

export function breadcrumbJsonLd(trail: { name: string; path: string }[]): WithContext<BreadcrumbList> {
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: trail.map((t, i) => ({ "@type": "ListItem", position: i + 1, name: t.name, item: abs(t.path) })),
  }
}
