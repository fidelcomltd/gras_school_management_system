import { routes } from "@/config/routes"
import { site } from "@/config/site"
import { homeCta } from "@/content/home"
import { reveal } from "@/components/motion/Reveal"
import { ButtonAnchor, ButtonLink } from "@/components/ui/Button"
import { Container } from "@/components/ui/Container"
import { Waves } from "@/components/ui/Waves"

/** Crimson closing band: message the school, or call it (call button from tablet up). */
export function HomeCtaSection() {
  return (
    <section className="relative overflow-hidden bg-crimson">
      <Waves variant="crimson" className="bottom-0" />
      <Container
        className="relative flex flex-wrap items-center justify-between gap-5 pt-12 pb-16 md:gap-8 md:py-[84px]"
        {...reveal()}
      >
        <div className="flex max-w-[680px] flex-col gap-3.5">
          <h2 className="text-[28px] leading-[1.18] font-extrabold text-white md:text-[42px] md:leading-[1.15]">
            {homeCta.title}
          </h2>
          <p className="text-[15.5px] leading-[1.6] text-rose md:text-lg md:leading-[1.65]">{homeCta.text}</p>
        </div>
        <div className="flex w-full flex-col gap-3.5 md:w-auto md:flex-row md:flex-wrap">
          <ButtonLink href={routes.contact} variant="white" size="lg" className="min-h-[52px] md:min-h-0">
            {homeCta.cta}
          </ButtonLink>
          <ButtonAnchor
            href={site.contact.phoneHref}
            variant="outlineLight"
            size="lg"
            icon="phone"
            iconFirst
            iconClassName="size-[17px] text-gold-soft"
            className="hidden md:inline-flex"
          >
            {site.contact.phoneDisplay}
          </ButtonAnchor>
        </div>
      </Container>
    </section>
  )
}
