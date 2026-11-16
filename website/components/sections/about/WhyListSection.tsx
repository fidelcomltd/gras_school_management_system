import { aboutSections, routes } from "@/config/routes"
import { whyAbout } from "@/content/about"
import { reasonsLong } from "@/content/school"
import { reveal } from "@/components/motion/Reveal"
import { ButtonLink } from "@/components/ui/Button"
import { Container } from "@/components/ui/Container"
import { Icon } from "@/components/ui/Icon"
import { Eyebrow, Heading } from "@/components/ui/Typography"

/** About's "Why choose us": a ruled checklist in three columns (one on phones). */
export function WhyListSection() {
  return (
    <section id={aboutSections.why} className="py-11 md:py-[100px]">
      <Container className="flex flex-col gap-3.5 md:gap-11">
        <div className="flex flex-wrap items-end justify-between gap-6" {...reveal()}>
          <div className="flex max-w-[640px] flex-col gap-3.5">
            <Eyebrow>{whyAbout.eyebrow}</Eyebrow>
            <Heading size="sm">
              <span className="md:hidden">{whyAbout.mobileTitle}</span>
              <span className="hidden md:inline">{whyAbout.title}</span>
            </Heading>
          </div>
          <ButtonLink href={routes.contact} variant="primary" className="hidden shadow-none md:inline-flex">
            {whyAbout.cta}
          </ButtonLink>
        </div>
        <ul className="grid md:grid-cols-2 md:gap-x-10 lg:grid-cols-3">
          {reasonsLong.map((label, i) => (
            <li
              key={label}
              {...reveal("rise", (i % 3) * 70)}
              className="flex items-center gap-3.5 border-b border-line py-3.5 md:gap-4 md:py-5"
            >
              <span className="flex size-[30px] shrink-0 items-center justify-center rounded-full bg-gold-tint text-gold-text md:size-9">
                <Icon name="check" size={18} strokeWidth={2.7} className="size-[15px] md:size-[18px]" />
              </span>
              <span className="text-[15.5px] font-semibold text-navy md:text-[17px]">{label}</span>
            </li>
          ))}
        </ul>
      </Container>
    </section>
  )
}
