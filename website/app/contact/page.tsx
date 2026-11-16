import { routes } from "@/config/routes"
import { site } from "@/config/site"
import { buildMetadata } from "@/lib/seo/metadata"
import { breadcrumbJsonLd, pageJsonLd } from "@/lib/seo/json-ld"
import { reveal } from "@/components/motion/Reveal"
import { JsonLd } from "@/components/seo/JsonLd"
import { Container } from "@/components/ui/Container"
import { ContactDetailsCard } from "@/components/sections/contact/ContactDetailsCard"
import { ContactForm } from "@/components/sections/contact/ContactForm"
import { ContactHero } from "@/components/sections/contact/ContactHero"
import { QuickContact } from "@/components/sections/contact/QuickContact"
import { ResultHelpCard } from "@/components/sections/contact/ResultHelpCard"
import { VisitCard } from "@/components/sections/contact/VisitCard"

const title = "Contact Us — Call, Email or Visit in Owerri"
const description = `Contact ${site.name}, ${site.address.oneLine}. Call ${site.contact.phoneDisplay}, email us, or send a message about our Crèche, Preschool or Primary School.`

export const metadata = buildMetadata({ title, description, path: routes.contact })

export default function ContactPage() {
  return (
    <>
      <JsonLd
        data={[
          pageJsonLd("ContactPage", { path: routes.contact, name: title, description }),
          breadcrumbJsonLd([
            { name: "Home", path: routes.home },
            { name: "Contact", path: routes.contact },
          ]),
        ]}
      />
      <ContactHero />
      <QuickContact />

      <section className="relative pt-8 pb-10 md:-mt-[90px] md:pt-0 md:pb-[100px]">
        <Container className="flex flex-wrap items-stretch gap-7">
          <div className="hidden min-w-0 flex-[1_1_380px] md:flex">
            <ContactDetailsCard />
          </div>
          <div
            {...reveal("fade")}
            className="min-w-0 flex-[999_1_560px] rounded-3xl border border-line-cool bg-white px-[18px] py-6 shadow-panel-sm md:rounded-[28px] md:p-11 md:shadow-panel"
          >
            <ContactForm />
          </div>
        </Container>
      </section>

      <section className="pb-12 md:pb-[110px]">
        <Container className="grid gap-6 md:gap-7 lg:grid-cols-2">
          <ResultHelpCard />
          <VisitCard />
        </Container>
      </section>
    </>
  )
}
