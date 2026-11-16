"use client"

import { useId, useState, type ReactNode } from "react"
import { cn } from "@/lib/cn"
import { Icon } from "@/components/ui/Icon"

/**
 * Phones fold the long part of the biography behind "Read her full story"; from lg it is always
 * open and the toggle disappears. The fold uses `display: contents` on desktop so the paragraphs and
 * the quote can interleave in the parent's flex order (see FounderSection).
 */
export function FounderStory({
  label,
  children,
  className,
}: {
  label: string
  children: ReactNode
  className?: string
}) {
  const [open, setOpen] = useState(false)
  const id = useId()
  return (
    <>
      <button
        type="button"
        aria-expanded={open}
        aria-controls={id}
        onClick={() => setOpen((v) => !v)}
        className="order-4 flex min-h-12 items-center justify-between border-t border-white/14 pt-1 text-[15px] font-bold text-white lg:hidden"
      >
        {label}
        <Icon
          name="chevronDown"
          size={20}
          strokeWidth={2.2}
          className={cn("text-gold transition-transform duration-300", open && "rotate-180")}
        />
      </button>
      <div
        id={id}
        className={cn("order-5 flex-col gap-3.5 lg:contents", open ? "flex animate-pop" : "hidden", className)}
      >
        {children}
      </div>
    </>
  )
}
