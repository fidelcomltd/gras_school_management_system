import { site } from "@/config/site"
import { visit } from "@/content/contact"
import { Crest } from "@/components/ui/Crest"
import { Icon } from "@/components/ui/Icon"

/**
 * "Visit us": the design's drawn street map with a crest pin, and a Get directions button that opens
 * Google Maps on the school's address (or exact coordinates, when NEXT_PUBLIC_GEO_LAT/LNG are set).
 * Drawn rather than embedded on purpose: an embedded map costs ~1 MB of third-party script on a page
 * parents often open on mobile data. `site.maps.embedUrl` is ready if the school wants a live map.
 */
export function VisitCard() {
  return (
    <div
      id="map"
      className="relative h-[300px] overflow-hidden rounded-[22px] bg-sky-tint md:h-auto md:min-h-[340px] md:rounded-[28px]"
    >
      <svg
        aria-hidden="true"
        viewBox="0 0 600 340"
        preserveAspectRatio="xMidYMid slice"
        className="absolute inset-0 size-full"
      >
        <rect width="600" height="340" className="fill-sky-tint" />
        <path
          d="M-20 250 C 120 220 220 280 360 240 S 560 200 640 230"
          className="stroke-sky-mid"
          strokeWidth="34"
          fill="none"
        />
        <path d="M-20 90 L 640 130" className="stroke-white" strokeWidth="18" />
        <path d="M150 -20 L 210 360" className="stroke-white" strokeWidth="16" />
        <path d="M420 -20 L 380 360" className="stroke-white" strokeWidth="22" />
        <path d="M-20 190 L 640 175" className="stroke-white" strokeWidth="10" />
        <path d="M290 -20 L 300 360" className="stroke-white" strokeWidth="8" />
        <rect x="225" y="140" width="55" height="28" rx="6" className="fill-sky-wash" />
        <rect x="440" y="40" width="80" height="60" rx="8" className="fill-sky-wash" />
        <rect x="40" y="110" width="90" height="55" rx="8" className="fill-sky-wash" />
      </svg>

      <div className="absolute top-[30%] left-1/2 -translate-x-1/2 -translate-y-full md:top-[34%]">
        <span className="flex size-[54px] -rotate-45 items-center justify-center rounded-[50%_50%_50%_0] bg-crimson shadow-pin md:size-16">
          <span className="flex size-8 rotate-45 items-center justify-center rounded-full bg-white p-[3px] md:size-[38px]">
            <Crest width={32} alt="" />
          </span>
        </span>
      </div>

      <div className="absolute inset-x-3 bottom-3 flex flex-col gap-3 rounded-2xl bg-white px-4 py-3.5 shadow-address-sm md:inset-x-5 md:bottom-5 md:flex-row md:flex-wrap md:items-center md:justify-between md:gap-x-5 md:gap-y-3.5 md:rounded-[20px] md:px-[22px] md:py-5 md:shadow-inset-photo">
        <address className="flex flex-col gap-0.5 not-italic md:gap-1">
          <span className="text-[11px] font-bold tracking-[0.14em] text-crimson uppercase md:text-xs">
            {visit.label}
          </span>
          <span className="font-display text-base font-extrabold text-navy md:text-lg">{site.address.street}</span>
          <span className="text-[13.5px] text-slate md:text-[14.5px]">
            {site.address.city}, {site.address.region}
          </span>
        </address>
        <a
          href={site.maps.directionsUrl}
          target="_blank"
          rel="noopener"
          className="flex min-h-[46px] lift-on-hover items-center justify-center gap-2 rounded-full bg-navy px-5 text-[14.5px] font-semibold text-white md:min-h-0 md:py-[13px]"
        >
          <Icon name="navigation" size={16} className="text-gold" />
          {visit.directions}
          <span className="sr-only"> (opens Google Maps in a new tab)</span>
        </a>
      </div>
    </div>
  )
}
