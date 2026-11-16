import Image from "next/image"
import { aboutHero } from "@/content/about"
import { Breadcrumb } from "@/components/ui/Breadcrumb"
import { Container } from "@/components/ui/Container"
import { Crest } from "@/components/ui/Crest"
import { Waves } from "@/components/ui/Waves"

/** Navy page hero with two floating photos (they drift gently on desktop: 7s / 8s, offset). */
export function AboutHero() {
  const { photos } = aboutHero
  return (
    <section className="relative overflow-hidden bg-navy">
      <Container className="grid items-center gap-4 pt-8 pb-[70px] md:gap-16 md:pt-[88px] md:pb-[150px] lg:grid-cols-2">
        <div className="flex flex-col gap-4 md:gap-[22px]">
          <Breadcrumb current={aboutHero.breadcrumb} tone="dark" />
          <h1 className="animate-rise text-4xl leading-[1.06] font-extrabold text-white md:text-6xl md:leading-[1.04]">
            {aboutHero.title}
          </h1>
          <p className="max-w-[540px] animate-rise text-base leading-[1.65] text-on-navy-soft [animation-delay:120ms] md:text-[19px]">
            <span className="md:hidden">{aboutHero.mobileText}</span>
            <span className="hidden md:inline">{aboutHero.text}</span>
          </p>
          <ul className="mt-1.5 hidden animate-rise flex-wrap gap-2.5 [animation-delay:240ms] md:flex">
            {aboutHero.pills.map((p) => (
              <li
                key={p}
                className="rounded-full border border-gold/55 px-3.5 py-2 text-[13px] font-semibold text-gold-soft"
              >
                {p}
              </li>
            ))}
          </ul>
        </div>

        <div className="relative mt-2 h-[260px] md:mt-0 md:h-[470px]">
          <div className="absolute top-0 right-0 h-[230px] w-[62%] overflow-hidden rounded-[22px] md:h-[400px] md:w-[58%] md:animate-float md:rounded-[28px] md:shadow-float-photo">
            <Image
              src={photos.main.src}
              alt={photos.main.alt}
              fill
              priority
              placeholder="blur"
              sizes="(min-width: 1024px) 360px, 62vw"
              className="object-cover"
            />
          </div>
          <div className="absolute top-10 left-0 h-[210px] w-[calc(46%+10px)] overflow-hidden rounded-[20px] border-[5px] border-navy md:top-[70px] md:h-[312px] md:w-[calc(44%+12px)] md:animate-[float_8s_1s_ease-in-out_infinite] md:rounded-3xl md:border-[6px] md:shadow-float-photo">
            <Image
              src={photos.inset.src}
              alt={photos.inset.alt}
              fill
              placeholder="blur"
              sizes="(min-width: 1024px) 280px, 46vw"
              className="object-cover"
            />
          </div>
          <div className="absolute bottom-0 left-[22%] hidden items-center gap-3.5 rounded-[20px] bg-white px-5 py-4 shadow-float-badge md:flex">
            <Crest width={48} alt="" />
            <span className="flex flex-col gap-0.5">
              <span className="font-display text-base font-extrabold text-navy">{aboutHero.badge.title}</span>
              <span className="text-[13px] text-slate">{aboutHero.badge.subtitle}</span>
            </span>
          </div>
        </div>
      </Container>
      <Waves variant="hero" ribbonOpacity={0.4} />
    </section>
  )
}
