import "server-only"
import { Resend } from "resend"
import { serverEnv } from "@/config/env.server"
import { site } from "@/config/site"
import { toInternationalPhone } from "@/lib/contact/schema"
import { formatPhone } from "@/lib/phone"

export type Enquiry = { name: string; phone: string; email: string; topic: string; message: string }

const escapeHtml = (s: string) =>
  s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]!)

function renderEmail(e: Enquiry) {
  const phone = formatPhone(toInternationalPhone(e.phone))
  const rows: [string, string][] = [
    ["Name", e.name],
    ["Phone", phone],
    ["Email", e.email || "Not given"],
    ["Enquiring about", e.topic],
  ]
  const text = [
    `New enquiry from the ${site.name} website`,
    "",
    ...rows.map(([k, v]) => `${k}: ${v}`),
    "",
    "Message:",
    e.message,
    "",
    `Reply by phone: ${phone}${e.email ? ` · or reply to this email` : ""}`,
  ].join("\n")

  const html = `<!doctype html><html><body style="margin:0;background:#f6fafe;font-family:Arial,Helvetica,sans-serif;color:#16213e">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:24px 12px"><tr><td align="center">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:16px;border:1px solid #e3edf8">
<tr><td style="background:#0e1d47;border-radius:16px 16px 0 0;padding:20px 24px;color:#ffffff;font-size:18px;font-weight:bold">New website enquiry
<div style="color:#c89b3c;font-size:12px;letter-spacing:2px;text-transform:uppercase;margin-top:4px">${escapeHtml(site.name)}</div></td></tr>
<tr><td style="padding:20px 24px"><table role="presentation" width="100%" cellpadding="0" cellspacing="0">
${rows
  .map(
    ([k, v]) =>
      `<tr><td style="padding:6px 0;color:#4a5675;font-size:13px;width:140px;vertical-align:top">${k}</td><td style="padding:6px 0;font-size:15px;font-weight:bold">${escapeHtml(v)}</td></tr>`
  )
  .join("")}
</table>
<div style="margin-top:16px;padding:16px;border-radius:12px;background:#f6fafe;border:1px solid #e3edf8;font-size:15px;line-height:1.6;white-space:pre-wrap">${escapeHtml(e.message)}</div>
<p style="margin:20px 0 0;font-size:13px;color:#4a5675">Call back: <a href="tel:${toInternationalPhone(e.phone)}" style="color:#a61b37;font-weight:bold">${escapeHtml(phone)}</a>${
    e.email ? " · or just reply to this email." : ""
  }</p></td></tr></table></td></tr></table></body></html>`

  return { text, html }
}

export type SendResult = { ok: true; id?: string } | { ok: false; reason: "not-configured" | "provider-error" }

/** Emails an enquiry to the school via Resend. Reply-To is the parent, when they gave an email. */
export async function sendEnquiry(enquiry: Enquiry): Promise<SendResult> {
  if (!serverEnv.resendApiKey) {
    if (process.env.NODE_ENV !== "production") {
      console.info("[contact] RESEND_API_KEY not set — enquiry logged instead of emailed:\n", renderEmail(enquiry).text)
      return { ok: true }
    }
    console.error("[contact] RESEND_API_KEY is not set in production; enquiry not sent")
    return { ok: false, reason: "not-configured" }
  }

  const { text, html } = renderEmail(enquiry)
  const resend = new Resend(serverEnv.resendApiKey)
  const { data, error } = await resend.emails.send({
    from: serverEnv.contactFrom,
    to: [serverEnv.contactTo],
    ...(enquiry.email ? { replyTo: enquiry.email } : {}),
    subject: `Website enquiry: ${enquiry.topic} — ${enquiry.name}`,
    text,
    html,
  })
  if (error) {
    console.error("[contact] Resend rejected the email:", error.name, error.message)
    return { ok: false, reason: "provider-error" }
  }
  return { ok: true, id: data?.id }
}
