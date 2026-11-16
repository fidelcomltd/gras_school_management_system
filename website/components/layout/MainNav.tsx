"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"
import { mainNav } from "@/config/routes"
import { cn } from "@/lib/cn"

/** Desktop navigation (lg+). The current page is crimson with an underline. */
export function MainNav() {
  const pathname = usePathname()
  return (
    <nav aria-label="Main" className="hidden items-center gap-9 text-[15px] font-semibold lg:flex">
      {mainNav.map((item) => {
        const current = pathname === item.href
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={current ? "page" : undefined}
            className={cn(
              "relative py-2 transition-colors duration-200 hover:text-crimson",
              "after:absolute after:inset-x-0 after:bottom-0 after:h-0.5 after:origin-left after:bg-crimson after:transition-transform after:duration-300",
              current ? "text-crimson after:scale-x-100" : "text-ink after:scale-x-0 hover:after:scale-x-100"
            )}
          >
            {item.label}
          </Link>
        )
      })}
    </nav>
  )
}
