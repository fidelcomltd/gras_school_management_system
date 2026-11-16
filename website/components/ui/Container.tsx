import type { ComponentPropsWithoutRef, ElementType } from "react"
import { cn } from "@/lib/cn"

type ContainerProps<T extends ElementType> = { as?: T } & ComponentPropsWithoutRef<T>

/**
 * The design's content column: 1240px of CONTENT plus gutters (16px phones, 32px tablet up). The canvas
 * has no box-sizing reset, so its `max-width:1240px; padding:0 32px` is 1304px wide in total.
 */
export function Container<T extends ElementType = "div">({ as, className, ...rest }: ContainerProps<T>) {
  const Tag = as ?? "div"
  return <Tag className={cn("mx-auto w-full max-w-[1272px] px-4 md:max-w-[1304px] md:px-8", className)} {...rest} />
}
