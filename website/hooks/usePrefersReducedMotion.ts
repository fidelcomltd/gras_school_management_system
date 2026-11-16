"use client"

import { useSyncExternalStore } from "react"

const query = "(prefers-reduced-motion: reduce)"

function subscribe(onChange: () => void) {
  const mq = window.matchMedia(query)
  mq.addEventListener("change", onChange)
  return () => mq.removeEventListener("change", onChange)
}

/** True when the visitor has asked their OS for less motion. False during SSR. */
export function usePrefersReducedMotion() {
  return useSyncExternalStore(
    subscribe,
    () => window.matchMedia(query).matches,
    () => false
  )
}
