"use client"

import { useSyncExternalStore } from "react"

const noop = () => () => {}

/**
 * False on the server and during hydration, true once the page is interactive. Use it to defer
 * non-critical work (e.g. loading carousel slides 2–4) until after the first paint.
 */
export function useHydrated() {
  return useSyncExternalStore(
    noop,
    () => true,
    () => false
  )
}
