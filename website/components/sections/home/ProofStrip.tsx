import { stats, type Stat } from "@/content/home"
import { cn } from "@/lib/cn"
import { reveal } from "@/components/motion/Reveal"
import { Container } from "@/components/ui/Container"

const tone: Record<Stat["tone"], string> = {
  crimson: "text-crimson",
  royal: "text-royal",
  gold: "text-gold-text",
  azure: "text-azure",
}

/** "At a glance": four tinted tiles on phones, one white card with dividers from tablet up. */
export function ProofStrip() {
  return (
    <section aria-label="At a glance" className="relative z-[2] pt-5 md:mt-8 md:pt-0">
      <Container>
        <dl
          {...reveal("rise")}
          className="grid grid-cols-2 gap-2.5 md:gap-0 md:overflow-hidden md:rounded-3xl md:border md:border-line md:bg-white md:shadow-card lg:grid-cols-4"
        >
          {stats.map((s, i) => (
            <div
              key={s.value}
              className={cn(
                "flex flex-col-reverse justify-end gap-1 rounded-[18px] border border-line-cool bg-mist p-[18px]",
                "md:gap-1.5 md:rounded-none md:border-0 md:border-line-soft md:bg-transparent md:px-[30px] md:py-7",
                i % 2 === 0 && "md:border-r",
                i < 2 && "md:border-b lg:border-b-0",
                i === 1 && "lg:border-r",
                i === 3 && "lg:border-r-0"
              )}
            >
              <dt className="text-[12.5px] leading-[1.45] text-slate md:text-sm md:leading-normal">
                <span className="md:hidden">{s.mobileLabel}</span>
                <span className="hidden md:inline">{s.label}</span>
              </dt>
              <dd className={cn("font-display text-2xl font-extrabold md:text-[34px]", tone[s.tone])}>{s.value}</dd>
            </div>
          ))}
        </dl>
      </Container>
    </section>
  )
}
