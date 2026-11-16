import { site, socialLinks } from "@/config/site"
import { cn } from "@/lib/cn"
import { Icon } from "@/components/ui/Icon"

/** Round TikTok / Facebook buttons. Only configured profiles render (NEXT_PUBLIC_*_URL). */
export function SocialIcons({ className, itemClassName }: { className?: string; itemClassName?: string }) {
  if (socialLinks.length === 0) return null
  return (
    <div className={cn("flex gap-2.5", className)}>
      {socialLinks.map((s) => (
        <a
          key={s.key}
          href={s.href}
          target="_blank"
          rel="noopener me"
          aria-label={`${site.name} on ${s.label}`}
          className={cn(
            "flex size-10 items-center justify-center rounded-full bg-white/8 text-white transition-colors duration-200 hover:bg-white/16",
            itemClassName
          )}
        >
          <Icon name={s.key} size={18} />
        </a>
      ))}
    </div>
  )
}
