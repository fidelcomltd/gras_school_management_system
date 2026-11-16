import Link from "next/link"
import { routes } from "@/config/routes"
import { cn } from "@/lib/cn"

const tones = {
  /** On navy (About hero) */
  dark: { link: "text-on-navy-dim hover:text-white", current: "text-gold-soft", sep: "text-on-navy-dim" },
  /** On sky tint (Contact header) */
  light: { link: "text-slate hover:text-navy", current: "font-semibold text-crimson", sep: "text-slate" },
} as const

/** "Home / Current page". The JSON-LD BreadcrumbList is emitted by the page, not here. */
export function Breadcrumb({
  current,
  tone,
  className,
}: {
  current: string
  tone: keyof typeof tones
  className?: string
}) {
  const t = tones[tone]
  return (
    <nav aria-label="Breadcrumb" className={cn("text-[13px] md:text-sm", className)}>
      <ol className="flex items-center gap-2 md:gap-2.5">
        <li>
          <Link href={routes.home} className={cn("transition-colors", t.link)}>
            Home
          </Link>
        </li>
        <li aria-hidden="true" className={t.sep}>
          /
        </li>
        <li aria-current="page" className={t.current}>
          {current}
        </li>
      </ol>
    </nav>
  )
}
