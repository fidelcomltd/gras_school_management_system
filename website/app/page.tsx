import { routes } from "@/config/routes"
import { heroSlides } from "@/content/home"
import { buildMetadata } from "@/lib/seo/metadata"
import { pageJsonLd } from "@/lib/seo/json-ld"
import { JsonLd } from "@/components/seo/JsonLd"
import { FounderTeaserSection } from "@/components/sections/home/FounderTeaserSection"
import { GallerySection } from "@/components/sections/home/GallerySection"
import { HeroCarousel } from "@/components/sections/home/HeroCarousel"
import { HomeCtaSection } from "@/components/sections/home/HomeCtaSection"
import { ProofStrip } from "@/components/sections/home/ProofStrip"
import { QuickResultCard } from "@/components/sections/home/QuickResultCard"
import { ResultPortalSection } from "@/components/sections/home/ResultPortalSection"
import { ServicesSection } from "@/components/sections/home/ServicesSection"
import { WelcomeSection } from "@/components/sections/home/WelcomeSection"
import { WhyChooseUsSection } from "@/components/sections/home/WhyChooseUsSection"

const title = "Golden Royal Ark School | Crèche, Preschool & Primary School in Owerri"
const description =
  "A nurturing, child-centred crèche, preschool and primary school in World Bank Area, Owerri, Imo State. Quality education for ages 6 months to 12 years since 2018."

export const metadata = buildMetadata({ title, absoluteTitle: true, description, path: routes.home })

export default function HomePage() {
  return (
    <>
      <JsonLd data={pageJsonLd("WebPage", { path: routes.home, name: title, description })} />
      <HeroCarousel slides={heroSlides} />
      <QuickResultCard />
      <ProofStrip />
      <WelcomeSection />
      <ServicesSection />
      <WhyChooseUsSection />
      <GallerySection />
      <FounderTeaserSection />
      <ResultPortalSection />
      <HomeCtaSection />
    </>
  )
}
