import { anchors } from "@/config/routes"
import { site } from "@/config/site"
import { founderTeaser } from "@/content/home"
import { founderBelief, founderQuote } from "@/content/school"
import { reveal } from "@/components/motion/Reveal"
import { FounderPortrait } from "@/components/cards/FounderPortrait"
import { ButtonLink } from "@/components/ui/Button"
import { Container } from "@/components/ui/Container"
import { Eyebrow, Heading, Lead } from "@/components/ui/Typography"

/** "Meet our founder": arched portrait + quote. Phones put the eyebrow above the photo. */
export function FounderTeaserSection() {
  return (
    <section className="bg-white py-14 md:py-[110px]">
      <Container className="grid gap-[18px] lg:grid-cols-2 lg:grid-rows-[1fr_auto_auto_1fr] lg:gap-x-20 lg:gap-y-0">
        <Eyebrow className="lg:col-start-2 lg:row-start-2 lg:self-end">{founderTeaser.eyebrow}</Eyebrow>

        <div
          className="w-full md:mx-auto md:my-6 md:max-w-[460px] lg:col-start-1 lg:row-span-4 lg:row-start-1 lg:my-0 lg:self-center"
          {...reveal("fade")}
        >
          <FounderPortrait className="h-[420px] md:h-[560px]">
            <div className="absolute bottom-4 left-3 flex flex-col rounded-[14px] bg-crimson px-3.5 py-2.5 text-white md:bottom-12 md:-left-7 md:gap-0.5 md:rounded-[18px] md:px-5 md:py-4 md:shadow-badge-crimson">
              <span className="font-display text-xl font-extrabold md:text-[28px]">
                {founderTeaser.experience.value}
              </span>
              <span className="text-xs text-rose md:text-[13px]">{founderTeaser.experience.label}</span>
            </div>
          </FounderPortrait>
        </div>

        <div
          className="flex flex-col gap-[18px] lg:col-start-2 lg:row-start-3 lg:mt-[22px] lg:gap-[22px]"
          {...reveal("rise", 100)}
        >
          <Heading>{site.founder.name}</Heading>
          <span className="text-sm leading-normal font-semibold text-gold-text md:text-base">
            {site.founder.role} · {site.founder.qualification}
          </span>
          <blockquote className="my-1 border-l-4 border-gold pl-[18px] font-display text-lg leading-normal font-semibold text-royal md:my-2 md:py-1 md:pl-[26px] md:text-[23px]">
            {founderQuote}
          </blockquote>
          <Lead className="hidden md:block">{founderBelief}</Lead>
          <ButtonLink
            href={anchors.aboutFounder}
            variant="outline"
            icon="arrowRight"
            className="min-h-12 self-start py-0 md:mt-1.5 md:min-h-0 md:py-[15px]"
          >
            {founderTeaser.cta}
          </ButtonLink>
        </div>
      </Container>
    </section>
  )
}
