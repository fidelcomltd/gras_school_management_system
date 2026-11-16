import type { MetadataRoute } from "next"
import { routes } from "@/config/routes"
import { site } from "@/config/site"

/** /manifest.webmanifest — lets parents add the school to their home screen. */
export default function manifest(): MetadataRoute.Manifest {
  return {
    name: site.name,
    short_name: site.shortName,
    description: `${site.motto}. Crèche, Preschool and Primary School in ${site.address.city}, ${site.address.region}.`,
    start_url: routes.home,
    display: "standalone",
    background_color: "#ffffff",
    theme_color: "#0e1d47",
    lang: site.language,
    categories: ["education"],
    icons: [
      { src: "/icon-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
      { src: "/icon.png", sizes: "512x512", type: "image/png", purpose: "any" },
      { src: "/icon-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
    ],
  }
}
