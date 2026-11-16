import { site } from "@/config/site"
import { contactDetails } from "@/content/contact"
import { Icon } from "@/components/ui/Icon"

/** Phone-only 2×2 tiles: call, email, TikTok, Facebook (social tiles only when configured). */
export function QuickContact() {
  const { labels } = contactDetails
  return (
    <section aria-label="Quick contact" className="px-4 pt-1 md:hidden">
      <div className="grid grid-cols-2 gap-2.5">
        <a
          href={site.contact.phoneHref}
          className="flex min-h-[124px] flex-col justify-between gap-2.5 rounded-[18px] bg-crimson p-4 text-white active:scale-[0.98]"
        >
          <Icon name="phone" size={22} />
          <span className="flex flex-col gap-0.5">
            <span className="text-[15px] font-bold">{labels.call}</span>
            <span className="text-[12.5px] text-rose">{site.contact.phoneDisplay}</span>
          </span>
        </a>
        <a
          href={site.contact.emailHref}
          className="flex min-h-[124px] flex-col justify-between gap-2.5 rounded-[18px] bg-navy p-4 text-white active:scale-[0.98]"
        >
          <Icon name="mail" size={22} className="text-gold" />
          <span className="flex flex-col gap-0.5">
            <span className="text-[15px] font-bold">{labels.email}</span>
            <span className="text-xs break-all text-on-navy-soft">{site.contact.email}</span>
          </span>
        </a>
        {site.socials.tiktok && (
          <a
            href={site.socials.tiktok}
            target="_blank"
            rel="noopener me"
            className="flex min-h-[94px] items-center gap-3 rounded-[18px] border border-line-cool bg-mist px-4 py-3.5 text-navy active:scale-[0.98]"
          >
            <Icon name="tiktok" size={22} />
            <span className="text-[15px] font-bold">{labels.tiktok}</span>
          </a>
        )}
        {site.socials.facebook && (
          <a
            href={site.socials.facebook}
            target="_blank"
            rel="noopener me"
            className="flex min-h-[94px] items-center gap-3 rounded-[18px] border border-line-cool bg-mist px-4 py-3.5 text-navy active:scale-[0.98]"
          >
            <Icon name="facebook" size={22} className="text-azure" />
            <span className="text-[15px] font-bold">{labels.facebook}</span>
          </a>
        )}
      </div>
    </section>
  )
}
