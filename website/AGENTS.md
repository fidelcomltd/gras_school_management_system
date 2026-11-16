# AGENTS.md — Golden Royal Ark School website

> The single source of truth for every contributor, human or AI. Tool-specific files (`CLAUDE.md`) only
> point here. If this file and the tooling disagree, the tooling wins and this file has a bug: fix it.

Public marketing site for **Golden Royal Ark School** (crèche, preschool and primary, Owerri, Imo
State), served at **goldenroyalark.com** from Netlify. Three pages (Home, About, Contact), a contact form
that emails the school through Resend, and links to the parents' result portal (a separate app in this
monorepo).

**Design spec:** the canvas "Golden Royal Ark School — Website"
(https://claude.ai/artifact/ML7H8aDhBDuK83uzRkdapR): Home, About and Contact at 1440 and 390, the
phone menu, and a style guide. Copy is verbatim from it.

## Stack

|           |                                                                                                      |
| --------- | ---------------------------------------------------------------------------------------------------- |
| Framework | Next.js 16 App Router, React 19, TypeScript strict (`noUncheckedIndexedAccess`)                      |
| Styling   | Tailwind CSS v4, CSS-first: every token is in `app/globals.css` `@theme`. No tailwind.config         |
| Motion    | CSS only (keyframes in `@theme`) + one IntersectionObserver (`RevealObserver`). No animation library |
| Email     | Resend, from the `/api/contact` route handler                                                        |
| Tests     | Playwright e2e against the production build                                                          |
| Hosting   | Netlify + Next.js runtime (declared in netlify.toml), Node 22                                        |

## Commands

```bash
npm run dev          # next dev on :3000. Styles stale after editing globals.css? npm run dev:clean
npm run check        # typecheck + lint (0 warnings) + prettier. Run before every commit
npm run build        # production build (all pages static; only /api/contact is dynamic)
npm test             # Playwright, against `next start` on :3000 (build first)
npm run verify       # check → build → test: what CI should run
npm run images       # regenerate photo masters + icons from ../assets (see scripts/process-images.mjs)
npm run screenshots  # full-page PNGs of every route at 390/768/1440 into .screenshots/
```

Open the site at **http://localhost:3000**, not 127.0.0.1: Next blocks the dev HMR socket for other
origins and the page reloads in a loop.

## Where things go

```
app/                  routes only: page.tsx, layout, metadata routes (sitemap, robots, manifest,
                      opengraph-image, llms.txt), api/contact
components/
  ui/                 primitives: Container, Button (ButtonLink/ButtonAnchor/ResultPortalButton),
                      Typography (Eyebrow/Heading/Lead), Icon, Crest, Waves, Breadcrumb
  cards/              ServiceCard, FounderPortrait (used by 2+ sections)
  layout/             UtilityBar, SiteHeader, MainNav, MobileMenu, SiteFooter, SocialIcons
  motion/             Reveal (`reveal()` attributes), RevealObserver
  sections/<page>/    sections used by ONE page (home/, about/, contact/)
  seo/                JsonLd
config/               env.ts (public env, validated), env.server.ts (secrets), site.ts (identity +
                      contact details), routes.ts (every internal path)
content/              all copy and photo registry (images.ts). Components hold no paragraphs
lib/                  framework-free helpers: cn, phone, seo/ (metadata, json-ld, og-image), contact/
hooks/                client hooks
assets/               committed image masters (imported statically → blur placeholders, no CLS)
tests/                Playwright
```

## Rules

1. **Tokens only.** Colours, fonts, shadows and motion live in `app/globals.css` `@theme`. Components use
   the utilities (`bg-navy`, `text-crimson`, `shadow-card`). New colour, shadow or overlay from the design → add a token (shadows and the `fade-*` overlays are built from the colour tokens with `color-mix`). Literal hex is allowed only where CSS variables cannot reach: viewport `themeColor`, the manifest, the share-card renderer (satori) and the HTML email.
2. **Copy lives in `content/`**, verbatim from the design. Where the phone artboard words something
   differently, the content object has a `mobile…` variant and the component shows one per breakpoint.
3. **Links come from `config/routes.ts`** (lint-enforced: no `href="/…"`). Contact details, the result
   portal URL and social links come from `config/site.ts`, which reads the environment. Never type a
   phone number, email or URL into a component.
4. **Server Components by default.** `"use client"` only for leaves that need state or browser APIs
   (carousel, menu, form, section nav). Animate server markup with `{...reveal()}`, not a client wrapper.
5. **Client code stays light.** Anything a client component imports ships to every visitor: `config/env.ts`
   and `lib/contact/schema.ts` are deliberately dependency-free (zod cost ~90 KB gzipped here).
6. **Every page:** `export const metadata = buildMetadata({...})`, exactly one `<h1>`, page JSON-LD
   (`pageJsonLd` + `breadcrumbJsonLd`), and an entry in `sitemapRoutes`.
7. **Reduced motion is respected globally** (globals.css). The hero does not autoplay for those visitors.
8. **Don't put `{...reveal()}` on an element that also has a transition/hover utility** (`card-lift`,
   `transition-*`): the utility's `transition` overrides the reveal's. Reveal a wrapper instead.
