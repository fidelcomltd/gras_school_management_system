import { servicesIntro } from "@/content/home"
import { services } from "@/content/school"
import { reveal } from "@/components/motion/Reveal"
import { ServiceCard } from "@/components/cards/ServiceCard"
import { Container } from "@/components/ui/Container"
import { Icon } from "@/components/ui/Icon"
import { Eyebrow, Heading } from "@/components/ui/Typography"

/** "Our services": a snap-scrolling rail below lg, a three-card grid from lg. */
export function ServicesSection() {
  return (
    <section className="mt-14 bg-mist py-12 md:mt-0 md:py-24">
      <Container className="flex flex-wrap items-end justify-between gap-6 pb-6 lg:pb-[52px]" {...reveal()}>
        <div className="flex max-w-[640px] flex-col gap-3 md:gap-3.5">
          <Eyebrow>{servicesIntro.eyebrow}</Eyebrow>
          <Heading>
            <span className="md:hidden">{servicesIntro.mobileTitle}</span>
            <span className="hidden md:inline">{servicesIntro.title}</span>
          </Heading>
        </div>
        <p className="hidden max-w-[400px] text-base leading-[1.7] text-slate md:block">{servicesIntro.text}</p>
      </Container>

      <div className="mx-auto max-w-[1304px] lg:px-8">
        <ul className="no-scrollbar flex snap-x snap-mandatory scroll-px-4 gap-3.5 overflow-x-auto px-4 pb-1.5 md:scroll-px-8 md:px-8 lg:grid lg:grid-cols-3 lg:gap-7 lg:overflow-visible lg:px-0 lg:pb-0">
          {services.map((s, i) => (
            <li key={s.name} className="shrink-0 snap-start" {...reveal("rise", i * 110)}>
              <ServiceCard service={s} className="h-[400px] w-[300px] lg:h-[500px] lg:w-auto" />
            </li>
          ))}
        </ul>
      </div>

      <p className="flex items-center gap-2 px-4 pt-5 text-sm text-slate md:px-8 lg:hidden">
        <Icon name="arrowRight" size={16} className="text-gold-text" />
        {servicesIntro.swipeHint}
      </p>
    </section>
  )
}
