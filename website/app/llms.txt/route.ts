import { routes } from "@/config/routes"
import { site, socialLinks } from "@/config/site"
import { coreValues, mission, reasonsLong, services, vision } from "@/content/school"

/**
 * /llms.txt — a plain-text brief of the school for AI assistants and agents (llmstxt.org). Built from
 * the same content and config as the pages, so it never drifts from what the site says.
 */
export const dynamic = "force-static"

export function GET() {
  const abs = (p: string) => new URL(p, site.url).toString()
  const body = `# ${site.name}

> ${site.name} (${site.initials}) is a crèche, preschool and primary school in ${site.address.city}, ${site.address.region}, Nigeria, founded in ${site.foundingYear}. It provides quality, child-centred care and education for children aged 6 months to 12 years. Motto: "${site.motto}".

## Key facts

- Founded: ${site.foundingYear}, starting with a Day Care and Nursery section; a Primary School was added later.
- Founder & Proprietress: ${site.founder.name}, ${site.founder.qualification}; 9+ years in education.
- Address: ${site.address.oneLine}, Nigeria
- Phone: ${site.contact.phoneDisplay}
- Email: ${site.contact.email}
- Parents check published results online at ${site.resultPortalUrl} with the pupil's registration number and access PIN. No account is needed.
${socialLinks.map((s) => `- ${s.label}: ${s.href}`).join("\n")}

## Sections and ages

${services.map((s) => `- ${s.name} (${s.ages}): ${s.summary}`).join("\n")}

## Mission

${mission}

## Vision

${vision}

## Core values (GREATNESS)

${coreValues.map((v) => `- ${v.letter}: ${v.name}`).join("\n")}

## Why parents choose the school

${reasonsLong.map((r) => `- ${r}`).join("\n")}

## Pages

- [Home](${abs(routes.home)}): overview, services, gallery, result portal
- [About](${abs(routes.about)}): story, mission and vision, core values, the proprietress
- [Contact](${abs(routes.contact)}): phone, email, address, directions and enquiry form
- [Result portal](${site.resultPortalUrl}): view a pupil's published result
`
  return new Response(body, {
    headers: { "content-type": "text/plain; charset=utf-8", "cache-control": "public, max-age=3600" },
  })
}
