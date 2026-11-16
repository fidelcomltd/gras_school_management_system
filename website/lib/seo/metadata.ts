import type { Metadata } from "next"
import { isProductionSite } from "@/config/env"
import { site } from "@/config/site"

type PageMeta = {
  /** Page title; the root layout appends " | Golden Royal Ark School" unless `absoluteTitle`. */
  title: string
  absoluteTitle?: boolean
  /** 140–160 characters: what a parent searching on Google should read. */
  description: string
  path: string
}

/**
 * Per-page metadata with a canonical URL and matching Open Graph / Twitter cards. Share images come
 * from each route's opengraph-image file, which Next attaches automatically.
 */
export function buildMetadata({ title, absoluteTitle, description, path }: PageMeta): Metadata {
  const fullTitle = absoluteTitle ? title : `${title} | ${site.name}`
  return {
    title: absoluteTitle ? { absolute: title } : title,
    description,
    alternates: { canonical: path },
    openGraph: {
      type: "website",
      locale: site.locale,
      siteName: site.name,
      url: path,
      title: fullTitle,
      description,
    },
    twitter: { card: "summary_large_image", title: fullTitle, description },
    robots: isProductionSite
      ? {
          index: true,
          follow: true,
          googleBot: { index: true, follow: true, "max-image-preview": "large", "max-snippet": -1 },
        }
      : { index: false, follow: false },
  }
}
