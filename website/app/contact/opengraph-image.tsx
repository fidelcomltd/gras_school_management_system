import { ogContentType, ogSize, renderOgImage } from "@/lib/seo/og-image"

export const alt = "Contact Golden Royal Ark School in Owerri"
export const size = ogSize
export const contentType = ogContentType

export default function Image() {
  return renderOgImage({
    eyebrow: "Contact us",
    title: "We'd love to hear from you.",
    photo: "toddlers-lawn",
  })
}
