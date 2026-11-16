import Image from "next/image"
import { aboutSections } from "@/config/routes"
import { passion } from "@/content/about"
import { reveal } from "@/components/motion/Reveal"
import { Container } from "@/components/ui/Container"
import { Eyebrow, Heading } from "@/components/ui/Typography"

/** "What we are passionate about": photo with belief card + seven numbered commitments. */
export function PassionSection() {
  return (
    <section id={aboutSections.passion} className="bg-mist py-11 md:py-[100px]">
      <Container className="grid gap-4 lg:grid-cols-2 lg:grid-rows-[1fr_auto_auto_1fr] lg:gap-x-16 lg:gap-y-0">
        <div className="flex flex-col gap-4 md:gap-[26px] lg:col-start-2 lg:row-start-2" {...reveal()}>
          <Eyebrow>{passion.eyebrow}</Eyebrow>
          <Heading size="sm">{passion.title}</Heading>
        </div>

        <div
          className="relative h-[220px] overflow-hidden rounded-[22px] md:my-6 md:h-[600px] md:rounded-[32px] lg:col-start-1 lg:row-span-4 lg:row-start-1 lg:my-0 lg:self-center"
          {...reveal("fade")}
        >
          <Image
            src={passion.photo.src}
            alt={passion.photo.alt}
            fill
            placeholder="blur"
            sizes="(min-width: 1024px) 600px, 100vw"
            className="object-cover"
          />
          <div className="absolute inset-x-6 bottom-6 hidden flex-col gap-1.5 rounded-[20px] bg-navy/88 px-6 py-[22px] text-white backdrop-blur-sm md:flex">
            <span className="text-xs font-bold tracking-[0.14em] text-gold uppercase">{passion.belief.label}</span>
            <span className="font-display text-xl leading-[1.4] font-bold">{passion.belief.text}</span>
          </div>
        </div>

        <ol className="flex flex-col gap-2 md:gap-2.5 lg:col-start-2 lg:row-start-3 lg:mt-[26px]">
          {passion.items.map((text, i) => (
            <li
              key={text}
              {...reveal("rise", i * 60)}
              className="flex items-start gap-3.5 rounded-[14px] border border-line-cool bg-white px-4 py-3.5 md:items-center md:gap-[18px] md:rounded-2xl md:px-5 md:py-4"
            >
              <span className="w-[22px] shrink-0 pt-px font-display text-sm font-extrabold text-gold-text md:w-[26px] md:pt-0 md:text-[15px]">
                {String(i + 1).padStart(2, "0")}
              </span>
              <span className="text-[15px] leading-normal text-ink md:text-base">{text}</span>
            </li>
          ))}
        </ol>
      </Container>
    </section>
  )
}
