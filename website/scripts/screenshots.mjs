// Full-page screenshots for visual review against the design.
//
//   npm run screenshots                                   # site on localhost:3000, all routes, 390/768/1440
//   npm run screenshots -- --base http://localhost:3000 --routes /,/about --widths 390,1440 --out .screenshots
//   npm run screenshots -- --selector "#results"          # one element instead of the full page
//
// Animations are settled (finished, not paused mid-way) and every scroll-reveal is forced visible, so the
// capture shows the page's resting state. Output: <out>/<route-name>@<width>.png

import { mkdir } from "node:fs/promises"
import path from "node:path"
import { chromium } from "@playwright/test"

const arg = (name, fallback) => {
  const i = process.argv.indexOf(`--${name}`)
  return i > -1 ? process.argv[i + 1] : fallback
}

const base = arg("base", "http://localhost:3000")
const routes = arg("routes", "/,/about,/contact").split(",")
const widths = arg("widths", "390,768,1440").split(",").map(Number)
const out = path.resolve(arg("out", ".screenshots"))
const selector = arg("selector", "")
const waitMs = Number(arg("wait", "600"))

const settle = `
  *, *::before, *::after { animation-duration: 0s !important; animation-delay: 0s !important;
    transition-duration: 0s !important; transition-delay: 0s !important; scroll-behavior: auto !important; }
`

await mkdir(out, { recursive: true })
const browser = await chromium.launch()
try {
  for (const width of widths) {
    // Reduced motion keeps the hero on slide 1, so captures are deterministic.
    const page = await browser.newPage({
      viewport: { width, height: width < 768 ? 844 : 900 },
      deviceScaleFactor: 1,
      reducedMotion: arg("motion", "reduce") === "reduce" ? "reduce" : "no-preference",
    })
    for (const route of routes) {
      await capture(page, route, width)
    }
    await page.close()
  }
} finally {
  await browser.close()
}

async function capture(page, route, width, attempt = 1) {
  try {
    await page.goto(new URL(route, base).toString(), { waitUntil: "networkidle" })
    await page.addStyleTag({ content: settle })
    await page.evaluate(async () => {
      document.querySelectorAll("[data-reveal]").forEach((el) => el.classList.add("is-revealed"))
      // Walk the page so lazy images load, then return to the top.
      for (let y = 0; y < document.body.scrollHeight; y += 600) {
        window.scrollTo(0, y)
        await new Promise((r) => setTimeout(r, 40))
      }
      window.scrollTo(0, 0)
      // Lazy images that never enter the viewport (e.g. off-screen marquee cards) never load: cap the wait.
      const loaded = Promise.all(
        Array.from(document.images).map((img) =>
          img.complete ? null : new Promise((r) => img.addEventListener("load", r, { once: true }))
        )
      )
      await Promise.race([loaded, new Promise((r) => setTimeout(r, 5000))])
    })
    await page.waitForTimeout(waitMs)
    const name = route
      .replace(/^\/$/, "home")
      .replace(/[^a-z0-9.-]+/gi, "-")
      .replace(/^-|-$/g, "")
    const file = path.join(out, `${name}@${width}.png`)
    if (selector) await page.locator(selector).first().screenshot({ path: file })
    else await page.screenshot({ path: file, fullPage: true })
    console.log(file)
  } catch (error) {
    // `next dev` can reload the page once while it compiles a route for the first time.
    if (attempt >= 3) throw error
    await capture(page, route, width, attempt + 1)
  }
}
