import { cn } from "@/lib/cn"

/**
 * The crest's wave motif, as the design uses it at the foot of bands. Each variant is the exact
 * desktop path plus its phone counterpart; the right one shows per breakpoint. Purely decorative.
 */
type Wave = { viewBox: string; paths: { d: string; className: string }[]; height: string }

const waves = {
  /** Under photo heroes: sky ribbon then white. `tint` sets the ribbon's opacity. */
  hero: {
    desktop: {
      viewBox: "0 0 1440 90",
      height: "h-[90px]",
      paths: [
        { d: "M0 50 C 180 10 360 10 540 45 S 900 85 1080 50 S 1350 15 1440 35 V90 H0 Z", className: "fill-sky" },
        { d: "M0 62 C 200 28 400 30 600 58 S 1000 90 1200 60 S 1380 40 1440 52 V90 H0 Z", className: "fill-white" },
      ],
    },
    mobile: {
      viewBox: "0 0 390 40",
      height: "h-10",
      paths: [
        { d: "M0 22 C 60 6 130 8 195 22 S 330 36 390 18 V40 H0 Z", className: "fill-sky" },
        { d: "M0 28 C 70 14 140 16 210 28 S 340 38 390 26 V40 H0 Z", className: "fill-white" },
      ],
    },
  },
  /** Crimson call-to-action bands: two deeper crimson swells across the lower part. */
  crimson: {
    desktop: {
      viewBox: "0 0 1440 300",
      height: "h-full",
      paths: [
        { d: "M0 210 C 240 160 480 170 720 210 S 1200 260 1440 200 V300 H0 Z", className: "fill-crimson-deep" },
        { d: "M0 250 C 260 220 520 225 780 252 S 1240 280 1440 250 V300 H0 Z", className: "fill-crimson-deeper" },
      ],
    },
    mobile: {
      viewBox: "0 0 390 120",
      height: "h-[100px]",
      paths: [
        { d: "M0 60 C 70 40 150 42 220 62 S 340 80 390 58 V120 H0 Z", className: "fill-crimson-deep" },
        { d: "M0 88 C 90 72 170 74 250 90 S 360 100 390 86 V120 H0 Z", className: "fill-crimson-deeper" },
      ],
    },
  },
  /** Sky-tint bands (contact header): pale swell then white. */
  sky: {
    desktop: {
      viewBox: "0 0 1440 120",
      height: "h-[120px]",
      paths: [
        { d: "M0 70 C 240 30 480 40 720 72 S 1200 100 1440 60 V120 H0 Z", className: "fill-sky-wash" },
        { d: "M0 96 C 260 70 520 74 780 98 S 1240 112 1440 92 V120 H0 Z", className: "fill-white" },
      ],
    },
    mobile: {
      viewBox: "0 0 390 60",
      height: "h-[50px]",
      paths: [
        { d: "M0 30 C 70 12 140 14 210 30 S 340 46 390 26 V60 H0 Z", className: "fill-sky-wash" },
        { d: "M0 44 C 80 32 160 34 240 46 S 360 54 390 42 V60 H0 Z", className: "fill-white" },
      ],
    },
  },
} satisfies Record<string, { desktop: Wave; mobile: Wave }>

type WavesProps = {
  variant: keyof typeof waves
  className?: string
  /** Opacity of the first (ribbon) path; the design uses .55 on Home and .4 on About. */
  ribbonOpacity?: number
}

function WaveSvg({ wave, className, ribbonOpacity }: { wave: Wave; className: string; ribbonOpacity?: number }) {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox={wave.viewBox}
      preserveAspectRatio="none"
      className={cn("pointer-events-none absolute bottom-[-1px] left-0 block w-full", wave.height, className)}
    >
      {wave.paths.map((p, i) => (
        <path key={i} d={p.d} className={p.className} opacity={i === 0 ? ribbonOpacity : undefined} />
      ))}
    </svg>
  )
}

export function Waves({ variant, className, ribbonOpacity }: WavesProps) {
  const w = waves[variant]
  return (
    <>
      <WaveSvg wave={w.desktop} ribbonOpacity={ribbonOpacity} className={cn("hidden md:block", className)} />
      <WaveSvg wave={w.mobile} ribbonOpacity={ribbonOpacity} className={cn("md:hidden", className)} />
    </>
  )
}
