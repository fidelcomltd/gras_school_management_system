import { readFile } from "node:fs/promises"
import path from "node:path"
import { ImageResponse } from "next/og"
import sharp from "sharp"
import { site } from "@/config/site"

/**
 * Branded 1200×630 share card (WhatsApp, Facebook, X, LinkedIn, iMessage, Slack…): a school photo
 * under a navy fade, the crest, the page's headline and the motto. Rendered once at build time.
 */
export const ogSize = { width: 1200, height: 630 }
// JPEG, not PNG: WhatsApp skips link previews whose image is larger than ~300 KB.
export const ogContentType = "image/jpeg"

// turbopackIgnore: these reads happen at build time only; without the hint the tracer pulls in the repo.
const root = /*turbopackIgnore: true*/ process.cwd()
const font = (weight: 700 | 800) =>
  readFile(
    path.join(
      /*turbopackIgnore: true*/ root,
      `node_modules/@fontsource/plus-jakarta-sans/files/plus-jakarta-sans-latin-${weight}-normal.woff`
    )
  )

async function dataUri(file: string, width: number, height?: number) {
  const buf = await sharp(path.join(/*turbopackIgnore: true*/ root, file))
    .resize({ width, height, fit: "cover", position: "attention" })
    .jpeg({ quality: 82 })
    .toBuffer()
  return `data:image/jpeg;base64,${buf.toString("base64")}`
}

export async function renderOgImage({ title, eyebrow, photo }: { title: string; eyebrow: string; photo: string }) {
  const [bold, extraBold, bg, crest] = await Promise.all([
    font(700),
    font(800),
    dataUri(`assets/photos/${photo}.jpg`, 760, 630),
    readFile(path.join(/*turbopackIgnore: true*/ root, "assets/brand/crest.png")).then(
      (b) => `data:image/png;base64,${b.toString("base64")}`
    ),
  ])

  const png = new ImageResponse(
    <div style={{ width: "100%", height: "100%", display: "flex", background: "#0e1d47", position: "relative" }}>
      {/* eslint-disable-next-line @next/next/no-img-element -- satori renders plain <img> */}
      <img
        src={bg}
        alt=""
        width={760}
        height={630}
        style={{ position: "absolute", right: 0, top: 0, objectFit: "cover" }}
      />
      {/* satori has no `inset`: the fade is positioned explicitly over the photo's left edge */}
      <div
        style={{
          position: "absolute",
          left: 440,
          top: 0,
          width: 520,
          height: 630,
          display: "flex",
          backgroundImage: "linear-gradient(90deg, #0e1d47 0%, rgba(14,29,71,0.82) 45%, rgba(14,29,71,0) 100%)",
        }}
      />
      <div
        style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          padding: "56px 64px",
          width: 760,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 18 }}>
          {/* eslint-disable-next-line @next/next/no-img-element -- satori renders plain <img> */}
          <img src={crest} alt="" width={84} height={81} />
          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            <span style={{ fontFamily: "Jakarta", fontWeight: 800, fontSize: 30, color: "#ffffff" }}>{site.name}</span>
            <span
              style={{
                fontFamily: "Jakarta",
                fontWeight: 700,
                fontSize: 15,
                letterSpacing: 3,
                color: "#c89b3c",
                textTransform: "uppercase",
              }}
            >
              {site.motto}
            </span>
          </div>
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 18 }}>
          <span style={{ fontFamily: "Jakarta", fontWeight: 700, fontSize: 20, color: "#f3e3b8" }}>{eyebrow}</span>
          <span
            style={{
              fontFamily: "Jakarta",
              fontWeight: 800,
              fontSize: 56,
              lineHeight: 1.08,
              color: "#ffffff",
              letterSpacing: -1,
            }}
          >
            {title}
          </span>
        </div>
        <div
          style={{ display: "flex", gap: 14, fontFamily: "Jakarta", fontWeight: 700, fontSize: 19, color: "#dce6f7" }}
        >
          <span>{`${site.address.city}, ${site.address.region}`}</span>
          <span style={{ color: "#c89b3c" }}>·</span>
          <span>{site.contact.phoneDisplay}</span>
        </div>
      </div>
      <div
        style={{
          position: "absolute",
          left: 0,
          right: 0,
          bottom: 0,
          height: 10,
          display: "flex",
          background: "#a61b37",
        }}
      />
    </div>,
    {
      ...ogSize,
      fonts: [
        { name: "Jakarta", data: bold, weight: 700, style: "normal" },
        { name: "Jakarta", data: extraBold, weight: 800, style: "normal" },
      ],
    }
  )
  const jpeg = await sharp(Buffer.from(await png.arrayBuffer()))
    .jpeg({ quality: 84, mozjpeg: true })
    .toBuffer()
  return new Response(new Uint8Array(jpeg), {
    headers: { "content-type": ogContentType, "cache-control": "public, max-age=31536000, immutable" },
  })
}
