import type { Thing, WithContext } from "schema-dts"

/** Renders schema.org data. `<` is escaped so no string in the data can close the script tag. */
export function JsonLd({ data }: { data: WithContext<Thing> | WithContext<Thing>[] }) {
  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: JSON.stringify(data).replace(/</g, "\\u003c") }}
    />
  )
}
