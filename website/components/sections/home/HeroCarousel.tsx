"use client"

import Image from "next/image"
import { useCallback, useEffect, useRef, useState, type CSSProperties } from "react"
import { routes } from "@/config/routes"
import type { HeroSlide } from "@/content/home"
import { cn } from "@/lib/cn"
import { useHydrated } from "@/hooks/useHydrated"
import { usePrefersReducedMotion } from "@/hooks/usePrefersReducedMotion"
import { ButtonLink, ResultPortalButton } from "@/components/ui/Button"
import { Icon } from "@/components/ui/Icon"
import { Waves } from "@/components/ui/Waves"

const AUTOPLAY_MS = 6000
const SWIPE_PX = 48
const pad = (n: number) => String(n).padStart(2, "0")

/**
 * Home hero (design: 4 slides · 6s autoplay, pauses on hover · 1.2s crossfade · 7s slow zoom on the
 * active photo · headline rises in 0.9s, staggered 120ms · dots, counter and arrows).
 * Also pauses on keyboard focus and in background tabs, swipes on touch, and does not autoplay for
 * visitors who prefer reduced motion. Only the first photo loads eagerly (it is the LCP image).
 */
export function HeroCarousel({ slides }: { slides: HeroSlide[] }) {
  const [index, setIndex] = useState(0)
  const [hovered, setHovered] = useState(false)
  const [focused, setFocused] = useState(false)
  const [tabHidden, setTabHidden] = useState(false)
  const mounted = useHydrated()
  const reducedMotion = usePrefersReducedMotion()
  const touchX = useRef<number | null>(null)
  const count = slides.length

  const go = useCallback((i: number) => setIndex(((i % count) + count) % count), [count])
  const playing = !hovered && !focused && !tabHidden && !reducedMotion

  useEffect(() => {
    const onVisibility = () => setTabHidden(document.hidden)
    document.addEventListener("visibilitychange", onVisibility)
    return () => document.removeEventListener("visibilitychange", onVisibility)
  }, [])

  // A timeout keyed on `index` restarts the 6s count whenever the slide changes, however it changed.
  useEffect(() => {
    if (!playing) return
    const t = window.setTimeout(() => go(index + 1), AUTOPLAY_MS)
    return () => window.clearTimeout(t)
  }, [index, playing, go])

  const active = slides[index]!

  return (
    <section
      aria-roledescription="carousel"
      aria-label="Welcome to Golden Royal Ark School"
      className="relative h-[640px] overflow-hidden bg-navy md:h-[720px]"
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      // Keyboard focus pauses; a mouse click or tap focuses the button too, but must not stop autoplay.
      onFocus={(e) => e.target.matches(":focus-visible") && setFocused(true)}
      onBlur={(e) => !e.currentTarget.contains(e.relatedTarget) && setFocused(false)}
      onTouchStart={(e) => (touchX.current = e.touches[0]?.clientX ?? null)}
      onTouchEnd={(e) => {
        const start = touchX.current
        const end = e.changedTouches[0]?.clientX
        touchX.current = null
        if (start == null || end == null || Math.abs(end - start) < SWIPE_PX) return
        go(end < start ? index + 1 : index - 1)
      }}
    >
      {slides.map((slide, i) => {
        const on = i === index
        if (i > 0 && !mounted) return null
        return (
          <div
            key={slide.photo.src.src}
            role="group"
            aria-roledescription="slide"
            aria-label={`${i + 1} of ${count}`}
            aria-hidden={!on}
            className={cn(
              "absolute inset-0 transition-opacity duration-[1200ms] ease-in-out",
              on ? "opacity-100" : "opacity-0"
            )}
          >
            <Image
              src={slide.photo.src}
              alt={slide.photo.alt}
              fill
              priority={i === 0}
              fetchPriority={i === 0 ? "high" : "low"}
              placeholder={i === 0 ? "blur" : "empty"}
              quality={85}
              sizes="100vw"
              style={{ "--pos-m": slide.position.mobile, "--pos-d": slide.position.desktop } as CSSProperties}
              className={cn(
                "object-cover [object-position:var(--pos-m)] md:[object-position:var(--pos-d)]",
                on && "animate-ken-burns"
              )}
            />
          </div>
        )
      })}

      {/* Readability overlays: left-to-right on desktop, bottom-up on phones */}
      <div aria-hidden="true" className="absolute inset-0 hidden fade-hero-x md:block" />
      <div aria-hidden="true" className="absolute inset-x-0 bottom-0 hidden h-[220px] fade-hero-bottom md:block" />
      <div aria-hidden="true" className="absolute inset-0 fade-hero-y md:hidden" />

      <div className="relative mx-auto flex h-full max-w-[1304px] flex-col justify-end px-4 pb-[86px] md:justify-center md:px-8 md:pt-10 md:pb-[196px]">
        <div className="flex max-w-[640px] flex-col gap-4 md:gap-[22px]">
          {/* Stable live region (announced when paused); the keyed child inside re-runs the rise animation. */}
          <div aria-live={playing ? "off" : "polite"} aria-atomic="true">
            <div key={index} className="flex flex-col gap-3.5 md:gap-[22px]">
              <span className="inline-flex animate-rise items-center gap-2 self-start rounded-full border border-gold/60 bg-white/12 px-3 py-1.5 text-[11px] font-bold tracking-[0.08em] text-gold-soft uppercase [--rise-from:22px] md:gap-2.5 md:px-4 md:py-2 md:text-[13px] md:font-semibold md:tracking-[0.06em] md:[--rise-from:26px]">
                <span className="size-1.5 rounded-full bg-gold md:size-[7px]" />
                {active.eyebrow}
              </span>
              <h1 className="animate-rise text-[34px] leading-[1.1] font-extrabold text-white [--rise-from:22px] [animation-delay:120ms] md:text-[58px] md:leading-[1.06] md:[--rise-from:26px]">
                {active.title}
              </h1>
              <p className="hidden max-w-[540px] animate-rise text-[19px] leading-[1.6] text-on-navy-hero [animation-delay:240ms] md:block">
                {active.text}
              </p>
            </div>
          </div>
          <div className="mt-1.5 flex flex-col gap-3.5 md:mt-2 md:flex-row md:flex-wrap">
            <ButtonLink
              href={routes.about}
              variant="primary"
              size="lg"
              icon="arrowRight"
              className="min-h-[52px] shadow-none md:min-h-0 md:shadow-crimson"
            >
              Discover our school
            </ButtonLink>
            <ResultPortalButton
              variant="outlineGold"
              size="lg"
              iconClassName="text-gold size-[17px]"
              className="hidden md:inline-flex"
            >
              Check results
            </ResultPortalButton>
          </div>
        </div>
      </div>

      {/* Controls */}
      <div className="absolute inset-x-0 bottom-[46px] md:bottom-[120px]">
        <div className="mx-auto flex max-w-[1304px] items-center justify-between gap-6 px-4 md:px-8">
          <div className="flex w-full items-center justify-between gap-[18px] md:w-auto md:justify-start">
            <span className="order-2 font-display text-[13px] font-bold text-on-navy md:order-1 md:text-[15px] md:text-white">
              {pad(index + 1)} / {pad(count)}
            </span>
            <div className="order-1 flex items-center gap-1.5 md:order-2 md:gap-2">
              {slides.map((_, i) => (
                <button
                  key={i}
                  type="button"
                  onClick={() => go(i)}
                  aria-label={`Show slide ${i + 1} of ${count}`}
                  aria-current={i === index ? "true" : undefined}
                  className={cn(
                    "h-2 cursor-pointer rounded-full transition-[width,background-color] duration-400 ease-out md:h-2.5",
                    i === index ? "w-7 bg-gold md:w-9" : "w-2 bg-white/55 hover:bg-white/80 md:w-2.5"
                  )}
                />
              ))}
            </div>
          </div>
          <div className="hidden gap-3 md:flex">
            <button
              type="button"
              onClick={() => go(index - 1)}
              aria-label="Previous slide"
              className="flex size-[52px] lift-on-hover cursor-pointer items-center justify-center rounded-full border-[1.5px] border-white/50 bg-navy/35 text-white hover:border-white"
            >
              <Icon name="chevronLeft" size={22} />
            </button>
            <button
              type="button"
              onClick={() => go(index + 1)}
              aria-label="Next slide"
              className="flex size-[52px] lift-on-hover cursor-pointer items-center justify-center rounded-full bg-gold text-navy"
            >
              <Icon name="chevronRight" size={22} />
            </button>
          </div>
        </div>
      </div>

      <Waves variant="hero" ribbonOpacity={0.55} />
    </section>
  )
}
