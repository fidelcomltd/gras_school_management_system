import { resultHelp } from "@/content/contact"
import { ResultPortalButton } from "@/components/ui/Button"
import { Icon } from "@/components/ui/Icon"

/** "Looking for your child's result?" — gold card that sends parents to the portal. */
export function ResultHelpCard() {
  return (
    <div
      id="results"
      className="relative flex flex-col gap-3 overflow-hidden rounded-[22px] border-[1.5px] border-gold-line bg-gold-cream px-[18px] py-[22px] md:gap-4 md:rounded-[28px] md:p-10"
    >
      <span className="hidden size-14 items-center justify-center rounded-2xl bg-navy text-gold md:flex">
        <Icon name="lock" size={26} />
      </span>
      <h2 className="text-[21px] leading-[1.25] font-extrabold text-navy md:text-[28px] md:leading-[1.2]">
        {resultHelp.title}
      </h2>
      <p className="text-[14.5px] leading-[1.6] text-umber md:text-base md:leading-[1.65]">
        <span className="md:hidden">{resultHelp.mobileText}</span>
        <span className="hidden md:inline">{resultHelp.text}</span>
      </p>
      <ResultPortalButton
        variant="gold"
        className="min-h-[50px] py-0 font-bold shadow-none md:mt-1.5 md:min-h-0 md:self-start md:py-[15px]"
        iconClassName="size-[17px]"
      >
        {resultHelp.cta}
      </ResultPortalButton>
    </div>
  )
}
