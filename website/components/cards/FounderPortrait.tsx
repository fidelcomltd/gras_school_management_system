import Image from "next/image"
import type { ReactNode } from "react"
import { photos } from "@/content/images"
import { cn } from "@/lib/cn"

/**
 * The proprietress in an arch (radius 180/240 top, 22/28 bottom) with an offset gold outline.
 * Home and About share it; `className` sets the photo height and `children` overlays a badge.
 */
export function FounderPortrait({
  className,
  ground = "light",
  children,
  priority,
}: {
  className?: string
  ground?: "light" | "dark"
  children?: ReactNode
  priority?: boolean
}) {
  return (
    <div className="relative mt-1 mr-3.5 mb-3.5 md:m-0">
      <div
        aria-hidden="true"
        className="absolute inset-[14px_-14px_-14px_14px] rounded-[180px_180px_22px_22px] border-2 border-gold md:inset-[22px_-22px_-22px_22px] md:rounded-[240px_240px_28px_28px]"
      />
      <div
        className={cn(
          "relative overflow-hidden rounded-[180px_180px_22px_22px] md:rounded-[240px_240px_28px_28px]",
          ground === "light" ? "bg-sky-tint" : "bg-royal",
          className
        )}
      >
        <Image
          src={photos.proprietress.src}
          alt={photos.proprietress.alt}
          fill
          priority={priority}
          placeholder="blur"
          sizes="(min-width: 768px) 460px, 92vw"
          className="object-cover object-[center_20%]"
        />
      </div>
      {children}
    </div>
  )
}
