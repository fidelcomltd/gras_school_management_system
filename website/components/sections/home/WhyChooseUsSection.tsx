import { whyIntro } from "@/content/home"
import { reasons, type Reason } from "@/content/school"
import { cn } from "@/lib/cn"
import { reveal } from "@/components/motion/Reveal"
import { Container } from "@/components/ui/Container"
import { Crest } from "@/components/ui/Crest"
import { Icon } from "@/components/ui/Icon"
import { Eyebrow, Heading, Lead } from "@/components/ui/Typography"

const tile: Record<Reason["tone"], string> = {
  crimson: "md:bg-crimson-tint text-crimson",
  royal: "md:bg-royal-tint text-royal",
  gold: "md:bg-gold-tint text-gold-text",
}

/**
 * Nine reasons as icon cards. On phones the last (security) card spans both columns in navy,
 * exactly as the mobile artboard draws it.
 */
export function WhyChooseUsSection() {
  return (
    <section className="py-14 md:py-[104px]">
      <Container className="grid items-start gap-5 lg:grid-cols-2 lg:gap-16">
        <div className="flex flex-col gap-5 lg:max-w-[420px]" {...reveal()}>
          <Eyebrow>{whyIntro.eyebrow}</Eyebrow>
          <Heading>{whyIntro.title}</Heading>
          <Lead className="hidden md:block">{whyIntro.text}</Lead>
          <div className="mt-2.5 hidden items-center gap-[18px] rounded-[22px] bg-navy p-6 text-white md:flex">
            <Crest width={64} alt="" className="shrink-0" />
            <p className="text-[15px] leading-[1.6] text-on-navy">{whyIntro.note}</p>
          </div>
        </div>

        <ul className="grid grid-cols-2 gap-2.5 md:mt-6 md:grid-cols-3 md:gap-4 lg:mt-0 lg:grid-cols-2">
          {reasons.map((r, i) => {
            const last = i === reasons.length - 1
            return (
              <li
                key={r.label}
                {...reveal("rise", (i % 3) * 80)}
                className={cn("grid", last && "col-span-2 md:col-span-1")}
              >
                <div
                  className={cn(
                    "flex flex-col gap-2.5 rounded-[18px] border border-line-cool bg-mist p-4",
                    "md:card-lift md:gap-3.5 md:rounded-[20px] md:p-6",
                    last &&
                      "flex-row items-center gap-3 border-navy bg-navy md:flex-col md:items-start md:border-line-cool md:bg-mist"
                  )}
                >
                  <span
                    className={cn(
                      "flex shrink-0 md:size-12 md:items-center md:justify-center md:rounded-[14px]",
                      tile[r.tone],
                      last && "text-gold md:text-gold-text"
                    )}
                  >
                    <Icon name={r.icon} size={24} />
                  </span>
                  <h3
                    className={cn(
                      "font-sans text-sm leading-[1.35] font-semibold tracking-normal text-navy md:font-display md:text-[17px] md:font-bold md:tracking-[-0.02em]",
                      last && "text-white md:text-navy"
                    )}
                  >
                    {r.label}
                  </h3>
                </div>
              </li>
            )
          })}
        </ul>
      </Container>
    </section>
  )
}
