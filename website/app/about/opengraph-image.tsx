import { ogContentType, ogSize, renderOgImage } from "@/lib/seo/og-image"

export const alt = "About Golden Royal Ark School: our story, mission, values and proprietress"
export const size = ogSize
export const contentType = ogContentType

export default function Image() {
  return renderOgImage({
    eyebrow: "About us · Est. 2018",
    title: "A nurturing, child-centred learning community.",
    photo: "school-celebration",
  })
}
