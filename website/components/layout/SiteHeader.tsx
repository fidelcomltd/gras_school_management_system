import Link from "next/link"
import { routes } from "@/config/routes"
import { site } from "@/config/site"
import { ButtonLink, ResultPortalButton } from "@/components/ui/Button"
import { Container } from "@/components/ui/Container"
import { Crest } from "@/components/ui/Crest"
import { MainNav } from "@/components/layout/MainNav"
import { MobileMenu } from "@/components/layout/MobileMenu"

/** Sticky white header. Desktop (lg+): nav + two actions. Below lg: hamburger → MobileMenu. */
export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b border-line bg-white/95 backdrop-blur-md supports-[backdrop-filter]:bg-white/90">
      <Container className="flex h-[calc(var(--header-h)-1px)] items-center justify-between gap-4 lg:gap-8">
        <Link href={routes.home} className="flex items-center gap-2.5 lg:gap-3.5" aria-label={`${site.name}, home`}>
          <Crest width={58} alt="" priority className="h-auto w-11 lg:w-[58px]" />
          <span className="flex flex-col gap-px lg:gap-0.5">
            <span className="font-display text-[15px] font-extrabold tracking-[-0.01em] text-royal lg:text-[19px]">
              {site.name}
            </span>
            <span className="text-[9.5px] font-bold tracking-[0.16em] text-crimson uppercase lg:text-[11px] lg:tracking-[0.18em]">
              {site.motto}
            </span>
          </span>
        </Link>

        <MainNav />

        <div className="hidden items-center gap-3 lg:flex">
          <ResultPortalButton variant="goldChip" size="sm" iconClassName="size-[15px]">
            Check Results
          </ResultPortalButton>
          <ButtonLink
            href={routes.contact}
            variant="primary"
            size="sm"
            className="px-[22px] py-[13px] shadow-crimson-sm"
          >
            Contact us
          </ButtonLink>
        </div>

        <MobileMenu />
      </Container>
    </header>
  )
}
