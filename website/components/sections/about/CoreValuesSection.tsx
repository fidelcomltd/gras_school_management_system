import { aboutSections } from "@/config/routes"
import { valuesIntro } from "@/content/about"
import { coreValues } from "@/content/school"
import { reveal } from "@/components/motion/Reveal"
import { Container } from "@/components/ui/Container"
import { Eyebrow, Heading } from "@/components/ui/Typography"

const letterTone = ["text-crimson", "text-royal", "text-gold-value"] as const

/** The nine values whose initials spell GREATNESS. Cards turn navy and rise on hover. */
export function CoreValuesSection() {
  return (
    <section
      id={aboutSections.values}
      className="relative overflow-hidden bg-sky-tint pt-11 pb-[52px] md:pt-[100px] md:pb-[110px]"
    >
      <svg
        aria-hidden="true"
        viewBox="0 0 1440 120"
        preserveAspectRatio="none"
        className="absolute bottom-0 left-0 hidden h-[120px] w-full md:block"
      >
        <path d="M0 70 C 240 30 480 40 720 72 S 1200 100 1440 60 V120 H0 Z" className="fill-sky-wash" />
      </svg>
      <Container className="relative flex flex-col gap-4 md:gap-[52px]">
        <div className="flex flex-wrap items-end justify-between gap-6" {...reveal()}>
          <div className="flex max-w-[640px] flex-col gap-4 md:gap-3.5">
            <Eyebrow>{valuesIntro.eyebrow}</Eyebrow>
            <Heading size="sm">
              {valuesIntro.titleBefore} <span className="text-crimson">{valuesIntro.highlight}</span>.
            </Heading>
          </div>
          <p className="hidden max-w-[400px] text-base leading-[1.7] text-slate-deep md:block">{valuesIntro.text}</p>
        </div>

        <ul className="mt-1.5 grid grid-cols-3 gap-2 md:mt-0 md:grid-cols-5 md:gap-3 lg:grid-cols-9">
          {coreValues.map((v, i) => (
            <li key={v.name} {...reveal("rise", i * 60)} className="grid">
              <div className="group flex flex-col items-center gap-2 rounded-2xl bg-white px-1.5 pt-4 pb-3.5 text-center shadow-value-sm transition-[translate,background-color,box-shadow] duration-450 ease-[var(--ease-out-soft)] md:gap-3.5 md:rounded-[22px] md:px-3 md:pt-[26px] md:pb-[22px] md:shadow-value md:hover:-translate-y-2.5 md:hover:bg-navy md:hover:shadow-value-hover">
                <span
                  aria-hidden="true"
                  className={`font-display text-[38px] leading-none font-extrabold md:text-6xl ${letterTone[i % 3]}`}
                >
                  {v.letter}
                </span>
                <span className="text-[11.5px] leading-tight font-bold text-navy transition-colors duration-300 md:text-sm md:leading-[1.3] md:group-hover:text-white">
                  {v.name}
                </span>
              </div>
            </li>
          ))}
        </ul>
      </Container>
    </section>
  )
}
