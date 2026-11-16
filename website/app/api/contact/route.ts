import { NextResponse } from "next/server"
import { validateEnquiry } from "@/lib/contact/schema"
import { sendEnquiry } from "@/lib/contact/send-enquiry"

/**
 * POST /api/contact — the contact form's only endpoint.
 * 200 { ok: true } · 400 { ok: false, fields } · 413 too large · 429 slow down · 502/503 not sent.
 * Spam: a honeypot field and a minimum fill time are answered with a silent 200 (no hint to bots).
 */
export const runtime = "nodejs"

const MAX_BODY_BYTES = 8 * 1024
const MIN_FILL_MS = 2500
const WINDOW_MS = 10 * 60 * 1000
const MAX_PER_WINDOW = 5

// Best-effort per-IP limiter. Serverless instances don't share memory, so this blunts bursts from a
// single client rather than enforcing a global quota — enough for a school contact form.
const hits = new Map<string, number[]>()
function rateLimited(ip: string, now = Date.now()) {
  const recent = (hits.get(ip) ?? []).filter((t) => now - t < WINDOW_MS)
  recent.push(now)
  hits.set(ip, recent)
  if (hits.size > 5000) hits.clear()
  return recent.length > MAX_PER_WINDOW
}

export async function POST(request: Request) {
  const declared = Number(request.headers.get("content-length") ?? 0)
  if (declared > MAX_BODY_BYTES) return NextResponse.json({ ok: false }, { status: 413 })
  const raw = await request.text()
  if (Buffer.byteLength(raw) > MAX_BODY_BYTES) return NextResponse.json({ ok: false }, { status: 413 })

  let body: unknown
  try {
    body = JSON.parse(raw)
  } catch {
    return NextResponse.json({ ok: false, fields: [] }, { status: 400 })
  }

  const parsed = validateEnquiry(body)
  if (!parsed.ok) return NextResponse.json({ ok: false, fields: parsed.fields }, { status: 400 })

  const { website, elapsedMs, name, phone, email, topic, message } = parsed.data
  if (website || (elapsedMs !== undefined && elapsedMs < MIN_FILL_MS)) return NextResponse.json({ ok: true })

  const ip =
    request.headers.get("x-nf-client-connection-ip") ??
    request.headers.get("x-forwarded-for")?.split(",")[0]?.trim() ??
    "unknown"
  if (rateLimited(ip)) return NextResponse.json({ ok: false }, { status: 429 })

  const result = await sendEnquiry({ name, phone, email, topic, message })
  if (!result.ok) return NextResponse.json({ ok: false }, { status: result.reason === "not-configured" ? 503 : 502 })
  return NextResponse.json({ ok: true })
}
