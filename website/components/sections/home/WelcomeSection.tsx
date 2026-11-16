import Image from "next/image"
import { routes } from "@/config/routes"
import { welcome } from "@/content/home"
import { reveal } from "@/components/motion/Reveal"
import { ButtonLink } from "@/components/ui/Button"
import { Container } from "@/components/ui/Container"
import { Crest } from "@/components/ui/Crest"
import { Icon } from "@/components/ui/Icon"
import { Eyebrow, Heading } from "@/components/ui/Typography"

/**
 * Photo collage + welcome copy. Phones read heading → photos → copy; desktop puts the collage in a
 * left column beside the text. One grid, two placements, so nothing renders twice.
 */
export function WelcomeSection() {
  const { photos } = welcome
  return (
    <section className="pt-14 md:pt-[110px] md:pb-[100px]">
      <Container className="grid gap-[18px] lg:grid-cols-2 lg:grid-rows-[1fr_auto_auto_1fr] lg:gap-x-[72px] lg:gap-y-0">
        <div className="flex flex-col gap-[18px] lg:col-start-2 lg:row-start-2 lg:gap-[22px]" {...reveal()}>
          <Eyebrow>
            <span className="md:hidden">{welcome.mobileEyebrow}</span>
            <span className="hidden md:inline">{welcome.eyebrow}</span>
          </Eyebrow>
          <Heading>{welcome.title}</Heading>
        </div>

        <div
          className="relative my-1.5 mb-2.5 h-[330px] md:my-8 md:h-[560px] lg:col-start-1 lg:row-span-4 lg:row-start-1 lg:my-0"
          {...reveal("fade")}
        >
          <div
            aria-hidden="true"
            className="absolute top-10 -left-6 hidden size-[300px] rounded-full bg-sky-tint md:block"
          />
          <div className="absolute top-0 left-0 h-[280px] w-[72%] zoom-on-hover overflow-hidden rounded-[22px] md:h-[480px] md:w-[74%] md:rounded-[28px] md:shadow-photo">
            <Image
              src={photos.main.src}
              alt={photos.main.alt}
              fill
              sizes="(min-width: 1024px) 440px, 72vw"
              className="object-cover"
              placeholder="blur"
            />
          </div>
          <div className="absolute right-0 bottom-0 h-[212px] w-[calc(48%+12px)] zoom-on-hover overflow-hidden rounded-[20px] border-[6px] border-white shadow-inset-photo md:h-[336px] md:w-[calc(46%+16px)] md:rounded-3xl md:border-8 md:shadow-inset-photo-lg">
            <Image
              src={photos.inset.src}
              alt={photos.inset.alt}
              fill
              sizes="(min-width: 1024px) 280px, 48vw"
              className="object-cover object-[center_30%]"
              placeholder="blur"
            />
          </div>
          <div className="absolute bottom-[30px] left-6 hidden items-center gap-3 rounded-[18px] bg-navy px-5 py-4 text-white shadow-badge md:flex">
            <Crest width={40} alt="" />
            <span className="flex flex-col">
              <span className="text-[15px] font-bold">{welcome.badge.title}</span>
              <span className="text-xs text-on-navy-soft">{welcome.badge.subtitle}</span>
            </span>
          </div>
        </div>

        <div
          className="flex flex-col gap-[18px] lg:col-start-2 lg:row-start-3 lg:mt-[22px] lg:gap-[22px]"
          {...reveal("rise", 120)}
        >
          {welcome.paragraphs.map((p) => (
            <p key={p.desktop} className="text-base leading-[1.7] text-slate md:text-[17px] md:leading-[1.75]">
              <span className="md:hidden">{p.mobile}</span>
              <span className="hidden md:inline">{p.desktop}</span>
            </p>
          ))}
          <ul className="mt-1.5 hidden grid-cols-2 gap-x-6 gap-y-3 md:grid">
            {welcome.qualities.map((q) => (
              <li key={q} className="flex items-start gap-2.5 text-[15px] font-medium text-ink">
                <Icon name="check" size={20} strokeWidth={2.4} className="mt-px shrink-0 text-gold" />
                {q}
              </li>
            ))}
          </ul>
          <ButtonLink
            href={routes.about}
            variant="navy"
            icon="arrowRight"
            className="min-h-12 self-start py-0 md:mt-2.5 md:min-h-0 md:py-[15px]"
          >
            More about us
          </ButtonLink>
        </div>
      </Container>
    </section>
  )
}