9. **Prove fidelity visually** before calling a design change done: screenshot at 390 and 1440 and compare
   with the canvas artboard section by section.

## Translating the design: gotchas that cost time

- **The canvas has no `box-sizing` reset.** `max-width:1240px; padding:0 32px` is 1304px wide in total
  (hence `Container`'s `md:max-w-[1304px]`). A box with `height:52px` and a 1.5px border is 55px tall;
  a photo with `height:200px` and a 6px border is 212px. Add borders/padding to stated sizes.
- **The canvas sets no base line-height** (browser `normal`). `@theme` resets Tailwind's named text sizes
  to `normal` too, so add `leading-*` only where the design states one.
- **tailwind-merge drops `leading-*` when it merges a `text-[size]` class.** Size headings with
  `<Heading size="sm|md">`, not `className="text-[28px]"`.
- Tailwind v4 `rotate-*`, `translate-*`, `scale-*` set the individual `rotate`/`translate`/`scale`
  properties: transition those, not `transform`.

## Environment

`.env.example` is the contract and `config/env.ts` validates it (a bad value fails the build with the
variable's name). `NEXT_PUBLIC_*` values are baked in at build: change, then redeploy.

| Variable                                                                               | Purpose                                                                           |
| -------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| `NEXT_PUBLIC_SITE_URL`                                                                 | canonical origin (metadata, sitemap, JSON-LD, share cards)                        |
| `NEXT_PUBLIC_SITE_ENV`                                                                 | `production` or `preview`; anything but production is `noindex` + robots disallow |
| `NEXT_PUBLIC_RESULT_PORTAL_URL`                                                        | every "Check results" link (staging and production differ)                        |
| `NEXT_PUBLIC_CONTACT_PHONE` / `_EMAIL` / `NEXT_PUBLIC_ADDRESS_*` / `NEXT_PUBLIC_GEO_*` | contact details everywhere, incl. structured data                                 |
| `NEXT_PUBLIC_TIKTOK_URL` / `NEXT_PUBLIC_FACEBOOK_URL`                                  | social links; blank hides them                                                    |
| `NEXT_PUBLIC_GOOGLE_SITE_VERIFICATION`                                                 | Search Console HTML-tag token                                                     |
| `RESEND_API_KEY`, `CONTACT_FORM_TO`, `CONTACT_FORM_FROM`                               | contact email (server only)                                                       |

## Deployment (Netlify)

A **separate Netlify site** from the admin app (whose config is the repo-root `netlify.toml`).

1. New site from this repo → **Base directory: `website`** (Netlify then reads `website/netlify.toml`).
2. Environment variables per `.env.example`, per deploy context. Deploy previews and branch deploys get
   `NEXT_PUBLIC_SITE_ENV=preview` from `netlify.toml`, so they are never indexed.
3. Domain: add `goldenroyalark.com` and `www.goldenroyalark.com`; make the apex primary (www redirects).
4. Resend: add and verify the `goldenroyalark.com` domain (DNS records), then set `RESEND_API_KEY`.
   Without it production answers the form with 503 and logs why; locally the enquiry is logged instead.
5. After the first deploy: submit `https://goldenroyalark.com/sitemap.xml` in Google Search Console and
   Bing Webmaster Tools, and create/claim the Google Business Profile with the same name, address and phone.

## SEO surface (all generated from config + content)

`/sitemap.xml`, `/robots.txt` (AI crawlers explicitly welcome), `/llms.txt` (plain-text brief for AI
assistants), `/manifest.webmanifest`, per-page canonical + Open Graph + Twitter cards, branded share
images (`opengraph-image.tsx`, JPEG < 120 KB for WhatsApp), and schema.org `School`, `WebSite`,
`WebPage`/`AboutPage`/`ContactPage`, `BreadcrumbList` and the founder as `Person`.

## Never

- Commit `.env` / `.env.local` or any secret. Push, or commit to `main`.
- Disable a lint rule to get green: fix the code, or change the rule here with the reason.
- Add a dependency that client components import without measuring the bundle first.
