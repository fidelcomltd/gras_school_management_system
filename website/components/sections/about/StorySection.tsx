import { aboutSections } from "@/config/routes"
import { story } from "@/content/about"
import { cn } from "@/lib/cn"
import { reveal } from "@/components/motion/Reveal"
import { Container } from "@/components/ui/Container"
import { Icon } from "@/components/ui/Icon"
import { Eyebrow, Heading } from "@/components/ui/Typography"

const dot = { crimson: "bg-crimson text-white", royal: "bg-royal text-white", gold: "bg-gold text-navy" } as const
const year = { crimson: "text-crimson", royal: "text-royal", gold: "text-gold-text" } as const

/** "Our story" with the 2018 → Then → Today timeline. */
export function StorySection() {
  return (
    <section id={aboutSections.story} className="py-11 md:py-[100px]">
      <Container className="grid items-start gap-4 lg:grid-cols-2 lg:gap-20">
        <div className="flex flex-col gap-4 md:gap-[22px]" {...reveal()}>
          <Eyebrow>{story.eyebrow}</Eyebrow>
          <Heading size="sm">{story.title}</Heading>
          {story.paragraphs.map((p) => (
            <p key={p.desktop} className="text-base leading-[1.7] text-slate md:text-[17px] md:leading-[1.75]">
              <span className="md:hidden">{p.mobile}</span>
              <span className="hidden md:inline">{p.desktop}</span>
            </p>
          ))}
        </div>

        <ol
          aria-label="Our journey"
          className="mt-2.5 flex flex-col rounded-[22px] border border-line-cool bg-mist px-[18px] py-[22px] md:mt-0 md:rounded-[28px] md:p-9"
          {...reveal("rise", 120)}
        >
          {story.timeline.map((item, i) => {
            const last = i === story.timeline.length - 1
            return (
              <li key={item.year} className="grid grid-cols-[44px_1fr] gap-3.5 md:grid-cols-[56px_1fr] md:gap-5">
                <div className="flex flex-col items-center">
                  <span
                    className={cn(
                      "flex size-11 items-center justify-center rounded-full md:size-14",
                      dot[item.tone as keyof typeof dot]
                    )}
                  >
                    <Icon name={item.icon} size={22} className="size-5 md:size-6" />
                  </span>
                  {!last && (
                    <span
                      aria-hidden="true"
                      className="min-h-[30px] w-0.5 flex-1 bg-[repeating-linear-gradient(180deg,var(--color-gold)_0_6px,transparent_6px_12px)] md:min-h-10"
                    />
                  )}
                </div>
                <div className={cn("flex flex-col gap-1 md:gap-1.5", !last && "pb-[22px] md:pb-8")}>
                  <span
                    className={cn(
                      "font-display text-[19px] font-extrabold md:text-[22px]",
                      year[item.tone as keyof typeof year]
                    )}
                  >
                    {item.year}
                  </span>
                  <span className="hidden text-[17px] font-bold text-navy md:block">{item.title}</span>
                  <p className="text-[14.5px] leading-[1.6] text-slate md:text-[15.5px] md:leading-[1.65]">
                    <span className="md:hidden">{item.mobileText}</span>
                    <span className="hidden md:inline">{item.text}</span>
                  </p>
                </div>
              </li>
            )
          })}
        </ol>
      </Container>
    </section>
  )
}
