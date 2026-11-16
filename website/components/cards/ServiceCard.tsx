import Image from "next/image"
import type { Service } from "@/content/school"
import { cn } from "@/lib/cn"

/** Photo card for Crèche / Preschool / Primary: age pill top-left, title and summary over a navy fade. */
export function ServiceCard({ service, className }: { service: Service; className?: string }) {
  return (
    <article
      className={cn(
        "relative isolate card-lift zoom-on-hover overflow-hidden rounded-3xl bg-navy lg:rounded-[28px]",
        className
      )}
    >
      <Image
        src={service.photo.src}
        alt={service.photo.alt}
        fill
        sizes="(min-width: 1024px) 380px, 300px"
        placeholder="blur"
        className="-z-10 object-cover"
        style={service.objectPosition ? { objectPosition: service.objectPosition } : undefined}
      />
      <div aria-hidden="true" className="absolute inset-0 -z-10 fade-card lg:fade-card-lg" />
      <span className="absolute top-4 left-4 rounded-full bg-white px-3 py-1.5 text-xs font-bold text-crimson lg:top-5 lg:left-5 lg:px-3.5 lg:py-2 lg:text-[13px]">
        {service.ages}
      </span>
      <div className="absolute inset-x-5 bottom-5 flex flex-col gap-2 lg:inset-x-7 lg:bottom-7 lg:gap-2.5">
        <h3 className="text-2xl font-extrabold text-white lg:text-[30px]">{service.name}</h3>
        <p className="text-[14.5px] leading-[1.55] text-on-navy lg:text-base lg:leading-[1.6]">{service.summary}</p>
      </div>
    </article>
  )
}
