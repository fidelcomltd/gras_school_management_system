import type { ComponentPropsWithoutRef } from "react"
import { cn } from "@/lib/cn"

type Tone = "crimson" | "gold" | "goldSoft"

const eyebrowTone: Record<Tone, string> = {
  crimson: "text-crimson",
  gold: "text-gold",
  goldSoft: "text-gold-soft",
}

/** Small caps label above a heading: Inter 700, 12–13px, +16% tracking. */
export function Eyebrow({ tone = "crimson", className, ...rest }: ComponentPropsWithoutRef<"span"> & { tone?: Tone }) {
  return (
    <span
      className={cn("text-xs font-bold tracking-[0.16em] uppercase md:text-[13px]", eyebrowTone[tone], className)}
      {...rest}
    />
  )
}

const headingSize = {
  /** Home: 30 → 44px */
  md: "text-[30px] leading-[1.15] md:text-[44px] md:leading-[1.12]",
  /** About / Contact phone artboards use 28px */
  sm: "text-[28px] leading-[1.15] md:text-[44px] md:leading-[1.12]",
} as const

/**
 * Section heading (h2). Jakarta 800, navy. Choose the size with `size`, not with a text-[…] class:
 * tailwind-merge drops leading-* when it merges a font-size class, which loosens the line-height.
 */
export function Heading({
  className,
  size = "md",
  ...rest
}: ComponentPropsWithoutRef<"h2"> & { size?: keyof typeof headingSize }) {
  return <h2 className={cn("font-extrabold text-navy", headingSize[size], className)} {...rest} />
}

/** Body copy: Inter 16–17px, line-height 1.7–1.75, slate. */
export function Lead({ className, ...rest }: ComponentPropsWithoutRef<"p">) {
  return (
    <p className={cn("text-base leading-[1.7] text-slate md:text-[17px] md:leading-[1.75]", className)} {...rest} />
  )
}
