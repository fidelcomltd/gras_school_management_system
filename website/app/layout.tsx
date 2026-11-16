import type { Metadata, Viewport } from "next"
import { Inter, Plus_Jakarta_Sans } from "next/font/google"
import { env, isProductionSite } from "@/config/env"
import { site } from "@/config/site"
import { schoolJsonLd, websiteJsonLd } from "@/lib/seo/json-ld"
import { JsonLd } from "@/components/seo/JsonLd"
import { SiteFooter } from "@/components/layout/SiteFooter"
import { SiteHeader } from "@/components/layout/SiteHeader"
import { UtilityBar } from "@/components/layout/UtilityBar"
import { RevealObserver } from "@/components/motion/RevealObserver"
import "./globals.css"

// Self-hosted at build time by next/font: no request to Google, no layout shift.
const inter = Inter({ subsets: ["latin"], variable: "--font-inter", display: "swap" })
const jakarta = Plus_Jakarta_Sans({
  subsets: ["latin"],
  weight: ["600", "700", "800"],
  variable: "--font-jakarta",
  display: "swap",
})

const defaultDescription =
  "Golden Royal Ark School is a crèche, preschool and primary school in Owerri, Imo State. Quality, child-centred education for children aged 6 months to 12 years since 2018."

export const metadata: Metadata = {
  metadataBase: new URL(site.url),
  title: { default: `${site.name} | Crèche, Preschool & Primary School in Owerri`, template: `%s | ${site.name}` },
  description: defaultDescription,
  applicationName: site.name,
  authors: [{ name: site.name, url: site.url }],
  creator: site.name,
  publisher: site.name,
  category: "education",
  keywords: [
    "Golden Royal Ark School",
    "school in Owerri",
    "primary school in Owerri",
    "nursery school in Owerri",
    "crèche in Owerri",
    "preschool in Owerri",
    "day care Owerri",
    "World Bank Owerri school",
    "Imo State schools",
  ],
  formatDetection: { telephone: false, email: false, address: false },
  openGraph: { type: "website", siteName: site.name, locale: site.locale },
  robots: isProductionSite ? { index: true, follow: true } : { index: false, follow: false },
  ...(env.googleSiteVerification ? { verification: { google: env.googleSiteVerification } } : {}),
}

export const viewport: Viewport = {
  themeColor: "#0e1d47",
  width: "device-width",
  initialScale: 1,
}

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang={site.language} className={`${inter.variable} ${jakarta.variable}`} suppressHydrationWarning>
      <head>
        {/* Before first paint: lets CSS hide scroll-reveal content only when JS will reveal it. */}
        <script dangerouslySetInnerHTML={{ __html: "document.documentElement.classList.add('js')" }} />
        <JsonLd data={[schoolJsonLd(), websiteJsonLd()]} />
      </head>
      <body className="flex min-h-dvh flex-col">
        <a
          href="#main"
          className="sr-only z-50 rounded-full bg-navy px-5 py-3 font-semibold text-white focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
        >
          Skip to content
        </a>
        <UtilityBar />
        <SiteHeader />
        <main id="main" className="flex-1">
          {children}
        </main>
        <SiteFooter />
        <RevealObserver />
      </body>
    </html>
  )
}
