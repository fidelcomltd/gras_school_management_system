"use client"

import { usePathname } from "next/navigation"
import { useEffect } from "react"

/**
 * One IntersectionObserver for the whole page: any element with `data-reveal` fades/rises in the
 * first time it scrolls into view. Server components opt in with an attribute (see components/motion/
 * Reveal.tsx) and ship no JavaScript of their own. Re-scans on every client navigation.
 */
export function RevealObserver() {
  const pathname = usePathname()

  useEffect(() => {
    const els = Array.from(document.querySelectorAll<HTMLElement>("[data-reveal]:not(.is-revealed)"))
    if (!("IntersectionObserver" in window)) {
      els.forEach((el) => el.classList.add("is-revealed"))
      return
    }
    const io = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (!entry.isIntersecting) continue
          entry.target.classList.add("is-revealed")
          io.unobserve(entry.target)
        }
      },
      { rootMargin: "0px 0px -8% 0px", threshold: 0.12 }
    )
    els.forEach((el) => io.observe(el))
    return () => io.disconnect()
  }, [pathname])

  return null
}
