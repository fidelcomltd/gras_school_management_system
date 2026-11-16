import { ogContentType, ogSize, renderOgImage } from "@/lib/seo/og-image"

export const alt = "Golden Royal Ark School, Owerri: crèche, preschool and primary school"
export const size = ogSize
export const contentType = ogContentType

export default function Image() {
  return renderOgImage({
    eyebrow: "Crèche · Preschool · Primary School",
    title: "Building future leaders through excellence, character and care.",
    photo: "morning-assembly",
  })
}
