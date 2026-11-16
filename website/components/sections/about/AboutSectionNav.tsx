"use client"

import { useEffect, useRef, useState } from "react"
import type { AboutSectionId } from "@/config/routes"
import { cn } from "@/lib/cn"

type Item = { id: AboutSectionId; label: string; mobileLabel: string }

/**
 * Sticky "On this page" pills under the header. Scroll-spy: the active section is the last one whose
 * top has passed a reading line just below the sticky bars (and the last section once the page is
 * scrolled to the bottom, since a short final section may never reach the line). On phones the active
 * pill scrolls itself into view inside the swipeable strip.
 */
export function AboutSectionNav({ items }: { items: Item[] }) {
  const [active, setActive] = useState<AboutSectionId>(items[0]!.id)
  const stripRef = useRef<HTMLDivElement>(null)
  const barRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const sections = items.map((i) => document.getElementById(i.id)).filter((el): el is HTMLElement => !!el)
    let frame = 0
    const update = () => {
      frame = 0
      const bar = barRef.current?.getBoundingClientRect()
      const line = (bar ? bar.bottom : 0) + 24
      const atBottom = window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2
      let current = sections[0]
      if (atBottom) current = sections[sections.length - 1]
      else for (const s of sections) if (s.getBoundingClientRect().top <= line) current = s
      if (current) setActive(current.id as AboutSectionId)
    }
    const onScroll = () => {
      if (!frame) frame = requestAnimationFrame(update)
    }
    update()
    window.addEventListener("scroll", onScroll, { passive: true })
    window.addEventListener("resize", onScroll)
    return () => {
      window.removeEventListener("scroll", onScroll)
      window.removeEventListener("resize", onScroll)
      if (frame) cancelAnimationFrame(frame)
    }
  }, [items])

  useEffect(() => {
    const strip = stripRef.current
    const pill = strip?.querySelector<HTMLElement>(`[data-id="${active}"]`)
    if (!strip || !pill || strip.scrollWidth <= strip.clientWidth) return
    strip.scrollTo({ left: pill.offsetLeft - 16, behavior: "smooth" })
  }, [active])

  return (
    <div
      ref={barRef}
      className="sticky top-[var(--header-h)] z-30 border-b border-line-soft bg-white/95 backdrop-blur-md"
    >
      <nav aria-label="On this page" className="mx-auto max-w-[1304px]">
        <div
          ref={stripRef}
          className="no-scrollbar flex gap-2 overflow-x-auto px-4 py-3 md:flex-wrap md:justify-center md:gap-2.5 md:overflow-visible md:px-8 md:py-3.5"
        >
          {items.map((item) => {
            const on = item.id === active
            return (
              <a
                key={item.id}
                href={`#${item.id}`}
                data-id={item.id}
                aria-current={on ? "location" : undefined}
                onClick={() => setActive(item.id)}
                className={cn(
                  "inline-flex min-h-10 shrink-0 items-center rounded-full border-[1.5px] px-4 text-sm font-semibold transition-colors duration-200 md:min-h-0 md:px-[18px] md:py-2.5",
                  on
                    ? "border-navy bg-navy text-white"
                    : "border-field-line bg-white text-ink hover:border-crimson hover:text-crimson"
                )}
              >
                <span className="md:hidden">{item.mobileLabel}</span>
                <span className="hidden md:inline">{item.label}</span>
              </a>
            )
          })}
        </div>
      </nav>
    </div>
  )
}
