import { contactHero } from "@/content/contact"
import { Breadcrumb } from "@/components/ui/Breadcrumb"
import { Waves } from "@/components/ui/Waves"

/** Sky header band. Centred on desktop (the contact cards overlap its foot), left-aligned on phones. */
export function ContactHero() {
  return (
    <section className="relative overflow-hidden bg-sky-tint">
      <Waves variant="sky" />
      <div className="relative mx-auto flex max-w-[1304px] flex-col gap-3 px-4 pt-8 pb-16 md:items-center md:gap-[18px] md:px-8 md:pt-[72px] md:pb-[150px] md:text-center">
        <Breadcrumb current={contactHero.breadcrumb} tone="light" />
        <h1 className="animate-rise text-4xl leading-[1.06] font-extrabold text-navy md:text-[58px] md:leading-[1.05]">
          {contactHero.title}
        </h1>
        <p className="max-w-[640px] animate-rise text-base leading-[1.6] text-slate-deep [animation-delay:120ms] md:text-[19px]">
          <span className="md:hidden">{contactHero.mobileText}</span>
          <span className="hidden md:inline">{contactHero.text}</span>
        </p>
      </div>
    </section>
  )
}
