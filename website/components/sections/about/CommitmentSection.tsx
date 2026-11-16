import { routes } from "@/config/routes"
import { site } from "@/config/site"
import { commitment } from "@/content/about"
import { reveal } from "@/components/motion/Reveal"
import { ButtonLink, ResultPortalButton } from "@/components/ui/Button"
import { Waves } from "@/components/ui/Waves"
import { Eyebrow } from "@/components/ui/Typography"

/** Crimson closing statement: "Every child matters." */
export function CommitmentSection() {
  return (
    <section className="relative overflow-hidden bg-crimson">
      <Waves variant="crimson" className="bottom-0" />
      <div
        className="relative mx-auto flex max-w-[1024px] flex-col items-center gap-3.5 px-4 pt-[52px] pb-[76px] text-center md:gap-[22px] md:px-8 md:pt-[100px] md:pb-[120px]"
        {...reveal()}
      >
        <Eyebrow tone="goldSoft">{commitment.eyebrow}</Eyebrow>
        <h2 className="text-[34px] leading-[1.1] font-extrabold text-white md:text-[56px] md:leading-[1.08]">
          {commitment.title}
        </h2>
        <p className="max-w-[760px] text-[15.5px] leading-[1.65] text-rose-soft md:text-[19px] md:leading-[1.7]">
          <span className="md:hidden">{commitment.mobileText}</span>
          <span className="hidden md:inline">{commitment.text}</span>
        </p>
        <span className="mt-2.5 hidden font-display text-lg font-bold text-gold-soft md:block">{site.tagline}</span>
        <div className="mt-2 flex w-full flex-col gap-3.5 md:mt-4 md:w-auto md:flex-row md:flex-wrap md:justify-center">
          <ButtonLink href={routes.contact} variant="white" size="lg" className="min-h-[52px] py-0 md:min-h-0 md:py-4">
            {commitment.primaryCta}
          </ButtonLink>
          <ResultPortalButton variant="outlineLight" size="lg" icon={null} className="hidden md:inline-flex">
            {commitment.secondaryCta} ↗
          </ResultPortalButton>
        </div>
      </div>
    </section>
  )
}
