import { resultBand } from "@/content/home"
import { resultPortalLinkProps } from "@/components/ui/Button"
import { Icon } from "@/components/ui/Icon"

/** Phone-only shortcut under the hero: parents' most common task, one tap away. */
export function QuickResultCard() {
  return (
    <section aria-label="For parents" className="px-4 pt-2 md:hidden">
      <a
        {...resultPortalLinkProps}
        className="flex items-center gap-3.5 rounded-[20px] border-[1.5px] border-gold-line-soft bg-gold-cream p-4 transition-transform active:scale-[0.99]"
      >
        <span className="flex size-[46px] shrink-0 items-center justify-center rounded-[14px] bg-navy text-gold">
          <Icon name="lock" size={22} />
        </span>
        <span className="flex flex-1 flex-col gap-0.5">
          <span className="text-[15px] font-bold text-navy">{resultBand.quickCard.title}</span>
          <span className="text-[13px] text-umber">{resultBand.quickCard.subtitle}</span>
        </span>
        <Icon name="external" size={20} className="text-gold-text" />
        <span className="sr-only"> (opens the result portal in a new tab)</span>
      </a>
    </section>
  )
}
