import { resultBand } from "@/content/home"
import { reveal } from "@/components/motion/Reveal"
import { ResultPortalButton } from "@/components/ui/Button"
import { Container } from "@/components/ui/Container"
import { Icon } from "@/components/ui/Icon"

/** "Check your child's result online": sky card, gold portal button, and three steps (desktop). */
export function ResultPortalSection() {
  return (
    <section id="results" className="pb-14 md:pb-[110px]">
      <Container>
        <div
          {...reveal("scale")}
          className="relative grid items-center gap-12 overflow-hidden rounded-[26px] bg-sky-tint px-5 pt-7 pb-8 md:rounded-[32px] md:p-14 lg:grid-cols-2"
        >
          <svg
            aria-hidden="true"
            viewBox="0 0 600 200"
            preserveAspectRatio="none"
            className="absolute right-0 bottom-0 hidden h-40 w-3/5 md:block"
          >
            <path d="M0 120 C 100 80 200 80 300 115 S 500 150 600 100 V200 H0 Z" className="fill-sky-soft" />
            <path d="M0 150 C 120 120 240 125 360 150 S 540 170 600 140 V200 H0 Z" className="fill-sky-mid" />
          </svg>
          <svg
            aria-hidden="true"
            viewBox="0 0 390 120"
            preserveAspectRatio="none"
            className="absolute inset-x-0 bottom-0 h-[90px] w-full md:hidden"
          >
            <path d="M0 60 C 70 40 140 42 210 60 S 340 80 390 55 V120 H0 Z" className="fill-sky-soft" />
          </svg>

          <div className="relative flex flex-col gap-4 md:gap-[18px]">
            <span className="inline-flex items-center gap-2 self-start rounded-full bg-white px-3 py-1.5 text-xs font-bold text-royal md:px-3.5 md:py-[7px] md:text-[13px]">
              <Icon name="lock" size={15} className="hidden md:block" />
              {resultBand.badge}
            </span>
            <h2 className="text-[28px] leading-[1.15] font-extrabold text-navy md:text-[40px]">{resultBand.title}</h2>
            <p className="max-w-[480px] text-[15px] leading-[1.65] text-slate-deep md:text-[17px] md:leading-[1.7]">
              <span className="md:hidden">{resultBand.mobileText}</span>
              <span className="hidden md:inline">{resultBand.text}</span>
            </p>
            <ResultPortalButton
              variant="gold"
              size="lg"
              className="min-h-[52px] py-0 shadow-none md:mt-2 md:min-h-0 md:self-start md:py-4 md:shadow-gold"
            >
              {resultBand.cta}
            </ResultPortalButton>
          </div>

          <ol className="relative hidden flex-col gap-3.5 md:flex">
            {resultBand.steps.map((step, i) => (
              <li
                key={step.title}
                {...reveal("rise", 120 + i * 110)}
                className="flex items-center gap-[18px] rounded-[20px] bg-white px-[22px] py-5 shadow-step"
              >
                <span className="flex size-11 shrink-0 items-center justify-center rounded-full bg-navy font-display text-[17px] font-extrabold text-gold">
                  {i + 1}
                </span>
                <span className="flex flex-col gap-0.5">
                  <span className="font-bold text-navy">{step.title}</span>
                  <span className="text-sm text-slate">{step.detail}</span>
                </span>
              </li>
            ))}
          </ol>
        </div>
      </Container>
    </section>
  )
}
