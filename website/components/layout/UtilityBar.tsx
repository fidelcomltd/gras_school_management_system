import { site, socialLinks } from "@/config/site"
import { Container } from "@/components/ui/Container"
import { Icon } from "@/components/ui/Icon"

/** Navy strip above the header (tablet and up): phone, email, social. */
export function UtilityBar() {
  return (
    <div className="hidden bg-navy text-[13px] text-on-navy md:block">
      <Container className="flex flex-wrap items-center justify-between gap-x-7 gap-y-3 py-2.5">
        <div className="flex flex-wrap items-center gap-x-7 gap-y-2">
          <a
            href={site.contact.phoneHref}
            className="inline-flex items-center gap-2 transition-colors hover:text-white"
          >
            <Icon name="phone" size={15} className="text-gold" />
            {site.contact.phoneDisplay}
          </a>
          <a
            href={site.contact.emailHref}
            className="inline-flex items-center gap-2 transition-colors hover:text-white"
          >
            <Icon name="mail" size={15} className="text-gold" />
            {site.contact.email}
          </a>
        </div>
        {socialLinks.length > 0 && (
          <div className="flex items-center gap-4">
            <span className="text-on-navy-dim">Follow us</span>
            {socialLinks.map((s) => (
              <a
                key={s.key}
                href={s.href}
                target="_blank"
                rel="noopener me"
                aria-label={`${site.name} on ${s.label}`}
                className="inline-flex text-white transition-colors hover:text-gold"
              >
                <Icon name={s.key} size={17} />
              </a>
            ))}
          </div>
        )}
      </Container>
    </div>
  )
}
