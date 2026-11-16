import Image from "next/image"
import Link from "next/link"
import { anchors } from "@/config/routes"
import { site } from "@/config/site"
import { contactDetails } from "@/content/contact"
import { cn } from "@/lib/cn"
import { Icon, type IconName } from "@/components/ui/Icon"

type Row = { href: string; icon: IconName; tile: string; label: string; value: string; external?: boolean }

/** Desktop "Contact details" card: photo, then call / email / social rows, address at the foot. */
export function ContactDetailsCard() {
  const { labels } = contactDetails
  const rows: Row[] = [
    {
      href: site.contact.phoneHref,
      icon: "phone",
      tile: "bg-crimson text-white",
      label: labels.call,
      value: site.contact.phoneDisplay,
    },
    {
      href: site.contact.emailHref,
      icon: "mail",
      tile: "bg-royal text-white",
      label: labels.email,
      value: site.contact.email,
    },
  ]
  if (site.socials.tiktok)
    rows.push({
      href: site.socials.tiktok,
      icon: "tiktok",
      tile: "bg-white text-navy",
      label: labels.tiktok,
      value: labels.tiktokText,
      external: true,
    })
  if (site.socials.facebook)
    rows.push({
      href: site.socials.facebook,
      icon: "facebook",
      tile: "bg-azure text-white",
      label: labels.facebook,
      value: labels.facebookText,
      external: true,
    })

  return (
    <aside
      aria-label="Contact details"
      className="flex min-w-0 flex-[1_1_380px] flex-col overflow-hidden rounded-[28px] bg-navy text-white shadow-details"
    >
      <div className="relative h-[250px] shrink-0">
        <Image
          src={contactDetails.photo.src}
          alt={contactDetails.photo.alt}
          fill
          placeholder="blur"
          sizes="460px"
          className="object-cover"
        />
        <div aria-hidden="true" className="absolute inset-0 fade-panel" />
      </div>
      <div className="flex flex-1 flex-col gap-1.5 px-8 pt-2 pb-[34px]">
        <h2 className="mb-1 text-[26px] font-extrabold">{contactDetails.title}</h2>
        <p className="mb-3.5 text-[15px] text-on-navy-muted">{contactDetails.text}</p>
        {rows.map((r) => (
          <a
            key={r.label}
            href={r.href}
            {...(r.external ? { target: "_blank", rel: "noopener me" } : {})}
            className="-mx-3.5 flex items-center gap-4 rounded-2xl p-3.5 transition-colors duration-200 hover:bg-white/8"
          >
            <span className={cn("flex size-[46px] shrink-0 items-center justify-center rounded-[14px]", r.tile)}>
              <Icon name={r.icon} size={21} />
            </span>
            <span className="flex min-w-0 flex-col gap-0.5">
              <span className="text-xs font-bold tracking-[0.12em] text-gold uppercase">{r.label}</span>
              <span className="truncate text-[17px] font-semibold">{r.value}</span>
            </span>
          </a>
        ))}
        <Link href={anchors.contactMap} className="mt-auto flex items-start gap-4 pt-[18px]">
          <span className="flex size-[46px] shrink-0 items-center justify-center rounded-[14px] border-[1.5px] border-dashed border-gold/70 text-gold">
            <Icon name="mapPin" size={21} />
          </span>
          <span className="flex flex-col gap-0.5">
            <span className="text-xs font-bold tracking-[0.12em] text-gold uppercase">{labels.visit}</span>
            <span className="text-[15px] leading-normal">
              {site.address.street},
              <br />
              {site.address.city}, {site.address.region}
            </span>
          </span>
        </Link>
      </div>
    </aside>
  )
}
