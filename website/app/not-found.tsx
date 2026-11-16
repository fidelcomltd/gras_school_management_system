import type { Metadata } from "next"
import { routes } from "@/config/routes"
import { ButtonLink } from "@/components/ui/Button"
import { Container } from "@/components/ui/Container"
import { Crest } from "@/components/ui/Crest"
import { Eyebrow } from "@/components/ui/Typography"

export const metadata: Metadata = { title: "Page not found", robots: { index: false } }

export default function NotFound() {
  return (
    <section className="bg-sky-tint py-24 md:py-32">
      <Container className="flex flex-col items-center gap-5 text-center">
        <Crest width={88} alt="" />
        <Eyebrow>Page not found</Eyebrow>
        <h1 className="max-w-[640px] text-4xl leading-[1.08] font-extrabold text-navy md:text-[52px]">
          We couldn&apos;t find that page.
        </h1>
        <p className="max-w-[520px] text-base leading-[1.7] text-slate md:text-[17px]">
          It may have moved. Head back home, or get in touch and we&apos;ll help.
        </p>
        <div className="mt-2 flex flex-wrap justify-center gap-3.5">
          <ButtonLink href={routes.home} variant="primary" size="lg" icon="arrowRight">
            Back to home
          </ButtonLink>
          <ButtonLink href={routes.contact} variant="outline" size="lg">
            Contact us
          </ButtonLink>
        </div>
      </Container>
    </section>
  )
}
