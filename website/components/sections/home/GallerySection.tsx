import Image from "next/image"
import type { CSSProperties } from "react"
import { gallery, type GalleryItem } from "@/content/home"
import { cn } from "@/lib/cn"
import { reveal } from "@/components/motion/Reveal"
import { Container } from "@/components/ui/Container"
import { Eyebrow, Heading } from "@/components/ui/Typography"

/**
 * Infinite photo marquee — pure CSS, no JavaScript. The track holds the photos twice and slides by
 * exactly half its width (70s desktop, 50s phone), so the loop is seamless. Pauses on hover or
 * keyboard focus; under reduced motion it becomes a plain horizontal scroller (globals.css).
 * Cards vary in size with a slight tilt; hover lifts, straightens and zooms the photo.
 */
function GalleryCard({ item, duplicate }: { item: GalleryItem; duplicate?: boolean }) {
  return (
    <figure
      aria-hidden={duplicate || undefined}
      style={{ "--w": `${item.width}px`, "--h": `${item.height}px`, "--tilt": `${item.tilt}deg` } as CSSProperties}
      className={cn(
        "group relative m-0 h-[calc(var(--h)*0.667)] w-[calc(var(--w)*0.667)] shrink-0 overflow-hidden rounded-[20px] bg-royal md:h-(--h) md:w-(--w) md:rounded-[26px]",
        "rotate-(--tilt) transition-[rotate,translate,box-shadow] duration-600 ease-[var(--ease-out-soft)]",
        "md:hover:-translate-y-2.5 md:hover:-rotate-[1.2deg] md:hover:shadow-gallery-hover"
      )}
    >
      <Image
        src={item.photo.src}
        alt={duplicate ? "" : item.alt}
        fill
        sizes={`(min-width: 768px) ${item.width}px, ${Math.round(item.width * 0.667)}px`}
        className="object-cover transition-transform duration-[1200ms] ease-[var(--ease-out-soft)] md:group-hover:scale-[1.08]"
      />
    </figure>
  )
}

export function GallerySection() {
  return (
    <section
      aria-labelledby="gallery-heading"
      className="relative overflow-hidden bg-navy pt-[52px] pb-[60px] md:pt-[100px] md:pb-[110px]"
    >
      <Container className="flex flex-wrap items-end justify-between gap-6 pb-7 md:pb-[52px]" {...reveal()}>
        <div className="flex flex-col gap-2.5 md:gap-3.5">
          <Eyebrow tone="gold">{gallery.eyebrow}</Eyebrow>
          <Heading id="gallery-heading" className="text-white">
            {gallery.title}
          </Heading>
        </div>
        <p className="hidden max-w-[380px] text-base leading-[1.7] text-on-navy-muted md:block">{gallery.text}</p>
      </Container>

      <div
        className="marquee no-scrollbar overflow-hidden py-2.5 md:py-5"
        tabIndex={0}
        role="region"
        aria-label="Photo gallery, scrolling. Hover or focus to pause."
      >
        <div className="marquee-track flex w-max animate-marquee items-center gap-3.5 [--marquee-duration:50s] md:gap-6 md:[--marquee-duration:70s]">
          {gallery.items.map((item) => (
            <GalleryCard key={item.alt} item={item} />
          ))}
          {gallery.items.map((item) => (
            <GalleryCard key={`dup-${item.alt}`} item={item} duplicate />
          ))}
        </div>
      </div>
    </section>
  )
}
