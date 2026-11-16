import { routes } from "@/config/routes"
import { aboutNav } from "@/content/about"
import { buildMetadata } from "@/lib/seo/metadata"
import { breadcrumbJsonLd, founderJsonLd, pageJsonLd } from "@/lib/seo/json-ld"
import { JsonLd } from "@/components/seo/JsonLd"
import { AboutHero } from "@/components/sections/about/AboutHero"
import { AboutSectionNav } from "@/components/sections/about/AboutSectionNav"
import { CommitmentSection } from "@/components/sections/about/CommitmentSection"
import { CoreValuesSection } from "@/components/sections/about/CoreValuesSection"
import { FounderSection } from "@/components/sections/about/FounderSection"
import { MissionVisionSection } from "@/components/sections/about/MissionVisionSection"
import { PassionSection } from "@/components/sections/about/PassionSection"
import { StorySection } from "@/components/sections/about/StorySection"
import { WhyListSection } from "@/components/sections/about/WhyListSection"

const title = "About Us — Our Story, Mission, Values & Proprietress"
const description =
  "Founded in 2018 by Mrs. Fredrick Juliet, Golden Royal Ark School in Owerri raises academically sound, morally upright and confident children. Read our story, mission, vision and GREATNESS values."

export const metadata = buildMetadata({ title, description, path: routes.about })

export default function AboutPage() {
  return (
    <>
      <JsonLd
        data={[
          pageJsonLd("AboutPage", { path: routes.about, name: title, description }),
          breadcrumbJsonLd([
            { name: "Home", path: routes.home },
            { name: "About us", path: routes.about },
          ]),
          founderJsonLd(),
        ]}
      />
      <AboutHero />
      <AboutSectionNav items={aboutNav} />
      <StorySection />
      <PassionSection />
      <MissionVisionSection />
      <CoreValuesSection />
      <FounderSection />
      <WhyListSection />
      <CommitmentSection />
    </>
  )
}
