import type { MetadataRoute } from "next"
import { sitemapRoutes } from "@/config/routes"
import { site } from "@/config/site"

/** /sitemap.xml — generated from the route registry, so a new page can't be forgotten. */
export default function sitemap(): MetadataRoute.Sitemap {
  const lastModified = new Date()
  return sitemapRoutes.map((r) => ({
    url: new URL(r.path, site.url).toString(),
    lastModified,
    changeFrequency: r.changeFrequency,
    priority: r.priority,
  }))
}
