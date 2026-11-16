import Image from "next/image"
import crest from "@/assets/brand/crest.png"
import { site } from "@/config/site"

type CrestProps = {
  /** Rendered width in CSS px; height follows the crest's aspect ratio. */
  width: number
  className?: string
  /** Decorative crests (next to the school's name) pass "" so screen readers don't repeat it. */
  alt?: string
  priority?: boolean
}

/** The school crest. One source (assets/brand/crest.png, 2× master) for every size on the site. */
export function Crest({ width, className, alt = `${site.name} crest`, priority }: CrestProps) {
  const height = Math.round((width * crest.height) / crest.width)
  return (
    <Image
      src={crest}
      alt={alt}
      width={width}
      height={height}
      priority={priority}
      sizes={`${width}px`}
      className={className}
    />
  )
}
