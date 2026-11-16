import Link from "next/link"
import type { ComponentPropsWithoutRef, ReactNode } from "react"
import { site } from "@/config/site"
import { cn } from "@/lib/cn"
import { Icon, type IconName } from "@/components/ui/Icon"

const variants = {
  /** Crimson pill — the main call to action */
  primary: "bg-crimson text-white shadow-crimson hover:bg-crimson-hover",
  /** Gold pill — result-portal actions */
  gold: "bg-gold text-navy font-bold shadow-gold",
  navy: "bg-navy text-white",
  /** Navy outline on white */
  outline: "border-[1.5px] border-navy text-navy hover:bg-navy hover:text-white",
  /** Gold outline on dark photo (hero) */
  outlineGold: "border-[1.5px] border-gold text-white hover:bg-white/10",
  /** White pill on crimson */
  white: "bg-white text-crimson font-bold",
  /** Light outline on crimson */
  outlineLight: "border-[1.5px] border-gold-soft text-white hover:bg-white/10",
  /** Header "Check Results" chip */
  goldChip: "border-[1.5px] border-gold bg-gold-cream text-navy hover:bg-gold-tint",
} as const

const sizes = {
  sm: "gap-2 px-[18px] py-3 text-sm",
  md: "gap-2.5 px-6 py-[15px] text-[15px]",
  lg: "gap-2.5 px-[26px] py-4 text-base",
} as const

export type ButtonVariant = keyof typeof variants
export type ButtonSize = keyof typeof sizes

export function buttonClasses(variant: ButtonVariant = "primary", size: ButtonSize = "md", className?: string) {
  return cn(
    "inline-flex lift-on-hover items-center justify-center rounded-full font-semibold no-underline",
    variants[variant],
    sizes[size],
    className
  )
}

type Common = {
  variant?: ButtonVariant
  size?: ButtonSize
  icon?: IconName
  iconClassName?: string
  /** Icon before the label instead of after it. */
  iconFirst?: boolean
  children: ReactNode
}

function Content({
  icon,
  iconClassName,
  iconFirst,
  children,
}: Pick<Common, "icon" | "iconClassName" | "iconFirst" | "children">) {
  const glyph = icon ? <Icon name={icon} size={18} className={cn("shrink-0", iconClassName)} /> : null
  return (
    <>
      {iconFirst && glyph}
      {children}
      {!iconFirst && glyph}
    </>
  )
}

type ButtonLinkProps = Common & Omit<ComponentPropsWithoutRef<typeof Link>, "children">

/** Internal link styled as a pill button. */
export function ButtonLink({
  variant,
  size,
  icon,
  iconClassName,
  iconFirst,
  className,
  children,
  ...rest
}: ButtonLinkProps) {
  return (
    <Link className={buttonClasses(variant, size, className)} {...rest}>
      <Content icon={icon} iconClassName={iconClassName} iconFirst={iconFirst}>
        {children}
      </Content>
    </Link>
  )
}

type AnchorProps = Common & Omit<ComponentPropsWithoutRef<"a">, "children">

/** tel:, mailto: and off-site links styled as a pill button. */
export function ButtonAnchor({
  variant,
  size,
  icon,
  iconClassName,
  iconFirst,
  className,
  children,
  ...rest
}: AnchorProps) {
  return (
    <a className={buttonClasses(variant, size, className)} {...rest}>
      <Content icon={icon} iconClassName={iconClassName} iconFirst={iconFirst}>
        {children}
      </Content>
    </a>
  )
}

/** Props that make any <a> open the environment's result portal in a new tab. */
export const resultPortalLinkProps = {
  href: site.resultPortalUrl,
  target: "_blank",
  rel: "noopener",
} as const

/** "Check results" style button pointing at NEXT_PUBLIC_RESULT_PORTAL_URL. `icon={null}` drops the icon. */
export function ResultPortalButton({
  children,
  icon = "external",
  ...rest
}: Omit<AnchorProps, "href" | "icon"> & { icon?: IconName | null }) {
  return (
    <ButtonAnchor {...resultPortalLinkProps} icon={icon ?? undefined} {...rest}>
      {children}
      <span className="sr-only"> (opens the result portal in a new tab)</span>
    </ButtonAnchor>
  )
}
