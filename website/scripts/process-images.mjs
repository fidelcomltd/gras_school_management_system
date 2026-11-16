// Turns the school's original photos and crest into the site's committed image masters.
//
//   npm run images                       # reads ../assets and ../frontend/public/crest.png
//   npm run images -- --src <dir>        # a different originals folder
//
// What it does, and why:
//   • auto-orients and STRIPS ALL METADATA. These are photos of children; phone EXIF can carry GPS.
//   • upscales every photo so its long side is 2048px (the originals are ~1080px WhatsApp exports and
//     the hero shows them full-bleed at up to 1440 CSS px). Lanczos3 + a gentle unsharp mask; strong
//     sharpening makes JPEG blocking visible, so the settings below are deliberately mild.
//   • writes the crest at 2× (320px) for retina headers, plus favicon / app icons / manifest icons.
//
// next/image does the per-device resizing and AVIF/WebP at request time; these files are only masters.
// Re-run this whenever the school sends better originals (e.g. a high-resolution or vector crest).

import { mkdir, writeFile } from "node:fs/promises"
import path from "node:path"
import { fileURLToPath } from "node:url"
import sharp from "sharp"

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
const argSrc = process.argv.indexOf("--src")
const srcDir = argSrc > -1 ? path.resolve(process.argv[argSrc + 1]) : path.resolve(root, "../assets")
const crestSrc = path.resolve(root, "../frontend/public/crest.png")
const photosOut = path.join(root, "assets/photos")
const brandOut = path.join(root, "assets/brand")
const appDir = path.join(root, "app")
const publicDir = path.join(root, "public")

const W = "WhatsApp Image 2026-11-07 at "
/** original filename → semantic name used in content/ */
const PHOTOS = {
  [`${W}7.54.30 AM.jpeg`]: "morning-assembly",
  [`${W}7.54.31 AM.jpeg`]: "reading-book",
  [`${W}7.54.31 AM (1).jpeg`]: "band-march",
  [`${W}7.54.31 AM (2).jpeg`]: "welcome-mural",
  [`${W}7.54.32 AM (4).jpeg`]: "playground",
  [`${W}7.54.34 AM (1).jpeg`]: "school-grounds",
  [`${W}7.54.34 AM (3).jpeg`]: "toddlers-lawn",
  [`${W}7.54.35 AM (1).jpeg`]: "outdoor-blocks",
  [`${W}7.54.35 AM (2).jpeg`]: "lawn-play",
  [`${W}7.54.35 AM (3).jpeg`]: "preschool-blocks",
  [`${W}7.54.36 AM.jpeg`]: "building-blocks",
  [`${W}7.54.36 AM (1).jpeg`]: "classroom-smiles",
  [`${W}7.54.36 AM (2).jpeg`]: "school-party",
  [`${W}7.54.36 AM (3).jpeg`]: "school-celebration",
  [`${W}7.54.36 AM (4).jpeg`]: "canopy-celebration",
  "the-proprietor.jpeg": "proprietress",
}

const LONG_SIDE = 2048
const NAVY = { r: 14, g: 29, b: 71, alpha: 1 }

async function processPhoto(file, name) {
  const input = sharp(path.join(srcDir, file)).rotate()
  const { width = 0, height = 0 } = await input.metadata()
  const portrait = height >= width
  await input
    .resize({ ...(portrait ? { height: LONG_SIDE } : { width: LONG_SIDE }), kernel: "lanczos3" })
    .sharpen({ sigma: 0.9, m1: 0.6, m2: 1.6, x1: 2, y2: 8, y3: 12 })
    .jpeg({ quality: 88, mozjpeg: true, chromaSubsampling: "4:4:4" })
    .toFile(path.join(photosOut, `${name}.jpg`))
  console.log(`photo  ${name}.jpg  (${width}×${height} → long side ${LONG_SIDE})`)
}

/** Crest centred on a square canvas. `pad` is the share of the side left empty around it. */
function crestSquare(size, { pad = 0.08, background } = {}) {
  const inner = Math.round(size * (1 - pad * 2))
  return sharp(crestSrc)
    .resize(inner, inner, { fit: "contain", kernel: "lanczos3", background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .sharpen({ sigma: 0.6 })
    .toBuffer()
    .then((crest) =>
      sharp({
        create: { width: size, height: size, channels: 4, background: background ?? { r: 0, g: 0, b: 0, alpha: 0 } },
      })
        .composite([{ input: crest, gravity: "centre" }])
        // Icons are served byte-for-byte (no image optimiser in front of them), so quantise.
        .png({ palette: true, quality: 92, effort: 10, compressionLevel: 9 })
    )
}

/** Minimal multi-size .ico (PNG-compressed entries, supported by every current browser). */
async function writeIco(file, sizes) {
  const pngs = await Promise.all(sizes.map((s) => crestSquare(s, { pad: 0.02 }).then((img) => img.toBuffer())))
  const header = Buffer.alloc(6 + 16 * pngs.length)
  header.writeUInt16LE(0, 0)
  header.writeUInt16LE(1, 2)
  header.writeUInt16LE(pngs.length, 4)
  let offset = header.length
  pngs.forEach((png, i) => {
    const e = 6 + i * 16
    header.writeUInt8(sizes[i] >= 256 ? 0 : sizes[i], e)
    header.writeUInt8(sizes[i] >= 256 ? 0 : sizes[i], e + 1)
    header.writeUInt16LE(1, e + 4)
    header.writeUInt16LE(32, e + 6)
    header.writeUInt32LE(png.length, e + 8)
    header.writeUInt32LE(offset, e + 12)
    offset += png.length
  })
  await writeFile(file, Buffer.concat([header, ...pngs]))
}

async function processBrand() {
  await sharp(crestSrc)
    .resize({ width: 320, kernel: "lanczos3" })
    .sharpen({ sigma: 0.7 })
    .png({ compressionLevel: 9 })
    .toFile(path.join(brandOut, "crest.png"))
  await (await crestSquare(512, { pad: 0.06 })).toFile(path.join(appDir, "icon.png"))
  await (await crestSquare(180, { pad: 0.1, background: { r: 255, g: 255, b: 255, alpha: 1 } }))
    .flatten({ background: "#ffffff" })
    .toFile(path.join(appDir, "apple-icon.png"))
  await writeIco(path.join(appDir, "favicon.ico"), [16, 32, 48])
  // Manifest icons: "any" keeps transparency; "maskable" needs a solid ground and a wide safe zone.
  await (await crestSquare(192, { pad: 0.06 })).toFile(path.join(publicDir, "icon-192.png"))
  await (await crestSquare(512, { pad: 0.2, background: NAVY })).toFile(path.join(publicDir, "icon-maskable-512.png"))
  console.log("brand  crest.png, favicon.ico, icon.png, apple-icon.png, manifest icons")
}

await Promise.all([photosOut, brandOut, publicDir].map((d) => mkdir(d, { recursive: true })))
for (const [file, name] of Object.entries(PHOTOS)) await processPhoto(file, name)
await processBrand()
