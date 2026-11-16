import { aboutSections } from "@/config/routes"
import { site } from "@/config/site"
import { founderBio } from "@/content/about"
import { founderBelief, founderQuote } from "@/content/school"
import { reveal } from "@/components/motion/Reveal"
import { FounderPortrait } from "@/components/cards/FounderPortrait"
import { Container } from "@/components/ui/Container"
import { Eyebrow } from "@/components/ui/Typography"
import { FounderStory } from "@/components/sections/about/FounderStory"

const bodyCopy = "text-[15px] leading-[1.75] text-on-navy-soft lg:text-[16.5px] lg:leading-[1.8]"

/**
 * The proprietress, in full. One DOM for both layouts: on phones the two column wrappers are
 * `display: contents` and `order` interleaves their children (eyebrow → portrait → name → stats →
 * intro → quote → fold); from lg they become real columns. No paragraph is rendered twice.
 */
export function FounderSection() {
  return (
    <section id={aboutSections.founder} className="bg-navy py-12 text-white lg:py-[110px]">
      <Container className="flex flex-col gap-4 lg:grid lg:grid-cols-2 lg:items-start lg:gap-20">
        {/* Left column (lg): portrait + stats */}
        <div className="contents lg:block lg:w-full lg:max-w-[460px] lg:justify-self-center">
          <div className="order-2 lg:order-none" {...reveal("fade")}>
            <FounderPortrait ground="dark" className="h-[420px] md:h-[580px]" />
          </div>
          <dl className="order-4 grid grid-cols-2 gap-2.5 lg:order-none lg:mt-[46px] lg:gap-3" {...reveal("rise", 120)}>
            {founderBio.stats.map((s) => (
              <div
                key={s.value}
                className="flex flex-col-reverse justify-end gap-0.5 rounded-2xl border border-white/12 bg-white/6 p-3.5 lg:gap-1 lg:rounded-[18px] lg:p-[18px]"
              >
                <dt className="text-xs text-on-navy-muted lg:text-[13px]">
                  <span className="lg:hidden">{s.mobileLabel}</span>
                  <span className="hidden lg:inline">{s.label}</span>
                </dt>
                <dd className="font-display text-xl font-extrabold text-gold lg:text-[26px]">{s.value}</dd>
              </div>
            ))}
          </dl>
        </div>

        {/* Right column (lg): the story */}
        <div className="contents lg:flex lg:flex-col lg:gap-[22px]">
          <Eyebrow tone="gold" className="order-1 lg:order-none">
            {founderBio.eyebrow}
          </Eyebrow>
          <div className="order-3 flex flex-col gap-4 lg:order-none lg:gap-[22px]">
            <h2 className="text-[30px] leading-[1.1] font-extrabold text-white lg:text-5xl lg:leading-[1.08]">
              {site.founder.name}
            </h2>
            <span className="text-[15px] font-semibold text-gold-soft lg:text-base">{site.founder.role}</span>
          </div>

          <div className="order-5 flex flex-col gap-4 lg:order-none lg:gap-[22px]">
            <p className="text-base leading-[1.7] text-white lg:text-[19px]">
              <span className="lg:hidden">{founderBio.mobileIntro}</span>
              <span className="hidden lg:inline">{founderBio.intro}</span>
            </p>
            <div className="flex flex-col gap-4 lg:gap-[22px]">
              {/* Phones: quote, then the fold. Desktop: the fold's paragraphs wrap around the quote. */}
              <blockquote className="order-2 my-1 rounded-[18px] border border-gold/35 bg-gold/12 p-[18px] font-display text-[17px] leading-[1.55] font-semibold text-gold-soft lg:order-2 lg:my-2.5 lg:rounded-[22px] lg:px-7 lg:py-[26px] lg:text-[21px]">
                {founderQuote}
              </blockquote>
              <FounderStory
                label={founderBio.mobileMoreLabel}
                className="lg:[&>*]:order-1 lg:[&>*:nth-child(n+3)]:order-3"
              >
                {founderBio.beforeQuote.map((p) => (
                  <p key={p} className={bodyCopy}>
                    {p}
                  </p>
                ))}
                <p className={bodyCopy}>{founderBelief}</p>
                {founderBio.afterQuote.map((p) => (
                  <p key={p} className={bodyCopy}>
                    {p}
                  </p>
                ))}
              </FounderStory>
            </div>
          </div>
        </div>
      </Container>
    </section>
  )
}
