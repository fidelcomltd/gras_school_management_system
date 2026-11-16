import type { MetadataRoute } from "next"
import { isProductionSite } from "@/config/env"
import { site } from "@/config/site"

/**
 * /robots.txt. Production welcomes every crawler, including AI assistants' search and answer bots,
 * so the school can be found and cited there too. Previews and branch deploys block everything.
 */
export default function robots(): MetadataRoute.Robots {
  if (!isProductionSite) return { rules: [{ userAgent: "*", disallow: "/" }] }
  return {
    rules: [
      { userAgent: "*", allow: "/", disallow: ["/api/"] },
      {
        userAgent: [
          "GPTBot",
          "OAI-SearchBot",
          "ChatGPT-User",
          "ClaudeBot",
          "Claude-SearchBot",
          "Claude-User",
          "PerplexityBot",
          "Google-Extended",
          "Applebot-Extended",
          "Bingbot",
        ],
        allow: "/",
        disallow: ["/api/"],
      },
    ],
    sitemap: new URL("/sitemap.xml", site.url).toString(),
    host: site.url,
  }
}
