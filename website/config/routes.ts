/**
 * Every internal destination. Components never type a path: they import it from here, so a renamed
 * route is a one-line change and the sitemap can't drift from the navigation.
 */
export const routes = {
  home: "/",
  about: "/about",
  contact: "/contact",
} as const

export type RoutePath = (typeof routes)[keyof typeof routes]

/** Anchors on the About page, shared by its section nav and anything that deep-links into it. */
export const aboutSections = {
  story: "story",
  passion: "passion",
  mission: "mission",
  values: "values",
  founder: "founder",
  why: "why",
} as const

export type AboutSectionId = (typeof aboutSections)[keyof typeof aboutSections]

export const anchors = {
  aboutFounder: `${routes.about}#${aboutSections.founder}`,
  contactMap: `${routes.contact}#map`,
} as const

/** Main navigation, in display order. */
export const mainNav = [
  { label: "Home", href: routes.home },
  { label: "About", href: routes.about },
  { label: "Contact", href: routes.contact },
] as const

/** Sitemap entries. `priority` and `changeFrequency` are hints only. */
export const sitemapRoutes = [
  { path: routes.home, priority: 1, changeFrequency: "monthly" },
  { path: routes.about, priority: 0.8, changeFrequency: "yearly" },
  { path: routes.contact, priority: 0.8, changeFrequency: "yearly" },
] as const
