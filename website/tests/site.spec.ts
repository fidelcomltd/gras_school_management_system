import { expect, test } from "@playwright/test"

const pages = ["/", "/about", "/contact"]

test.describe("every page", () => {
  for (const path of pages) {
    test(`${path}: one h1, canonical, valid JSON-LD, no sideways scroll`, async ({ page }) => {
      const res = await page.goto(path)
      expect(res?.status()).toBe(200)
      await expect(page.locator("h1")).toHaveCount(1)
      await expect(page.locator('link[rel="canonical"]')).toHaveAttribute("href", /^https?:\/\//)
      await expect(page.locator('meta[property="og:image"]')).toHaveAttribute("content", /opengraph-image/)

      const blocks = await page.locator('script[type="application/ld+json"]').allTextContents()
      expect(blocks.length).toBeGreaterThan(0)
      for (const b of blocks) expect(() => JSON.parse(b)).not.toThrow()

      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
      expect(overflow).toBeLessThanOrEqual(0)
    })
  }

  test("result portal links all point at the configured portal and open a new tab", async ({ page }) => {
    await page.goto("/")
    const links = page.locator('a[href*="results"]')
    expect(await links.count()).toBeGreaterThan(1)
    for (const link of await links.all()) {
      await expect(link).toHaveAttribute("target", "_blank")
      await expect(link).toHaveAttribute("rel", /noopener/)
    }
  })
})

// Tags scope a describe to one project (playwright.config.ts grepInvert), so nothing is reported as skipped.
test.describe("phone menu", { tag: "@phone" }, () => {
  test("opens, closes with Escape, and navigates", async ({ page }) => {
    await page.goto("/")
    await page.getByRole("button", { name: "Open menu" }).click()
    const menu = page.getByRole("dialog", { name: "Menu" })
    await expect(menu).toBeVisible()
    await page.keyboard.press("Escape")
    await expect(menu).toBeHidden()

    await page.getByRole("button", { name: "Open menu" }).click()
    await menu.getByRole("link", { name: "About" }).click()
    await expect(page).toHaveURL(/\/about$/)
    await expect(menu).toBeHidden()
  })
})

test.describe("contact form", () => {
  test("empty submit shows every required-field error and sends nothing", async ({ page }) => {
    let posted = false
    await page.route("**/api/contact", (route) => {
      posted = true
      return route.fulfill({ json: { ok: true } })
    })
    await page.goto("/contact")
    await page.getByRole("button", { name: /send message/i }).click()
    await expect(page.locator("form [role=alert]")).toContainText("Please check the highlighted fields")
    await expect(page.getByText("Please enter your full name.")).toBeVisible()
    await expect(page.getByText("Please enter a valid phone number.")).toBeVisible()
    await expect(page.getByText("Please write a short message.")).toBeVisible()
    await expect(page.getByText("Please tick the box so we can reply to you.")).toBeVisible()
    await expect(page.getByLabel(/full name/i)).toBeFocused()
    expect(posted).toBe(false)
  })

  async function fill(page: import("@playwright/test").Page, phone = "813 000 0000") {
    await page.getByLabel(/full name/i).fill("Ada Okafor")
    await page.getByLabel(/phone number/i).fill(phone)
    await page
      .getByRole("textbox", { name: /^message/i })
      .fill("Hello, I would like to know more about your Preschool.")
    await page.getByRole("checkbox").check()
  }

  test("valid submit posts the enquiry and thanks the parent by first name", async ({ page }) => {
    let body: Record<string, unknown> = {}
    await page.route("**/api/contact", async (route) => {
      body = route.request().postDataJSON()
      await route.fulfill({ json: { ok: true } })
    })
    await page.goto("/contact")
    // Typed with the country code, as phone autofill often does: must be accepted.
    await fill(page, "+234 813 000 0000")
    await page.getByRole("button", { name: /send message/i }).click()
    await expect(page.getByRole("heading", { name: "Thank you, Ada!" })).toBeVisible()
    expect(body).toMatchObject({
      name: "Ada Okafor",
      phone: "+234 813 000 0000",
      consent: true,
      topic: "General enquiry",
    })
    expect(typeof body.elapsedMs).toBe("number")
  })

  test("a failed send keeps the message and offers the phone number", async ({ page }) => {
    await page.route("**/api/contact", (route) => route.fulfill({ status: 502, json: { ok: false } }))
    await page.goto("/contact")
    await fill(page)
    await page.getByRole("button", { name: /send message/i }).click()
    await expect(page.locator("form [role=alert]")).toContainText("couldn't send your message")
    await expect(page.getByLabel(/full name/i)).toHaveValue("Ada Okafor")
  })
})

test.describe("/api/contact", { tag: "@api" }, () => {
  test("rejects an invalid enquiry with the failing fields", async ({ request }) => {
    const res = await request.post("/api/contact", {
      data: { name: "A", phone: "12", email: "x", topic: "General enquiry", message: "", consent: false },
    })
    expect(res.status()).toBe(400)
    expect((await res.json()).fields).toEqual(expect.arrayContaining(["name", "phone", "email", "message", "consent"]))
  })

  test("accepts Nigerian numbers typed with 0, without it, or with +234; rejects malformed ones", async ({
    request,
  }) => {
    const fieldsFor = async (phone: string) => {
      // An invalid name keeps the request from reaching the email provider; only `phone` is under test.
      const res = await request.post("/api/contact", {
        data: { name: "A", phone, email: "", topic: "General enquiry", message: "Hello", consent: true },
      })
      return (await res.json()).fields as string[]
    }
    for (const ok of ["0813 647 5557", "813 647 5557", "+234 813 647 5557", "2348136475557"])
      expect(await fieldsFor(ok), ok).not.toContain("phone")
    for (const bad of ["0813647555", "81364755570", "12", "+44 20 7946 0958"])
      expect(await fieldsFor(bad), bad).toContain("phone")
  })

  test("silently accepts (and drops) a honeypot submission", async ({ request }) => {
    const res = await request.post("/api/contact", {
      data: {
        name: "Bot",
        phone: "8130000000",
        email: "",
        topic: "General enquiry",
        message: "spam",
        consent: true,
        website: "http://spam",
      },
    })
    expect(res.status()).toBe(200)
  })
})
