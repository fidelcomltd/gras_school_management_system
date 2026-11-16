import Link from "next/link"
import { anchors, mainNav } from "@/config/routes"
import { site } from "@/config/site"
import { services } from "@/content/school"
import { resultPortalLinkProps } from "@/components/ui/Button"
import { Container } from "@/components/ui/Container"
import { Crest } from "@/components/ui/Crest"
import { SocialIcons } from "@/components/layout/SocialIcons"

const footerLabel: Record<string, string> = { About: "About us" }
const columnTitle = "text-xs font-bold tracking-[0.14em] text-gold uppercase"
const link = "text-on-navy transition-colors duration-200 hover:text-white"

/** Navy footer. Four columns on desktop; on phones the "Home — Mobile" stacked layout. */
export function SiteFooter() {
  const year = new Date().getFullYear()
  return (
    <footer className="bg-navy text-on-navy-muted">
      <Container className="grid gap-7 pt-11 pb-0 md:grid-cols-2 md:gap-10 md:pt-[72px] md:pb-10 lg:grid-cols-4">
        <div className="flex items-center gap-3 md:flex-col md:items-start md:gap-[18px]">
          <div className="flex items-center gap-3.5">
            <Crest width={64} alt={`${site.name} crest`} className="h-auto w-[54px] md:w-16" />
            <span className="hidden font-display text-lg leading-tight font-extrabold text-white md:block">
              Golden Royal
              <br />
              Ark School
            </span>
          </div>
          <span className="flex flex-col gap-1 md:gap-0">
            <span className="font-display text-[17px] font-extrabold text-white md:hidden">{site.name}</span>
            <p className="text-[12.5px] leading-normal md:text-sm md:leading-[1.7]">{site.tagline}</p>
          </span>
        </div>

        <nav aria-label="Footer" className="grid grid-cols-2 gap-3 text-[15px] md:flex md:flex-col">
          <span className={`${columnTitle} hidden md:block`}>Explore</span>
          {mainNav.map((item) => (
            <Link key={item.href} href={item.href} className={`${link} min-h-8 md:min-h-0`}>
              {footerLabel[item.label] ?? item.label}
            </Link>
          ))}
          <a {...resultPortalLinkProps} className={`${link} min-h-8 md:min-h-0`}>
            Result Portal ↗<span className="sr-only"> (opens in a new tab)</span>
          </a>
        </nav>

        <div className="hidden flex-col gap-3 text-[15px] md:flex">
          <span className={columnTitle}>Our services</span>
          {services.map((s) => (
            <span key={s.name} className="text-on-navy">
              {s.name} · {s.ages}
            </span>
          ))}
        </div>

        <div className="flex flex-col gap-2.5 text-[15px] md:gap-3">
          <span className={`${columnTitle} hidden md:block`}>Get in touch</span>
          <a
            href={site.contact.phoneHref}
            className="text-white transition-colors hover:text-gold-soft md:text-on-navy"
          >
            {site.contact.phoneDisplay}
          </a>
          <a
            href={site.contact.emailHref}
            className="text-white transition-colors hover:text-gold-soft md:text-on-navy"
          >
            {site.contact.email}
          </a>
          <Link href={anchors.contactMap} className={`${link} leading-normal`}>
            {site.address.street},<br className="hidden md:block" /> {site.address.city}, {site.address.region}
          </Link>
          <SocialIcons className="mt-1.5" itemClassName="size-11 md:size-10" />
        </div>
      </Container>

      <div className="md:border-t md:border-white/10">
        <Container className="pb-11 md:pb-0">
          <div className="mt-7 flex flex-col gap-1.5 border-t border-white/10 pt-[18px] text-[12.5px] text-on-navy-faint md:mt-0 md:flex-row md:flex-wrap md:justify-between md:gap-3 md:border-t-0 md:py-[22px] md:text-[13px]">
            <span>
              © {year} {site.name}
              <span className="hidden md:inline">. All rights reserved.</span>
            </span>
            <span className="font-semibold tracking-[0.12em] text-gold uppercase">{site.motto}</span>
          </div>
        </Container>
      </div>
    </footer>
  )
}
