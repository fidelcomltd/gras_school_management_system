import type { CSSProperties } from "react"

type RevealKind = "rise" | "fade" | "scale"

/**
 * Spread onto any element to animate it in on scroll: `<div {...reveal()}>` or
 * `<li {...reveal("rise", i * 80)}>` for a stagger. Works in server components (it is just
 * attributes); RevealObserver in the root layout does the rest. Respects reduced motion (globals.css).
 */
export function reveal(kind: RevealKind = "rise", delayMs = 0) {
  return {
    "data-reveal": kind,
    ...(delayMs ? { style: { "--reveal-delay": `${delayMs}ms` } as CSSProperties } : {}),
  }
}
