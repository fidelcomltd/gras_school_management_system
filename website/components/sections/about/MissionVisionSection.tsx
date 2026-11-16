import Image from "next/image"
import { aboutSections } from "@/config/routes"
import { missionVision } from "@/content/about"
import { mission, vision } from "@/content/school"
import { cn } from "@/lib/cn"
import { reveal } from "@/components/motion/Reveal"
import { Container } from "@/components/ui/Container"
import { Icon, type IconName } from "@/components/ui/Icon"
import { Eyebrow, Heading } from "@/components/ui/Typography"

function Statement({
  title,
  text,
  icon,
  tone,
}: {
  title: string
  text: string
  icon: IconName
  tone: "crimson" | "navy"
}) {
  const crimson = tone === "crimson"
  return (
    <article
      className={cn(
        "relative flex card-lift flex-col gap-3 overflow-hidden rounded-[22px] px-[22px] py-[26px] text-white md:gap-[18px] md:rounded-[28px] md:p-10",
        crimson ? "bg-crimson" : "bg-navy"
      )}
    >
      <span className="flex items-center gap-3 md:flex-col md:items-start md:gap-[18px]">
        <span
          className={cn(
            "flex md:size-[60px] md:items-center md:justify-center md:rounded-[18px]",
            crimson ? "text-gold-soft md:bg-white/14" : "text-gold md:bg-white/10"
          )}
        >
          <Icon name={icon} size={28} className="size-[26px] md:size-7" />
        </span>
        <h3 className="text-[22px] font-extrabold md:text-[30px]">{title}</h3>
      </span>
      <p
        className={cn(
          "text-[15.5px] leading-[1.65] md:text-lg md:leading-[1.7]",
          crimson ? "text-rose-soft" : "text-on-navy-soft"
        )}
      >
        {text}
      </p>
    </article>
  )
}

/** Mission and vision cards either side of a photo (desktop); stacked cards on phones. */
export function MissionVisionSection() {
  return (
    <section id={aboutSections.mission} className="py-11 md:py-[100px]">
      <Container className="flex flex-col gap-3.5 md:gap-12">
        <div className="flex flex-col gap-3.5 md:items-center md:text-center" {...reveal()}>
          <Eyebrow>{missionVision.eyebrow}</Eyebrow>
          <Heading className="hidden max-w-[720px] md:block">{missionVision.title}</Heading>
        </div>
        <div className="grid items-stretch gap-3.5 md:gap-6 lg:grid-cols-3">
          <div {...reveal("rise", 0)} className="grid">
            <Statement title="Our Mission" text={mission} icon="target" tone="crimson" />
          </div>
          <div
            {...reveal("rise", 110)}
            className="relative hidden min-h-[420px] overflow-hidden rounded-[28px] md:block lg:aspect-[1542/2048]"
          >
            <Image
              src={missionVision.photo.src}
              alt={missionVision.photo.alt}
              fill
              placeholder="blur"
              sizes="(min-width: 1024px) 400px, 100vw"
              className="object-cover"
            />
          </div>
          <div {...reveal("rise", 220)} className="grid">
            <Statement title="Our Vision" text={vision} icon="eye" tone="navy" />
          </div>
        </div>
      </Container>
    </section>
  )
}
