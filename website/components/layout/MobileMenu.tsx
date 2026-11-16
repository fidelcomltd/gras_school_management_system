"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"
import { useCallback, useEffect, useRef, useState } from "react"
import { mainNav } from "@/config/routes"
import { site } from "@/config/site"
import { cn } from "@/lib/cn"
import { resultPortalLinkProps } from "@/components/ui/Button"
import { Crest } from "@/components/ui/Crest"
import { Icon } from "@/components/ui/Icon"
import { SocialIcons } from "@/components/layout/SocialIcons"

const CLOSE_MS = 220
const stagger = [
  "[animation-delay:0ms]",
  "[animation-delay:60ms]",
  "[animation-delay:120ms]",
  "[animation-delay:180ms]",
]

/**
 * Phone/tablet menu ("Mobile — Menu open" artboard). A modal <dialog>: the browser handles the focus
 * trap, Escape and inert background; we add scroll lock and enter/exit motion.
 */
export function MobileMenu() {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [open, setOpen] = useState(false)
  const [closing, setClosing] = useState(false)
  const pathname = usePathname()

  const close = useCallback(() => {
    const dialog = dialogRef.current
    if (!dialog?.open) return
    setClosing(true)
    window.setTimeout(() => {
      dialog.close()
      setClosing(false)
    }, CLOSE_MS)
  }, [])

  // Keep React state in step with the native dialog (Escape closes it without us).
  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    const onClose = () => setOpen(false)
    const onCancel = (e: Event) => {
      e.preventDefault()
      close()
    }
    dialog.addEventListener("close", onClose)
    dialog.addEventListener("cancel", onCancel)
    return () => {
      dialog.removeEventListener("close", onClose)
      dialog.removeEventListener("cancel", onCancel)
    }
  }, [close])

  useEffect(() => {
    document.documentElement.style.overflow = open ? "hidden" : ""
    return () => {
      document.documentElement.style.overflow = ""
    }
  }, [open])

  // A navigation (including back/forward) always closes the menu.
  useEffect(() => {
    dialogRef.current?.close()
  }, [pathname])

  // Leaving phone width with the menu open (rotating a tablet) closes it.
  useEffect(() => {
    const mq = window.matchMedia("(min-width: 64rem)")
    const onChange = () => mq.matches && dialogRef.current?.close()
    mq.addEventListener("change", onChange)
    return () => mq.removeEventListener("change", onChange)
  }, [])

  const openMenu = () => {
    dialogRef.current?.showModal()
    setOpen(true)
  }

  return (
    <>
      <button
        type="button"
        onClick={openMenu}
        aria-label="Open menu"
        aria-haspopup="dialog"
        aria-expanded={open}
        className="flex size-[46px] items-center justify-center rounded-[14px] bg-field-addon text-navy transition-colors hover:bg-line lg:hidden"
      >
        <Icon name="menu" size={22} strokeWidth={2.2} />
      </button>

      <dialog
        ref={dialogRef}
        aria-label="Menu"
        className={cn(
          "fixed inset-0 m-0 h-dvh max-h-none w-full max-w-none overflow-hidden bg-navy p-0 text-white backdrop:bg-transparent",
          "open:flex open:flex-col",
          closing ? "animate-[menu-out_220ms_ease-in_both]" : "animate-[menu-in_300ms_var(--ease-out-soft)_both]"
        )}
      >
        <svg
          aria-hidden="true"
          viewBox="0 0 390 260"
          preserveAspectRatio="none"
          className="pointer-events-none absolute bottom-0 left-0 h-60 w-full"
        >
          <path d="M0 120 C 70 90 150 92 220 120 S 340 150 390 115 V260 H0 Z" className="fill-navy-raised" />
          <path d="M0 170 C 90 145 170 148 250 172 S 360 190 390 168 V260 H0 Z" className="fill-royal" />
        </svg>

        <div className="relative flex items-center justify-between border-b border-white/10 px-4 py-2.5 md:px-8">
          <span className="flex items-center gap-2.5">
            <Crest width={44} alt={`${site.name} crest`} />
            <span className="font-display text-[15px] font-extrabold">{site.name}</span>
          </span>
          <button
            type="button"
            onClick={close}
            aria-label="Close menu"
            className="flex size-[46px] items-center justify-center rounded-[14px] bg-white/10 text-white transition-colors hover:bg-white/20"
          >
            <Icon name="close" size={22} strokeWidth={2.2} />
          </button>
        </div>

        <nav aria-label="Main" className="relative flex flex-col px-4 pt-7 md:px-8">
          {mainNav.map((item, i) => {
            const current = pathname === item.href
            return (
              <Link
                key={item.href}
                href={item.href}
                onClick={close}
                aria-current={current ? "page" : undefined}
                className={cn(
                  "flex min-h-16 animate-slide-in items-center justify-between border-b border-white/10 font-display text-[30px] font-extrabold transition-colors hover:text-gold-soft",
                  stagger[i],
                  current ? "text-gold" : "text-white"
                )}
              >
                {item.label}
                <Icon name="arrowRight" size={22} />
              </Link>
            )
          })}
        </nav>

        <div className={cn("relative flex animate-slide-in flex-col gap-3 px-4 pt-7 md:px-8", stagger[3])}>
          <a
            {...resultPortalLinkProps}
            className="flex min-h-[54px] items-center justify-center gap-2.5 rounded-full bg-gold text-base font-bold text-navy"
          >
            Check Results
            <Icon name="external" size={18} />
            <span className="sr-only"> (opens the result portal in a new tab)</span>
          </a>
          <a
            href={site.contact.phoneHref}
            className="flex min-h-[54px] items-center justify-center gap-2.5 rounded-full bg-crimson text-base font-semibold text-white"
          >
            <Icon name="phone" size={18} />
            Call {site.contact.phoneDisplay}
          </a>
        </div>

        <div className="relative mt-auto flex flex-col gap-3.5 px-4 pb-[34px] md:px-8">
          <a href={site.contact.emailHref} className="text-[15px] text-on-navy hover:text-white">
            {site.contact.email}
          </a>
          <SocialIcons itemClassName="size-11 bg-white/10" />
          <span className="text-[11.5px] font-bold tracking-[0.16em] text-gold uppercase">{site.motto}</span>
        </div>
      </dialog>
    </>
  )
}
